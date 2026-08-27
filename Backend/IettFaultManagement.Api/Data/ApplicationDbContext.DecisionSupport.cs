using IettFaultManagement.Api.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Data;

/// <summary>
/// Scaffold edilen DbContext'i karar destek tabloları ve raporlama view'larıyla genişletir.
/// Partial class dosyaları derleme sırasında tek ApplicationDbContext olarak birleşir.
/// </summary>
public partial class ApplicationDbContext
{
    public DbSet<RootCause> RootCauses => Set<RootCause>();
    public DbSet<SolutionArticle> SolutionArticles => Set<SolutionArticle>();
    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();
    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();
    public DbSet<VwFaultSlaStatus> VwFaultSlaStatuses => Set<VwFaultSlaStatus>();
    public DbSet<VwVehicleHealthScore> VwVehicleHealthScores => Set<VwVehicleHealthScore>();
    public DbSet<VwRecurringVehicleFault> VwRecurringVehicleFaults => Set<VwRecurringVehicleFault>();
}
