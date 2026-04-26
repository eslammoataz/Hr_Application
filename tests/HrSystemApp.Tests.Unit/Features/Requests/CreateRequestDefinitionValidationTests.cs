using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Commands.Admin;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HrSystemApp.Tests.Unit.Features.Requests;

public class CreateRequestDefinitionValidationTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IOrgNodeRepository> _orgNodeRepoMock;
    private readonly Mock<IEmployeeRepository> _employeeRepoMock;
    private readonly Mock<IOrgNodeAssignmentRepository> _assignmentRepoMock;
    private readonly Mock<IRequestDefinitionRepository> _definitionRepoMock;

    // Use a placeholder GUID for testing - in real scenario this would be a valid RequestType Id
    private static readonly Guid LeaveRequestTypeId = Guid.NewGuid();

    public CreateRequestDefinitionValidationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _orgNodeRepoMock = new Mock<IOrgNodeRepository>();
        _employeeRepoMock = new Mock<IEmployeeRepository>();
        _assignmentRepoMock = new Mock<IOrgNodeAssignmentRepository>();
        _definitionRepoMock = new Mock<IRequestDefinitionRepository>();

        _unitOfWorkMock.SetupGet(x => x.OrgNodes).Returns(_orgNodeRepoMock.Object);
        _unitOfWorkMock.SetupGet(x => x.Employees).Returns(_employeeRepoMock.Object);
        _unitOfWorkMock.SetupGet(x => x.OrgNodeAssignments).Returns(_assignmentRepoMock.Object);
        _unitOfWorkMock.SetupGet(x => x.RequestDefinitions).Returns(_definitionRepoMock.Object);
    }

    private CreateRequestDefinitionCommandHandler CreateHandler()
    {
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<CreateRequestDefinitionCommandHandler>>();
        var loggingOptionsMock = new Mock<IOptions<LoggingOptions>>();
        loggingOptionsMock.Setup(x => x.Value).Returns(new LoggingOptions());
        return new CreateRequestDefinitionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object, loggerMock.Object, loggingOptionsMock.Object);
    }

    private void SetupUser(Guid employeeId, Guid companyId, string role = nameof(UserRole.CompanyAdmin))
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns("user123");
        _currentUserServiceMock.SetupGet(x => x.Role).Returns(role);
        _employeeRepoMock.Setup(x => x.GetByUserIdAsync("user123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = companyId, FullName = "Admin" });
    }

    [Fact]
    public async Task OverlappingHierarchyLevelRanges_ReturnsHierarchyRangesOverlap()
    {
        // Arrange: HL 1-3 overlaps with HL 2-4
        var companyId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 },
                new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 2, LevelsUp = 3, SortOrder = 2 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Request.HierarchyRangesOverlap);
    }

    [Fact]
    public async Task HierarchyLevel_WithLevelsUp0_ReturnsMissingLevelsUp()
    {
        var companyId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.HierarchyLevel, LevelsUp = 0, SortOrder = 1 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Request.MissingLevelsUp);
    }

    [Fact]
    public async Task HierarchyLevel_WithOrgNodeIdSet_ReturnsUnexpectedFieldsOnHierarchyLevelStep()
    {
        var companyId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.HierarchyLevel, OrgNodeId = nodeId, LevelsUp = 3, SortOrder = 1 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Request.UnexpectedFieldsOnHierarchyLevelStep);
    }

    [Fact]
    public async Task OrgNode_WithLevelsUpSet_ReturnsHierarchyLevelFieldsOnNonHierarchyStep()
    {
        var companyId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrgNode { Id = nodeId, CompanyId = companyId, Name = "Node" });

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.OrgNode, OrgNodeId = nodeId, LevelsUp = 3, SortOrder = 1 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Request.HierarchyLevelFieldsOnNonHierarchyStep);
    }

    [Fact]
    public async Task ValidHierarchyLevelDefinition_Succeeds()
    {
        var companyId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);
        _definitionRepoMock.Setup(x => x.AddAsync(It.IsAny<RequestDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition d, CancellationToken _) => d);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidMixedDefinition_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var fixedEmployeeId = Guid.NewGuid();
        SetupUser(Guid.NewGuid(), companyId);

        _definitionRepoMock.Setup(x => x.GetByTypeAsync(companyId, LeaveRequestTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition?)null);
        _employeeRepoMock.Setup(x => x.GetByIdAsync(fixedEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = fixedEmployeeId, CompanyId = companyId, FullName = "Special" });
        _definitionRepoMock.Setup(x => x.AddAsync(It.IsAny<RequestDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestDefinition d, CancellationToken _) => d);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateRequestDefinitionCommand
        {
            CompanyId = companyId,
            RequestTypeId = LeaveRequestTypeId,
            Steps = new List<WorkflowStepDto>
            {
                new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 2, SortOrder = 1 },
                new() { StepType = WorkflowStepType.DirectEmployee, DirectEmployeeId = fixedEmployeeId, SortOrder = 2 },
                new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 3, LevelsUp = 1, SortOrder = 3 }
            }
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
