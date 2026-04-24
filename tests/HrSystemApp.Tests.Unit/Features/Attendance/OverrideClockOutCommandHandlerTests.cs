using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Commands.OverrideClockOut;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Attendance;

/// <summary>
/// Tests for OverrideClockOutCommandHandler covering:
/// - C-5 fix: Company isolation — caller must be in the same company as the target employee
/// </summary>
public class OverrideClockOutCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<IAttendanceRulesProvider> _rulesProvider;
    private readonly Mock<ICurrentUserService> _currentUserService;

    public OverrideClockOutCommandHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _rulesProvider = new Mock<IAttendanceRulesProvider>();
        _currentUserService = new Mock<ICurrentUserService>();

        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private OverrideClockOutCommandHandler CreateHandler()
        => new(_unitOfWork.Object, _rulesProvider.Object, _currentUserService.Object);

    // ─── Reason validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenReasonIsEmpty_ReturnsOverrideReasonRequired()
    {
        var command = new OverrideClockOutCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, string.Empty);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.OverrideReasonRequired);
    }

    [Fact]
    public async Task Handle_WhenReasonIsWhitespace_ReturnsOverrideReasonRequired()
    {
        var command = new OverrideClockOutCommand(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "  ");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.OverrideReasonRequired);
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

        var command = new OverrideClockOutCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "Correcting clock-out");

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

        var command = new OverrideClockOutCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "Correcting clock-out");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCallerEmployeeIsNull_ReturnsUnauthorized()
    {
        // If the caller has no employee record, their CompanyId is null → mismatch with target.
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

        var command = new OverrideClockOutCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "Correcting clock-out");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenCallerAndTargetAreInSameCompany_PassesCompanyCheck()
    {
        var callerUserId = "caller-user-1";
        var sharedCompanyId = Guid.NewGuid();
        var targetEmployeeId = Guid.NewGuid();
        var attendanceId = Guid.NewGuid();

        _currentUserService.SetupGet(x => x.UserId).Returns(callerUserId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = sharedCompanyId });

        _employeeRepo
            .Setup(x => x.GetByIdAsync(targetEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = targetEmployeeId, CompanyId = sharedCompanyId });

        // Attendance exists with a clock-in so the clock-out validation can proceed.
        var clockInTime = DateTime.UtcNow.AddHours(-4);
        var attendance = new Domain.Models.Attendance
        {
            Id = attendanceId,
            EmployeeId = targetEmployeeId,
            Date = DateOnly.FromDateTime(DateTime.Today),
            FirstClockInUtc = clockInTime
        };

        var attendanceRepo = new Mock<IAttendanceRepository>();
        _unitOfWork.SetupGet(x => x.Attendances).Returns(attendanceRepo.Object);
        attendanceRepo
            .Setup(x => x.GetByEmployeeAndDateAsync(targetEmployeeId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attendance);

        var attendanceLogRepo = new Mock<IAttendanceLogRepository>();
        _unitOfWork.SetupGet(x => x.AttendanceLogs).Returns(attendanceLogRepo.Object);
        attendanceLogRepo
            .Setup(x => x.GetLastClockInAsync(attendanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceLog?)null); // uses FirstClockInUtc as fallback

        var command = new OverrideClockOutCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "Correcting clock-out");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Should not be Unauthorized once the company check passes.
        if (result.IsFailure)
        {
            result.Error.Should().NotBe(DomainErrors.Auth.Unauthorized);
        }
    }

    // ─── Attendance not found (after company check) ───────────────────────────

    [Fact]
    public async Task Handle_WhenAttendanceNotFound_ReturnsAttendanceNotFound()
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

        var attendanceRepo = new Mock<IAttendanceRepository>();
        _unitOfWork.SetupGet(x => x.Attendances).Returns(attendanceRepo.Object);
        attendanceRepo
            .Setup(x => x.GetByEmployeeAndDateAsync(targetEmployeeId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Models.Attendance?)null);

        var command = new OverrideClockOutCommand(
            targetEmployeeId, DateOnly.FromDateTime(DateTime.Today),
            DateTime.UtcNow, "Correcting clock-out");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Attendance.NotFound);
    }
}