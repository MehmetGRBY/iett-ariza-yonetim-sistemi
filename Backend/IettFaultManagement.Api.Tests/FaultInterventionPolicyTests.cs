using IettFaultManagement.Api.Services;

namespace IettFaultManagement.Api.Tests;

/// <summary>
/// Hareket ve yerinde müdahale kombinasyonlarının çelişkili kaynak kararı üretmediğini
/// doğrulayan birim testleri.
/// </summary>
public sealed class FaultInterventionPolicyTests
{
    private readonly FaultInterventionPolicy policy = new();

    [Theory]
    [InlineData("IMMOBILE", "YES", true,  true,  false, true,  false)]
    [InlineData("IMMOBILE", "YES", false, true,  false, true,  true)]
    [InlineData("IMMOBILE", "NO",  true,  true,  true,  false, false)]
    [InlineData("IMMOBILE", "NO",  true,  false, true,  false, true)]
    [InlineData("MOVABLE",  "YES", true,  true,  false, false, false)]
    [InlineData("MOVABLE",  "YES", false, true,  false, false, true)]
    [InlineData("MOVABLE",  "NO",  true,  true,  false, false, false)]
    [InlineData("MOVABLE",  "NO",  true,  false, false, false, true)]
    public void AllSupportedDecisionCombinations_ProduceExpectedResources(
        string mobility,
        string onsite,
        bool currentTrip,
        bool remainingTasks,
        bool expectedTow,
        bool expectedService,
        bool expectedReplacement)
    {
        var result = policy.Decide(mobility, onsite, remainingTasks, currentTrip);

        Assert.Equal(expectedTow, result.TowRequired);
        Assert.Equal(expectedService, result.ServiceVehicleRequired);
        Assert.Equal(expectedReplacement, result.ReplacementVehicleRequired);
        Assert.False(result.TowRequired && result.ServiceVehicleRequired);
    }

    [Fact]
    public void ImmobileAndOnsiteRepair_AssignsServiceAndReplacementForInterruptedTasks()
    {
        var result = policy.Decide("IMMOBILE", "YES", false);

        Assert.False(result.TowRequired);
        Assert.True(result.ServiceVehicleRequired);
        Assert.True(result.ReplacementVehicleRequired);
        Assert.False(result.CanContinueRemainingTasks);
        Assert.True(result.OnSiteRepairPossible);
    }

    [Fact]
    public void ImmobileAndNoOnsiteRepair_AssignsTowAndReplacementForInterruptedTasks()
    {
        var result = policy.Decide("IMMOBILE", "NO", false);

        Assert.True(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.True(result.ReplacementVehicleRequired);
        Assert.False(result.CanContinueRemainingTasks);
        Assert.False(result.OnSiteRepairPossible);
    }

    [Fact]
    public void MovableVehicle_DoesNotDispatchServiceForOnsiteDecision()
    {
        var result = policy.Decide("MOVABLE", "YES", true);

        Assert.False(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.False(result.ReplacementVehicleRequired);
        Assert.True(result.CanContinueRemainingTasks);
        Assert.True(result.OnSiteRepairPossible);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void MovableVehicleUnableToFinishTrips_AssignsReplacementVehicle(
        bool canCompleteCurrentTrip,
        bool canCompleteRemainingTasks)
    {
        var result = policy.Decide(
            "MOVABLE",
            "NO",
            canCompleteRemainingTasks,
            canCompleteCurrentTrip);

        Assert.False(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.True(result.ReplacementVehicleRequired);
        Assert.False(result.CanContinueRemainingTasks);
    }

    [Fact]
    public void MovableVehicleCompletingAllTrips_DoesNotAssignResource()
    {
        var result = policy.Decide("MOVABLE", "NO", true, true);

        Assert.False(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.False(result.ReplacementVehicleRequired);
        Assert.True(result.CanContinueRemainingTasks);
    }

    [Theory]
    [InlineData("YES", false, true)]
    [InlineData("NO", true, false)]
    public void ImmobileVehicleCompletingAllTrips_DoesNotAssignReplacementVehicle(
        string onsiteDecision,
        bool expectsTow,
        bool expectsService)
    {
        var result = policy.Decide("IMMOBILE", onsiteDecision, true, true);

        Assert.Equal(expectsTow, result.TowRequired);
        Assert.Equal(expectsService, result.ServiceVehicleRequired);
        Assert.False(result.ReplacementVehicleRequired);
    }

    [Theory]
    [InlineData("LIMITED", "YES")]
    [InlineData("MOVABLE", "MAYBE")]
    [InlineData("MOVABLE", "UNKNOWN")]
    public void InvalidDecisionValue_IsRejected(string mobility, string repair)
    {
        Assert.Throws<ArgumentException>(() => policy.Decide(mobility, repair, false));
    }

    [Theory]
    [InlineData("GARAGE_CHECK")]
    [InlineData("PRE_SERVICE_CHECK")]
    public void VehicleAlreadyAtGarage_DoesNotDispatchExternalResources(string context)
    {
        var result = policy.DecideForNonTask(context, "IMMOBILE", "NO");

        Assert.False(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.False(result.ReplacementVehicleRequired);
        Assert.False(result.CanContinueRemainingTasks);
    }

    [Fact]
    public void TestDriveBreakdown_CanDispatchTowWithoutReplacementVehicle()
    {
        var result = policy.DecideForNonTask("TEST_DRIVE", "IMMOBILE", "NO");

        Assert.True(result.TowRequired);
        Assert.False(result.ServiceVehicleRequired);
        Assert.False(result.ReplacementVehicleRequired);
        Assert.False(result.CanContinueRemainingTasks);
    }

    [Fact]
    public void NonTaskContext_InvalidValueIsRejected()
    {
        Assert.Throws<ArgumentException>(() => policy.DecideForNonTask("ACTIVE_TASK", "MOVABLE", "YES"));
    }
}
