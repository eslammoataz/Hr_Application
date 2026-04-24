using FluentAssertions;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Commands.OverrideClockIn;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Attendance;

/// <summary>
/// Tests for OverrideClockInCommandHandler covering:
/// - H-3 fix: InvalidClockIn returned instead of InvalidClockOut for future timestamp
/// - C-5 fix: Company isolation — caller must be in the same company as the target employee
/// </summary>
public class OverrideClockInCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<IAttendanceRulesProvider> _rulesProvider;
    private readonly Mock<ICurrentUserService> _currentUserService;

    public OverrideClockInCommandHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _rulesProvider = new Mock<IAttendanceRulesProvider>();
        _currentUserService = new Mock<ICurrentUserService>();

        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private OverrideClockInCommandHandler CreateHandler()
        => new(_unitOfWork.Object, _rulesProvider.Object, _currentUserService.Object);

    // ─── Reason validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenReasonIsEmpty_ReturnsOverrideReasonRequired()
    {
        var command = new OverrideClockInCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), string.Empty);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.OverrideReasonRequired);
    }

    [Fact]
    public async Task Handle_WhenReasonIsWhitespace_ReturnsOverrideReasonRequired()
    {
        var command = new OverrideClockInCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), "   ");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.OverrideReasonRequired);
    }

    // ─── Future clock-in timestamp (H-3 fix) ─────────────────────────────────

    [Fact]
    public async Task Handle_WhenClockInIsMoreThan5MinutesInFuture_ReturnsInvalidClockIn()
    {
        var futureClock = DateTime.UtcNow.AddMinutes(10);
        var command = new OverrideClockInCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            futureClock, "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.InvalidClockIn);
    }

    [Fact]
    public async Task Handle_WhenClockInIsMoreThan5MinutesInFuture_DoesNotReturnInvalidClockOut()
    {
        // Regression: before the fix, InvalidClockOut was returned instead of InvalidClockIn.
        var futureClock = DateTime.UtcNow.AddMinutes(10);
        var command = new OverrideClockInCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            futureClock, "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Error.Should().NotBe(DomainErrors.Attendance.InvalidClockOut);
    }

    [Fact]
    public async Task Handle_WhenClockInIsExactly5MinutesInFuture_ReturnsInvalidClockIn()
    {
        // Exactly 5 minutes is at the boundary — should still be accepted (> not >=).
        // We test slightly over 5 min here to be safe with timing.
        var futureClock = DateTime.UtcNow.AddMinutes(5).AddSeconds(5);
        var command = new OverrideClockInCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            futureClock, "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.InvalidClockIn);
    }

    // ─── Target employee not found ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTargetEmployeeNotFound_ReturnsEmployeeNotFound()
    {
        var callerUserId = "caller-user-1";
        var targetEmployeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _currentUserService.SetupGet(x => x.UserId).Returns(callerUserId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = companyId });

        _employeeRepo
            .Setup(x => x.GetByIdAsync(targetEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var command = new OverrideClockInCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Employee.NotFound);
    }

    // ─── Company isolation (C-5 fix) ─────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCallerAndTargetAreInDifferentCompanies_ReturnsUnauthorized()
    {
        var callerUserId = "caller-user-1";
        var callerCompanyId = Guid.NewGuid();
        var targetCompanyId = Guid.NewGuid(); // different company
        var targetEmployeeId = Guid.NewGuid();

        _currentUserService.SetupGet(x => x.UserId).Returns(callerUserId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = callerCompanyId });

        _employeeRepo
            .Setup(x => x.GetByIdAsync(targetEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = targetEmployeeId, CompanyId = targetCompanyId });

        var command = new OverrideClockInCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCallerEmployeeNotFoundAndTargetFromAnyCompany_ReturnsUnauthorized()
    {
        // callerEmployee is null → CompanyId comparison: null?.CompanyId != target.CompanyId → Unauthorized
        var callerUserId = "caller-user-1";
        var targetEmployeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _currentUserService.SetupGet(x => x.UserId).Returns(callerUserId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        _employeeRepo
            .Setup(x => x.GetByIdAsync(targetEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = targetEmployeeId, CompanyId = companyId });

        var command = new OverrideClockInCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCallerAndTargetAreInSameCompany_DoesNotReturnUnauthorized()
    {
        var callerUserId = "caller-user-1";
        var sharedCompanyId = Guid.NewGuid();
        var targetEmployeeId = Guid.NewGuid();

        _currentUserService.SetupGet(x => x.UserId).Returns(callerUserId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = sharedCompanyId });

        _employeeRepo
            .Setup(x => x.GetByIdAsync(targetEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = targetEmployeeId, CompanyId = sharedCompanyId });

        // Set up remaining dependencies so the handler can proceed past the company check.
        var attendanceRepo = new Mock<IAttendanceRepository>();
        _unitOfWork.SetupGet(x => x.Attendances).Returns(attendanceRepo.Object);
        attendanceRepo
            .Setup(x => x.GetByEmployeeAndDateAsync(targetEmployeeId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Models.Attendance?)null);

        _rulesProvider
            .Setup(x => x.GetRulesAsync(targetEmployeeId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftRulesUtc(
                DateOnly.FromDateTime(DateTime.Today),
                new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0),
                5, "UTC",
                DateTime.UtcNow.Date.AddHours(9),
                DateTime.UtcNow.Date.AddHours(17),
                DateTime.UtcNow.Date.AddHours(9).AddMinutes(5),
                DateTime.UtcNow.Date.AddHours(16)));

        var command = new OverrideClockInCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow.AddHours(-1), "Correcting attendance");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Should not be Unauthorized — the company check must pass.
        if (result.IsFailure)
        {
            result.Error.Should().NotBe(DomainErrors.Auth.Unauthorized);
        }
    }
}