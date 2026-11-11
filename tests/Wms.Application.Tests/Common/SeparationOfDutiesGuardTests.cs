using FluentAssertions;
using Wms.Application.Common.Guards;
using Xunit;

namespace Wms.Application.Tests.Common;

public class SeparationOfDutiesGuardTests
{
    [Fact]
    public void EnsureCanApprove_WhenApproverIsCreator_ShouldFail()
    {
        var creatorId = Guid.NewGuid();
        var approverId = creatorId;

        var result = SeparationOfDutiesGuard.EnsureCanApprove(creatorId, approverId, "PO-001");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));
    }

    [Fact]
    public void EnsureCanApprove_WhenApproverIsDifferentUser_ShouldSucceed()
    {
        var creatorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var result = SeparationOfDutiesGuard.EnsureCanApprove(creatorId, approverId, "PO-001");

        result.Succeeded.Should().BeTrue();
    }
}

