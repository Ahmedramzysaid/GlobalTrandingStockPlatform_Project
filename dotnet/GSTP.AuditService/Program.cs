using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using GSTP.AuditService.Data;
using GSTP.AuditService.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<AuditDbContext>(options =>
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

builder.Services.AddHostedService<AuditKafkaConsumer>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();
using (var scope = app.Services.CreateScope()) { scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.EnsureCreated(); }

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "GSTP Audit Service", status = "running" }));
app.Run();

// --- Kafka Consumer (subscribes to ALL business events) ---
public class AuditKafkaConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditKafkaConsumer> _logger;

    public AuditKafkaConsumer(IServiceProvider sp, IConfiguration config, ILogger<AuditKafkaConsumer> logger)
    { _sp = sp; _config = config; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(5000, ct);
        var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "audit-service",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(new[] { "orders.created", "orders.cancelled", "trades.executed", "payments.completed", "risk.alerts", "users.events", "settlement.events" });
        _logger.LogInformation("Audit Service listening to all business event topics");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

                var auditLog = new AuditLog
                {
                    EventType = result.Topic,
                    Action = result.Topic,
                    SourceService = "kafka",
                    CorrelationId = result.Message.Key,
                    NewValues = result.Message.Value,
                    CreatedAt = DateTime.UtcNow
                };

                // Try to extract user ID from event
                try
                {
                    var json = JsonDocument.Parse(result.Message.Value);
                    if (json.RootElement.TryGetProperty("Data", out var data) && data.TryGetProperty("UserId", out var uid))
                        auditLog.UserId = Guid.Parse(uid.GetString()!);
                }
                catch { /* Non-critical */ }

                db.AuditLogs.Add(auditLog);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Audit log recorded: {Topic}", result.Topic);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Audit consumer error"); }
        }
        consumer.Dispose();
    }
}
