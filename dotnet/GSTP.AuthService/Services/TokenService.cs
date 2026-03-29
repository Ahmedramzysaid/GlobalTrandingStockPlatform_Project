using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GSTP.AuthService.Models;
using Microsoft.IdentityModel.Tokens;

namespace GSTP.AuthService.Services;

public interface ITokenService
{
    string GenerateAccessToken(UserAuth user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(UserAuth user)
    {
        var key = _configuration["Jwt:SecretKey"] ?? "GlobalStockTradingPlatformSuperSecretKey2026!@#$";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "gstp-auth-service",
            audience: _configuration["Jwt:Audience"] ?? "gstp-api-gateway",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = _configuration["Jwt:SecretKey"] ?? "GlobalStockTradingPlatformSuperSecretKey2026!@#$";
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "gstp-auth-service",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "gstp-api-gateway",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
