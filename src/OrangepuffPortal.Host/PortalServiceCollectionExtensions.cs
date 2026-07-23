using Diagnostics.Abstractions.Interfaces;
using Diagnostics.NLog.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrangepuffPortal.Bff;
using OrangepuffPortal.Host.Infrastructure;
using OrangepuffPortal.Identity.Infrastructure;
using OrangepuffPortal.Shared.Auditing;

namespace OrangepuffPortal.Host
{
    /// <summary>
    /// Umbrella registration for everything OrangepuffPortal owns: the real <see cref="ICurrentUser"/>, auto-stamped transaction logging, the Identity module, and Bff-owned auth.
    /// Requires the host to have already called AddDiagnostics()/AddDiagnosticsAspNetCore() itself (Identity's command handlers take a hard dependency on <see cref="ITransactionLogger"/>).
    /// </summary>
    public static class PortalServiceCollectionExtensions
    {
        public static IServiceCollection AddOrangepuffPortal(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            // Auto-stamp every transaction span with the current request's user (overrides the base registration from AddDiagnostics; scoped because ICurrentUser is scoped).
            services.AddScoped<ITransactionLogger>(sp => new RequestContextTransactionLogger(
                sp.GetRequiredService<TransactionLoggerImplementation>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IHttpContextAccessor>()));

            services.AddIdentityModule(configuration);
            services.AddPortalBff(configuration);

            return services;
        }
    }
}
