using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IettFaultManagement.Api.Middleware;

/// <summary>
/// Uygulama boyunca yakalanmamış istisnaları merkezi olarak loglar ve frontend'in
/// anlayabileceği standart RFC 7807 ProblemDetails cevabına dönüştürür.
/// </summary>
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "İşlenmeyen API hatası. TraceId: {TraceId}", context.TraceIdentifier);
            // PostgreSQL hata kodları kullanılarak teknik veritabanı ayrıntılarını
            // dışarı sızdırmadan uygun HTTP durum kodu belirlenir.
            var (status, title, detail) = exception switch
            {
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
                    (StatusCodes.Status409Conflict, "Kayıt çakışması", "Aynı benzersiz bilgiye sahip başka bir kayıt bulunuyor."),
                DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } =>
                    (StatusCodes.Status409Conflict, "Bağlı kayıt hatası", "Bağlı kayıtlar nedeniyle işlem tamamlanamadı."),
                DbUpdateConcurrencyException =>
                    (StatusCodes.Status409Conflict, "Eş zamanlı güncelleme", "Kayıt başka bir işlem tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin."),
                ArgumentException =>
                    (StatusCodes.Status400BadRequest, "Geçersiz istek", exception.Message),
                InvalidOperationException =>
                    (StatusCodes.Status409Conflict, "İşlem uygulanamadı", exception.Message),
                KeyNotFoundException =>
                    (StatusCodes.Status404NotFound, "Kayıt bulunamadı", exception.Message),
                _ =>
                    (StatusCodes.Status500InternalServerError, "Sunucu hatası", "İşlem tamamlanamadı.")
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{status}",
                Title = title,
                Status = status,
                Detail = detail,
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            });
        }
    }
}
