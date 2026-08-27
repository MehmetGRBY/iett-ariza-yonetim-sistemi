using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IettFaultManagement.Api.Models.Database;
using Microsoft.IdentityModel.Tokens;

namespace IettFaultManagement.Api.Services;

/// <summary>
/// Giriş yapan kullanıcı için kimlik, rol, garaj ve güvenlik damgası claim'lerini
/// içeren, süreli ve kriptografik olarak imzalanmış JWT access token üretir.
/// </summary>
public sealed class JwtTokenService(IConfiguration configuration)
{
    public (string Token, DateTime ExpiresAt) Create(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 60));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,user.Id.ToString()), new(ClaimTypes.Name,user.FirstName+" "+user.LastName),
            new(ClaimTypes.Role,user.Role.Name), new("roleId",user.RoleId.ToString()),
            new("personnelNumber",user.PersonnelNumber),
            new("securityStamp", user.SecurityStamp.ToString())
        };
        if(user.GarageId.HasValue) claims.Add(new Claim("garageId",user.GarageId.Value.ToString()));
        var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var token=new JwtSecurityToken(configuration["Jwt:Issuer"],configuration["Jwt:Audience"],claims,
            expires:expires,signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token),expires);
    }
}
