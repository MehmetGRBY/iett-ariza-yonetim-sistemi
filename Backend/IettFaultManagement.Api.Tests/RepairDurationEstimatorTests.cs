using IettFaultManagement.Api.Services;

namespace IettFaultManagement.Api.Tests;

/// <summary>Tamir süresi ortalama/medyan hesaplarının tek ve çift örneklerde doğru olduğunu test eder.</summary>
public sealed class RepairDurationEstimatorTests
{
    [Fact]
    public void Calculate_ReturnsAverageAndMedian()
    {
        var result = RepairDurationEstimator.Calculate([30, 60, 90, 120]);
        Assert.Equal(4, result.SampleCount);
        Assert.Equal(75, result.AverageMinutes);
        Assert.Equal(75, result.MedianMinutes);
    }

    [Fact]
    public void Calculate_IgnoresInvalidDurations()
    {
        var result = RepairDurationEstimator.Calculate([-1, 0, 60, 20000]);
        Assert.Equal(1, result.SampleCount);
        Assert.Equal(60, result.AverageMinutes);
        Assert.Equal(60, result.MedianMinutes);
    }

    [Fact]
    public void Calculate_HandlesNoUsableHistory()
    {
        var result = RepairDurationEstimator.Calculate([]);
        Assert.Equal(0, result.SampleCount);
        Assert.Null(result.AverageMinutes);
        Assert.Null(result.MedianMinutes);
    }
}
