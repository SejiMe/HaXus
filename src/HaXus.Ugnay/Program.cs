using HaXus.Ugnay.Infrastructure.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddKeyPerFile(Environment.GetEnvironmentVariable("HAXUS_APP_SECRETS") ?? "/run/secrets/ugnay", optional: true);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
var connectionString = builder.Configuration.GetConnectionString("haxus");
if (string.IsNullOrWhiteSpace(connectionString))
{
    using var startupLogs = LoggerFactory.Create(logging => logging.AddJsonConsole());
    startupLogs.CreateLogger("Startup").LogCritical("Required configuration is missing: {SettingName}", "ConnectionStrings:haxus");
    throw new InvalidOperationException("Required configuration is missing: ConnectionStrings:haxus");
}
NpgsqlConnectionStringBuilder databaseSettings;
try { databaseSettings = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 3, CommandTimeout = 3 }; }
catch (ArgumentException) { throw new InvalidOperationException("Invalid configuration: ConnectionStrings:haxus"); }
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(databaseSettings.ConnectionString));
builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddCheck<DatabaseReadiness>("application-database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
