using System.Text;
using GSTP.PaymentService.Data;
using GSTP.PaymentService.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "GlobalStockTradingPlatformSuperSecretKey2026!@#$";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true, ValidIssuer = "gstp-auth-service",
            ValidateAudience = true, ValidAudience = "gstp-api-gateway",
            ValidateLifetime = true
        };
    });

builder.Services.AddSingleton<PaymentKafkaProducer>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "GSTP Payment Service", status = "running" }));
app.Run();
