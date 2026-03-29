using GSTP.AuthService.Data;
using GSTP.AuthService.Models;
using GSTP.AuthService.Models.DTOs;
using GSTP.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace GSTP.AuthService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthDbContext db, ITokenService tokenService, ILogger<AuthController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>Register a new user</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = ModelState });

        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { success = false, error = new { code = "EMAIL_EXISTS", message = "Email already registered" } });

        var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, salt);

        var user = new UserAuth
        {
            Email = dto.Email.ToLower().Trim(),
            PasswordHash = hash,
            Salt = salt,
            Role = "User"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email} (ID: {UserId})", user.Email, user.Id);

        return Created("", new
        {
            success = true,
            data = new
            {
                userId = user.Id,
                email = user.Email,
                message = "Registration successful"
            }
        });
    }

    /// <summary>Login and get JWT tokens</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower().Trim());

        if (user == null)
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password" } });

        if (user.IsLocked && user.LockoutUntil > DateTime.UtcNow)
            return StatusCode(423, new { success = false, error = new { code = "ACCOUNT_LOCKED", message = "Account is locked. Try again later." } });

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5)
            {
                user.IsLocked = true;
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(30);
            }
            await _db.SaveChangesAsync();
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password" } });
        }

        // Reset failed attempts on successful login
        user.FailedAttempts = 0;
        user.IsLocked = false;
        user.LockoutUntil = null;

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            DeviceInfo = Request.Headers["User-Agent"].FirstOrDefault(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("User logged in: {Email}", user.Email);

        return Ok(new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer",
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role
            }
        });
    }

    /// <summary>Refresh access token</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken && !t.Revoked);

        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid or expired refresh token" } });

        // Revoke old token
        storedToken.Revoked = true;

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(storedToken.User);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        return Ok(new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer",
            User = new UserInfoDto
            {
                Id = storedToken.User.Id,
                Email = storedToken.User.Email,
                Role = storedToken.User.Role
            }
        });
    }

    /// <summary>Logout - revoke tokens</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == Guid.Parse(userId) && !t.Revoked)
            .ToListAsync();

        foreach (var token in tokens)
            token.Revoked = true;

        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Logged out successfully" });
    }

    /// <summary>Get current user info</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return NotFound();

        return Ok(new
        {
            success = true,
            data = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role
            }
        });
    }
}
