using System.ComponentModel.DataAnnotations;

namespace IettFaultManagement.Api.Dtos;

// DTO (Data Transfer Object), veritabanı entity'sini doğrudan dışarı açmadan API'nin
// kabul edeceği veya döndüreceği alanları belirler. DataAnnotations otomatik 400 doğrulaması sağlar.

// Liste endpoint'lerinin kayıtlarla birlikte sayfa bilgisini döndürdüğü ortak cevap modeli.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items,int Page,int PageSize,int TotalCount,int TotalPages);
// Kimlik doğrulama istek/cevapları ve oturum açan kullanıcının frontend'e açılan bilgileri.
public sealed record LoginRequest([Required] string PersonnelNumber,[Required] string Password);
public sealed record LoginResponse(string AccessToken,DateTime ExpiresAt,UserResponse User);
public sealed record UserResponse(long Id,string PersonnelNumber,string FullName,string Role,long? GarageId,string? GarageName);
public sealed record ChangePasswordRequest([Required] string CurrentPassword,[Required,MinLength(8)] string NewPassword);
// Admin tarafından sicili oluşturulan personel ilk girişte kendi parolasını belirler.
public sealed record ActivateAccountRequest([Required]string PersonnelNumber,[Required,MinLength(8),MaxLength(100)]string NewPassword,[Required]string ConfirmPassword);
// Oturum açamayan kullanıcı da mevcut parolasını doğrulayarak login ekranından değiştirebilir.
public sealed record PublicChangePasswordRequest([Required]string PersonnelNumber,[Required]string CurrentPassword,[Required,MinLength(8),MaxLength(100)]string NewPassword,[Required]string ConfirmPassword);

// Araç liste ve detay ekranlarının ihtiyaç duyduğu okunabilir cevap modelleri.
public sealed record VehicleListResponse(long Id,string DoorNumber,string Plate,string Brand,string Model,short ModelYear,
    string VehicleType,string FuelType,int CurrentMileage,string Garage,string Status,int? Capacity,bool IsActive);
public sealed record VehicleDetailResponse(VehicleListResponse Vehicle,IReadOnlyList<object> FaultHistory,IReadOnlyList<object> GarageHistory,IReadOnlyList<object> StatusHistory);
public sealed record GarageResponse(long Id,string Code,string Name,int Capacity,long ActiveVehicles,int RemainingCapacity,decimal OccupancyRate,bool IsActive);
// Kapı numarası kurumsal benzersiz kimlik olduğu için güncelleme DTO'suna bilinçli olarak eklenmez.
public sealed record UpdateVehicleRequest(
    [Required,MaxLength(20)]string Plate,
    [Required,MaxLength(80)]string Brand,
    [Required,MaxLength(100)]string Model,
    [Range(1950,2100)]short ModelYear,
    [Required]long? VehicleTypeId,
    [Required]long? FuelTypeId,
    [Range(0,int.MaxValue)]int CurrentMileage,
    [Required]long? GarageId,
    [Required]long? VehicleStatusId,
    [MaxLength(100)]string? DutyType,
    [Range(0,int.MaxValue)]int? Capacity,
    [Required,MaxLength(500)]string ChangeDescription);
public sealed record ChangeVehicleActiveRequest(bool IsActive,[Required,MaxLength(500)]string Reason);
public sealed record UpdateGarageRequest(
    [Required,MaxLength(150)]string Name,
    [MaxLength(500)]string? Address,
    [Range(1,int.MaxValue)]int VehicleCapacity);
public sealed record ChangeGarageActiveRequest(bool IsActive,[Required,MaxLength(500)]string Reason);

// Arıza kaydında merkez yetkilisinden alınan bilgi ve ön değerlendirme cevapları.
public sealed record CreateFaultRequest([Required]string DoorNumber,long? DriverId,[Required]long? FaultCategoryId,
    [Required,MaxLength(3000)]string Description,[Range(0,int.MaxValue)]int MileageAtFailure,
    [Required,MaxLength(500)]string LocationDescription,DateTime OccurredAt,string MobilityStatus="MOVABLE",
    string OnSiteRepairDecision="NO",bool CanCompleteCurrentTrip=false,bool CanContinueRemainingTasks=false,bool DriverCanContinue=true,
    [Required,MaxLength(900)]string AssessmentNote="",[MaxLength(30)]string OperationContext="ACTIVE_TASK",
    long? TechnicianTeamId=null,long? TowTruckId=null,long? ServiceVehicleId=null,long? ReplacementVehicleId=null,
    long? ReplacementDriverId=null);
public sealed record UpdateFaultStatusRequest([Required]long? StatusId,[Required,MaxLength(1000)]string Description);
public sealed record CreateRepairReportRequest([Required]string Result,[Required,MaxLength(4000)]string Description,
    DateTime StartedAt,DateTime CompletedAt,long? RootCauseId,string? SolutionSummary,string? RecurrencePrevention,bool RequiresFollowUp);
public sealed record UpdateResourceStatusRequest([Required]string Status,[Required,MaxLength(1000)]string Description);
public sealed record DispatchTowAfterOnSiteFailureRequest([Required]long? TowTruckId);

// Personel olayı, rapor, sürücü ve kullanıcı yönetim istekleri.
public sealed record PersonnelIncidentRequest([Required]long? DriverId,[Required]string EventType,[Required,MaxLength(2000)]string Description,DateTime OccurredAt);
public sealed record PersonnelReportRequest([Required]DateOnly? ReportStartDate,[Required]DateOnly? ReportEndDate,[MaxLength(100)]string? ReportNumber,[MaxLength(2000)]string? Notes);
public sealed record CreateDriverRequest([Required]long? GarageId,[Required,MaxLength(100)]string FirstName,[Required,MaxLength(100)]string LastName,string GenderCode="MALE",string DriverType="NORMAL");
// Teknisyen eklenirken ekip seçimi istemciden alınmaz; backend uygun ekibi otomatik belirler.
public sealed record CreateTechnicianRequest([Required]long? GarageId,[Required,MaxLength(100)]string FirstName,[Required,MaxLength(100)]string LastName,string GenderCode="MALE");
public sealed record CreateUserRequest([MaxLength(30)]string? PersonnelNumber,[Required,MaxLength(100)]string FirstName,[Required,MaxLength(100)]string LastName,[Required]long? RoleId,long? GarageId,string GenderCode="U");
public sealed record UpdateUserRequest([Required,MaxLength(100)]string FirstName,[Required,MaxLength(100)]string LastName,[Required]long? RoleId,long? GarageId,string GenderCode="U",bool IsActive=true);
public sealed record ResetUserPasswordRequest([Required,MinLength(8),MaxLength(100)]string NewPassword);
// Karar destek modülünde bilgi makalesi, araç kontrolü ve operasyon olayı istekleri.
public sealed record CreateSolutionArticleRequest([Required]long? FaultCategoryId,long? RootCauseId,long? SourceRepairReportId,[Required,MaxLength(200)]string Title,[Required,MaxLength(1500)]string Symptoms,[Required]string SolutionSteps,string? SafetyNotes,int? EstimatedMinutes);
public sealed record CreateInspectionRequest([Required]long? VehicleId,long? FaultId,[Required]string InspectionType,[Required]string Result,int? Odometer,string? Notes,string? NextAction);
public sealed record CreateOperationalEventRequest([Required]string EventType,[Required,MaxLength(200)]string Title,[Required,MaxLength(2000)]string Description,long? GarageId,long? RouteId,DateTime StartsAt,DateTime? EndsAt);
public sealed record UpdateOperationalEventRequest([Required]string EventType,[Required,MaxLength(200)]string Title,[Required,MaxLength(2000)]string Description,long? GarageId,long? RouteId,DateTime StartsAt,DateTime? EndsAt,[Required]string Status);
// Sistem ayarı anahtarı değiştirilemez; admin yalnızca JSON değerini, açıklamayı ve aktiflik durumunu günceller.
public sealed record UpdateSystemSettingRequest([Required]string SettingValue,[MaxLength(500)]string? Description,bool IsActive);
