using FluentAssertions;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HrSystemApp.Tests.Unit.Services;

public class WorkflowResolutionServiceHierarchyTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly WorkflowResolutionService _sut;
    private readonly Mock<IOrgNodeRepository> _orgNodeRepoMock;
    private readonly Mock<IOrgNodeAssignmentRepository> _assignmentRepoMock;
    private readonly Mock<IEmployeeRepository> _employeeRepoMock;

    public WorkflowResolutionServiceHierarchyTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orgNodeRepoMock = new Mock<IOrgNodeRepository>();
        _assignmentRepoMock = new Mock<IOrgNodeAssignmentRepository>();
        _employeeRepoMock = new Mock<IEmployeeRepository>();

        _unitOfWorkMock.SetupGet(x => x.OrgNodes).Returns(_orgNodeRepoMock.Object);
        _unitOfWorkMock.SetupGet(x => x.OrgNodeAssignments).Returns(_assignmentRepoMock.Object);
        _unitOfWorkMock.SetupGet(x => x.Employees).Returns(_employeeRepoMock.Object);

        _sut = new WorkflowResolutionService(
            _unitOfWorkMock.Object,
            new Mock<ILogger<WorkflowResolutionService>>().Object,
            Options.Create(new LoggingOptions()));
    }

    // ── Helper: build a fake org tree ─────────────────────────────────────────

    private OrgNode MkNode(Guid id, string name, Guid? parentId = null)
        => new() { Id = id, Name = name, ParentId = parentId };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HierarchyLevel_LevelsUp3_BuildsTeamDeptVp()
    {
        // Arrange: Alex sits at TeamA (L1) → Dept1 (L2) → VP1 (L3)
        var alexId = Guid.NewGuid();
        var tomId = Guid.NewGuid(); // TeamA manager
        var sarahId = Guid.NewGuid(); // Dept1 manager
        var mikeId = Guid.NewGuid(); // VP1 manager

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);
        var vp1 = MkNode(Guid.NewGuid(), "VP 1", dept1.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1, vp1 });

        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(vp1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mikeId, FullName = "Mike" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var chain = result.Value;
        chain.Should().HaveCount(3);
        chain[0].Approvers.Should().ContainSingle(a => a.EmployeeId == tomId);
        chain[1].Approvers.Should().ContainSingle(a => a.EmployeeId == sarahId);
        chain[2].Approvers.Should().ContainSingle(a => a.EmployeeId == mikeId);
    }

    [Fact]
    public async Task HierarchyLevel_FewerAncestors_ShortensChainGracefully()
    {
        // Arrange: Alex sits at TeamB (L1) → Dept2 (L2) only (no VP)
        var alexId = Guid.NewGuid();
        var tomId = Guid.NewGuid();
        var sarahId = Guid.NewGuid();

        var teamB = MkNode(Guid.NewGuid(), "Team B");
        var dept2 = MkNode(Guid.NewGuid(), "Dept 2", teamB.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamB);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept2 });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamB.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // truncated, not an error
    }

    [Fact]
    public async Task HierarchyLevel_EmptyManagerLevel_SkipsContinues()
    {
        // Arrange: Level 2 (Dept) has no managers
        var alexId = Guid.NewGuid();
        var tomId = Guid.NewGuid();
        var mikeId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);
        var vp1 = MkNode(Guid.NewGuid(), "VP 1", dept1.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1, vp1 });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee>()); // empty
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(vp1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mikeId, FullName = "Mike" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // Team A + VP1; Dept1 skipped
    }

    [Fact]
    public async Task HierarchyLevel_SelfApprovalAtResolvedLevel_Skips()
    {
        // Arrange: Alex is a manager at his own node (Team A)
        var alexId = Guid.NewGuid();
        var alexEmp = new Employee { Id = alexId, FullName = "Alex" };
        var sarahId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 2, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Alex IS a manager at his own node
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1 });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { alexEmp }); // Alex is the only manager
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Level 1 skipped (self-approval), Level 2 only → 1 step
        result.Value.Should().HaveCount(1);
        result.Value[0].Approvers.Should().ContainSingle(a => a.EmployeeId == sarahId);
    }

    [Fact]
    public async Task HierarchyLevel_DuplicateManagerAcrossLevels_AppearsOnlyAtEarliest()
    {
        // Arrange: Maya is manager at both Dept1 and VP1. Maya should appear only at Dept1 (earliest).
        // VP1 is skipped because Maya is its only manager and she was already seen.
        var alexId = Guid.NewGuid();
        var mayaId = Guid.NewGuid();
        var tomId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);
        var vp1 = MkNode(Guid.NewGuid(), "VP 1", dept1.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 3, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1, vp1 });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mayaId, FullName = "Maya" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(vp1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mayaId, FullName = "Maya" } }); // Maya again

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // 2 steps: TeamA (Tom) + Dept1 (Maya). VP1 skipped because Maya was the only manager and deduped.
        result.Value.Should().HaveCount(2);
        result.Value[0].Approvers.Select(a => a.EmployeeId).Should().Contain(tomId);
        result.Value[1].Approvers.Select(a => a.EmployeeId).Should().Contain(mayaId);
    }

    [Fact]
    public async Task Mixed_DirectEmployee_Then_HierarchyLevel_ExpandsCorrectly()
    {
        // Arrange: DirectEmployee (fixed approver) + HierarchyLevel (levels 1-2)
        var alexId = Guid.NewGuid();
        var fixedId = Guid.NewGuid();
        var tomId = Guid.NewGuid();
        var sarahId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.DirectEmployee, DirectEmployeeId = fixedId, SortOrder = 1 },
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 2, SortOrder = 2 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1 });
        _employeeRepoMock.Setup(x => x.GetByIdAsync(fixedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = fixedId, FullName = "Fixed Approver" });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Approvers.First().EmployeeId.Should().Be(fixedId);
        result.Value[1].Approvers.First().EmployeeId.Should().Be(tomId);
        result.Value[2].Approvers.First().EmployeeId.Should().Be(sarahId);
    }

    [Fact]
    public async Task Mixed_Split_L1to2_Then_DirectEmployee_Then_L3_Renumbered()
    {
        // Arrange: HL 1-2 → DirectEmployee → HL 3
        var alexId = Guid.NewGuid();
        var fixedId = Guid.NewGuid();
        var tomId = Guid.NewGuid();
        var sarahId = Guid.NewGuid();
        var mikeId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);
        var vp1 = MkNode(Guid.NewGuid(), "VP 1", dept1.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 2, SortOrder = 1 },
            new() { StepType = WorkflowStepType.DirectEmployee, DirectEmployeeId = fixedId, SortOrder = 2 },
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 3, LevelsUp = 1, SortOrder = 3 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1, vp1 });
        _employeeRepoMock.Setup(x => x.GetByIdAsync(fixedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = fixedId, FullName = "Special" });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = tomId, FullName = "Tom" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(vp1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mikeId, FullName = "Mike" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value.Select(s => s.SortOrder).Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public async Task SortOrders_OnPlannedChain_AreContiguous1ToN()
    {
        // Arrange: HL with StartFromLevel=2, LevelsUp=2 → covers levels 2 and 3
        var alexId = Guid.NewGuid();
        var tomId = Guid.NewGuid();
        var sarahId = Guid.NewGuid();
        var mikeId = Guid.NewGuid();

        var teamA = MkNode(Guid.NewGuid(), "Team A");
        var dept1 = MkNode(Guid.NewGuid(), "Dept 1", teamA.Id);
        var vp1 = MkNode(Guid.NewGuid(), "VP 1", dept1.Id);

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 2, LevelsUp = 2, SortOrder = 99 } // non-contiguous sortOrder in definition
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode> { dept1, vp1 }); // [dept1, vp1] → levelNodes = [teamA, dept1, vp1]
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(dept1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = sarahId, FullName = "Sarah" } });
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(vp1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { new() { Id = mikeId, FullName = "Mike" } });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(s => s.SortOrder).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task EmptyChain_CallerAutoApproves()
    {
        // Arrange: all steps self-approve and get skipped
        var alexId = Guid.NewGuid();
        var alexEmp = new Employee { Id = alexId, FullName = "Alex" };

        var teamA = MkNode(Guid.NewGuid(), "Team A");

        var steps = new List<WorkflowStepDto>
        {
            new() { StepType = WorkflowStepType.HierarchyLevel, StartFromLevel = 1, LevelsUp = 1, SortOrder = 1 }
        };

        _assignmentRepoMock.Setup(x => x.IsManagerAtNodeAsync(alexId, teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Alex is the only manager
        _orgNodeRepoMock.Setup(x => x.GetByIdAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamA);
        _orgNodeRepoMock.Setup(x => x.GetAncestorsAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgNode>());
        _assignmentRepoMock.Setup(x => x.GetManagersByNodeAsync(teamA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { alexEmp });

        // Act
        var result = await _sut.BuildApprovalChainAsync(alexId, teamA.Id, steps, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty(); // empty = auto-approve
    }
}
