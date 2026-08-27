using System.Security.Claims;

namespace IettFaultManagement.Api.Extensions;
/// <summary>
/// JWT içindeki sık kullanılan kullanıcı ve garaj bilgilerini controller'larda
/// tekrar tekrar ayrıştırmadan güvenli biçimde okumayı sağlar.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static long UserId(this ClaimsPrincipal user)=>long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static long? GarageId(this ClaimsPrincipal user)=>long.TryParse(user.FindFirstValue("garageId"),out var id)?id:null;
}
