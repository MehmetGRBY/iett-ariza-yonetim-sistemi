namespace IettFaultManagement.Api.Services;

/// <summary>Arıza ön değerlendirmesinden hesaplanan kaynak ve devam kararlarını taşır.</summary>
public sealed record FaultInterventionDecision(
    bool TowRequired,
    bool ServiceVehicleRequired,
    bool ReplacementVehicleRequired,
    bool CanContinueRemainingTasks,
    bool? OnSiteRepairPossible);

/// <summary>
/// Araç hareketi ve yerinde müdahale kararına göre çekici, hizmet aracı ve
/// yedek araç ihtiyacını tek bir merkezî karar tablosundan hesaplar.
/// </summary>
public sealed class FaultInterventionPolicy
{
    private static readonly HashSet<string> MobilityValues = ["MOVABLE", "IMMOBILE"];
    private static readonly HashSet<string> RepairValues = ["YES", "NO"];

    public FaultInterventionDecision Decide(
        string mobilityStatus,
        string onSiteRepairDecision,
        bool requestedRemainingTasks,
        bool canCompleteCurrentTrip = true)
    {
        var mobility = mobilityStatus.Trim().ToUpperInvariant();
        var repair = onSiteRepairDecision.Trim().ToUpperInvariant();

        if (!MobilityValues.Contains(mobility))
            throw new ArgumentException("Araç hareket durumu MOVABLE veya IMMOBILE olmalıdır.");
        if (!RepairValues.Contains(repair))
            throw new ArgumentException("Müdahale kararı YES veya NO olmalıdır.");

        var immobile = mobility == "IMMOBILE";
        var onsite = repair == "YES";

        // Kaynak karar tablosu:
        // 1) Hareketsiz + yerinde tamir yok => çekici.
        // 2) Hareketsiz + yerinde tamir => hizmet aracı.
        // 3) Hareket durumundan bağımsız olarak mevcut veya kalan seferlerini
        //    yapamıyorsa görev devri için yeni araç ve yedek sürücü gerekir.
        var tow = immobile && repair == "NO";
        var service = immobile && onsite;
        var replacement = !canCompleteCurrentTrip || !requestedRemainingTasks;
        var canContinue = !replacement && (onsite || (!immobile && requestedRemainingTasks));

        return new FaultInterventionDecision(
            tow,
            service,
            replacement,
            canContinue,
            onsite);
    }

    /// <summary>
    /// Görev dışı araçlarda sefer devri olmadığı için yedek araç üretmez.
    /// Araç zaten garajdaysa dış kaynak göndermek yerine doğrudan teknik ekibi kullanır.
    /// </summary>
    public FaultInterventionDecision DecideForNonTask(
        string operationContext,
        string mobilityStatus,
        string onSiteRepairDecision)
    {
        var context = operationContext.Trim().ToUpperInvariant();
        var allowedContexts = new HashSet<string> { "TEST_DRIVE", "GARAGE_CHECK", "TRANSFER", "PRE_SERVICE_CHECK", "OTHER" };
        if (!allowedContexts.Contains(context))
            throw new ArgumentException("Geçerli görev dışı arıza durumu seçilmelidir.");

        var standard = Decide(mobilityStatus, onSiteRepairDecision, false);
        if (context is "GARAGE_CHECK" or "PRE_SERVICE_CHECK")
            return standard with
            {
                TowRequired = false,
                ServiceVehicleRequired = false,
                ReplacementVehicleRequired = false,
                CanContinueRemainingTasks = false
            };

        return standard with
        {
            ReplacementVehicleRequired = false,
            CanContinueRemainingTasks = false
        };
    }
}
