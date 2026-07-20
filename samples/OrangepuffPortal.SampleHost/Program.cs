using Diagnostics.AspNetCore.DependencyInjection;
using Diagnostics.NLog.DependencyInjection;
using OrangepuffPortal.Host;

var builder = WebApplication.CreateBuilder(args);

// Diagnostics logging — required by OrangepuffPortal.Identity's command handlers
// (they take a hard ITransactionLogger dependency). Not wired automatically by
// AddOrangepuffPortal since the connection string is deployment-specific.
builder.Services.AddDiagnostics(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DiagnosticLogs")
        ?? throw new InvalidOperationException("ConnectionStrings:DiagnosticLogs is not configured.");
    options.LoggerName = builder.Configuration["Diagnostics:LoggerName"] ?? "OrangepuffPortal.SampleHost";
    options.EnvironmentName = builder.Configuration["Diagnostics:EnvironmentName"] ?? "DEV";
});
builder.Services.AddDiagnosticsAspNetCore();

builder.Services.AddOrangepuffPortal(builder.Configuration);

var app = builder.Build();

await app.MigratePortalModulesAsync();

// Authentication must run before UseDiagnostics(): its transaction middleware auto-stamps the
// current user (RequestContextTransactionLogger -> CurrentUser.UserId), which throws instead of
// falling back to a hardcoded id — HttpContext.User has to already be populated by the time it runs.
app.UseAuthentication();
app.UseAuthorization();
app.UseDiagnostics();

app.MapOrangepuffPortal();

app.Run();
