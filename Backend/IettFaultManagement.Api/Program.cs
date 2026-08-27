using System.Text;
using IettFaultManagement.Api.Data;
using IettFaultManagement.Api.Middleware;
using IettFaultManagement.Api.Models.Database;
using IettFaultManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Uygulamanın logları yalnızca konsola ve Visual Studio Debug penceresine yazılır.
// Varsayılan provider'ları temizlemek, aynı logun birden fazla kez yazılmasını önler.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Controller tabanlı REST API desteğini Dependency Injection konteynerine ekler.
// Model doğrulama başarısız olduğunda tüm endpoint'ler aynı ProblemDetails biçimini döndürür.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(e =>
                        string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? "Geçersiz değer."
                            : e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Title = "Doğrulama hatası",
                Detail = "Gönderilen bilgiler kontrol edilmelidir.",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            });
        };
    });
// Endpoint metadata'sı Swagger/OpenAPI belgesi tarafından okunabilir hale gelir.
builder.Services.AddEndpointsApiExplorer();
// Swagger, geliştirme sırasında endpoint'leri tarayıcıdan incelemek ve denemek için kullanılır.
// Bearer tanımı sayesinde Swagger arayüzüne JWT eklenebilir.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "İETT Arıza Yönetim API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "JWT access token"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});
// Audit interceptor'ın işlemi yapan kullanıcıya ve IP adresine ulaşabilmesini sağlar.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
// PostgreSQL bağlantısını EF Core DbContext'e tanımlar.
// SaveChanges sırasında değişikliklerin audit_logs tablosuna yazılması için interceptor eklenir.
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()));
// Parolaları düz metin tutmak yerine güvenli ve tuzlanmış hash olarak saklar.
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<FaultAutomationProcessor>();
builder.Services.AddScoped<FaultResourceAssignmentService>();
builder.Services.AddScoped<FaultLifecycleService>();
builder.Services.AddScoped<AppNotificationService>();
builder.Services.AddScoped<AttachmentStorageService>();
builder.Services.AddScoped<PageAccessService>();
builder.Services.AddSingleton<FaultInterventionPolicy>();
// Kullanıcı isteğinden bağımsız, belirli aralıklarla çalışan arka plan servisleri.
// Arıza akışı, SLA, personel raporu ve görev planı gibi süreçleri otomatik tutarlar.
builder.Services.AddHostedService<FaultAutomationWorker>();
builder.Services.AddHostedService<FaultTeamQueueWorker>();
builder.Services.AddHostedService<FaultMonitoringWorker>();
builder.Services.AddHostedService<PersonnelIncidentAutomationService>();
builder.Services.AddHostedService<TaskStatusSynchronizationService>();
builder.Services.AddHostedService<RollingTaskPlanningService>();
// Bitiş zamanı geçen açık operasyon olaylarını düzenli olarak kapatır.
builder.Services.AddHostedService<OperationalEventExpirationWorker>();
// Uygulama bildirimleriyle birlikte oluşan e-posta outbox kayıtlarını SMTP'ye iletir.
// Email:Enabled=false iken kuyruk korunur, dışarıya e-posta gönderilmez.
builder.Services.AddHostedService<EmailDeliveryWorker>();

// JWT imza anahtarı kaynak koda yazılmaz. Yapılandırmada bulunmazsa uygulama güvensiz
// bir anahtarla başlamak yerine bilinçli olarak durdurulur.
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key yapılandırılmalıdır.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromSeconds(30)
    };
    // Bir token matematiksel olarak geçerli olsa bile kullanıcı pasife alınmış,
    // kilitlenmiş veya parolası değiştirilmiş olabilir. SecurityStamp kontrolü eski token'ı iptal eder.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdText = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var securityStamp = context.Principal?.FindFirst("securityStamp")?.Value;
            if (!long.TryParse(userIdText, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
            {
                context.Fail("Geçersiz erişim anahtarı.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var user = await db.AppUsers.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new { x.IsActive, x.LockedUntil, x.SecurityStamp })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow ||
                !string.Equals(user.SecurityStamp.ToString(), securityStamp, StringComparison.OrdinalIgnoreCase))
                context.Fail("Kullanıcı oturumu artık geçerli değil.");
        }
    };
});
builder.Services.AddAuthorization();
// Ayrı frontend projesinin tarayıcıdan API'ye erişmesine izin verilen adresleri sınırlar.
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5500"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks();

// Bu noktaya kadar servisler kaydedildi; bundan sonra HTTP middleware hattı oluşturulur.
var app = builder.Build();

// Yakalanmamış hataları standart ProblemDetails cevabına çevirir.
app.UseMiddleware<ApiExceptionMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
// Yerel frontend HTTP (localhost:5173) üzerinden çalıştığı için geliştirme
// ortamında HTTPS yönlendirmesi yapılmaz. Canlı ortamda trafik yine HTTPS'e zorlanır.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Frontend");
// Sıra önemlidir: önce token'dan kullanıcı bulunur, sonra rol/garaj yetkisi kontrol edilir.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Sunucunun ayakta olup olmadığını izleme araçlarına bildiren hafif endpoint.
app.MapHealthChecks("/health");
app.Run();

public partial class Program;
