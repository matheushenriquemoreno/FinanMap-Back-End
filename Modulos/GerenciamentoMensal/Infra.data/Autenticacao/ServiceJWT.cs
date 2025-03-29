using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Login.Interfaces;
using Domain.Entity;
using Microsoft.IdentityModel.Tokens;

namespace Infra.Autenticacao;

public class ServiceJWT : IServiceJWT
{
    public string CriarToken(Usuario user)
    {
        var keyByte = Encoding.ASCII.GetBytes(JWTModel.SecretKey);

        var key = new SymmetricSecurityKey(keyByte);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var claims = new List<Claim>
    {
        new Claim(nameof(Usuario.Id), user.Id),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Name, user.Nome),
    };

        var token = new JwtSecurityToken(
            issuer: JWTModel.Issuer,
            audience: JWTModel.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
