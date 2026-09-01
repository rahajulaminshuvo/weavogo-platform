using WeavoGo.Master.Api.Domain;
using Xunit;

namespace WeavoGo.Master.Tests;

/// <summary>SDS §8.1 — the item lifecycle state machine, in isolation from the database.</summary>
public class ItemLifecycleTests
{
    [Theory]
    [InlineData(ItemStatuses.Draft, ItemStatuses.PendingApproval)]
    [InlineData(ItemStatuses.PendingApproval, ItemStatuses.Active)]
    [InlineData(ItemStatuses.PendingApproval, ItemStatuses.Draft)]   // rejection returns to Draft
    [InlineData(ItemStatuses.Active, ItemStatuses.Inactive)]
    [InlineData(ItemStatuses.Active, ItemStatuses.Obsolete)]
    [InlineData(ItemStatuses.Active, ItemStatuses.PendingApproval)]  // §10.6 re-approval of an edit
    [InlineData(ItemStatuses.Inactive, ItemStatuses.Active)]
    [InlineData(ItemStatuses.Inactive, ItemStatuses.Obsolete)]
    public void PermittedTransitions_AreAllowed(string from, string to)
        => Assert.True(ItemStatuses.CanTransition(from, to));

    [Theory]
    [InlineData(ItemStatuses.Draft, ItemStatuses.Active)]        // no skipping PendingApproval
    [InlineData(ItemStatuses.Draft, ItemStatuses.Obsolete)]
    [InlineData(ItemStatuses.Draft, ItemStatuses.Inactive)]
    [InlineData(ItemStatuses.Obsolete, ItemStatuses.Active)]     // Obsolete is terminal
    [InlineData(ItemStatuses.Obsolete, ItemStatuses.Inactive)]
    [InlineData(ItemStatuses.Inactive, ItemStatuses.PendingApproval)]
    [InlineData(ItemStatuses.PendingApproval, ItemStatuses.Obsolete)]
    public void ForbiddenTransitions_AreRejected(string from, string to)
        => Assert.False(ItemStatuses.CanTransition(from, to));

    [Fact]
    public void NewItem_StartsAsDraft()
        => Assert.Equal(ItemStatuses.Draft, new ItemMaster().ItemStatus);

    [Fact]
    public void NewItem_StartsAtVersionOne()
        => Assert.Equal(1, new ItemMaster().VersionNumber);

    [Fact]
    public void ClearValues_EmptiesEveryValueColumn()
    {
        var attribute = new ItemAttribute
        {
            ValueText = "x",
            ValueNumber = 1,
            ValueDate = new DateOnly(2026, 1, 1),
            ValueBoolean = true
        };

        attribute.ClearValues();

        Assert.Null(attribute.ValueText);
        Assert.Null(attribute.ValueNumber);
        Assert.Null(attribute.ValueDate);
        Assert.Null(attribute.ValueBoolean);
    }
}
