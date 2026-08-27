namespace IettFaultManagement.Api.Services;

/// <summary>Benzer geçmiş tamirlerden hesaplanan örnek sayısı, ortalama ve medyan süreyi taşır.</summary>
public sealed record RepairDurationStatistics(int SampleCount, int? AverageMinutes, int? MedianMinutes);

/// <summary>
/// Uç değerlerin etkisini azaltmak için geçerli tamir sürelerinden istatistik üretir.
/// Bu sonuç operasyon planlamasında kullanılan veri temelli süre tahminine temel olur.
/// </summary>
public static class RepairDurationEstimator
{
    public static RepairDurationStatistics Calculate(IEnumerable<int> source)
    {
        var values = source.Where(x => x is > 0 and < 10080).Order().ToArray();
        if (values.Length == 0) return new(0, null, null);
        var average = (int)Math.Round(values.Average());
        var median = values.Length % 2 == 1
            ? values[values.Length / 2]
            : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
        return new(values.Length, average, median);
    }
}
