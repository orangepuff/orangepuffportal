using Diagnostics.Abstractions.Interfaces;
using Diagnostics.NLog.Transactions;
using MediatR;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrangepuffPortal.Bff;
using OrangepuffPortal.Config.Infrastructure;
using OrangepuffPortal.ConfigText.Infrastructure;
using OrangepuffPortal.Host.ConfigText;
using OrangepuffPortal.Host.Infrastructure;
using OrangepuffPortal.Identity.Infrastructure;
using OrangepuffPortal.Shared.Auditing;
using OrangepuffPortal.Shared.Modules;

namespace OrangepuffPortal.Host
{
    /// <summary>
    /// Umbrella registration for everything OrangepuffPortal owns: the real <see cref="ICurrentUser"/>, auto-stamped transaction logging, the Identity module, the ConfigText module, the Config module, and Bff-owned auth.
    /// Requires the host to have already called AddDiagnostics()/AddDiagnosticsAspNetCore() itself (Identity's command handlers take a hard dependency on <see cref="ITransactionLogger"/>).
    /// </summary>
    public static class PortalServiceCollectionExtensions
    {
        public static IServiceCollection AddOrangepuffPortal(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<IUserDirectory, UserDirectory>();

            // Distributed cache: Redis when ConnectionStrings:Redis is provided, otherwise an
            // in-process fallback so development environments without Redis work transparently.
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = "portal:";
                });
            }
            else
            {
                services.AddDistributedMemoryCache();
            }

            // Auto-stamp every transaction span with the current request's user (overrides the base registration from AddDiagnostics; scoped because ICurrentUser is scoped).
            services.AddScoped<ITransactionLogger>(sp => new RequestContextTransactionLogger(
                sp.GetRequiredService<TransactionLoggerImplementation>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IHttpContextAccessor>()));

            services.AddIdentityModule(configuration);
            services.AddConfigTextModule(configuration);
            services.AddConfigModule(configuration);
            services.AddPortalBff(configuration);

            // Runs through the same MigratePortalModulesAsync() pipeline as every other IPortalModule —
            // seeds the portal shell's own default text (see ConfigText/PortalShellTextPortalModule.cs)
            // automatically at every consuming app's startup, with no extra call site required there.
            services.AddSingleton<IPortalModule, PortalShellTextPortalModule>();

            // Registered once, here, for every module's assembly together — Identity publishes
            // UserCreatedNotification (OrangepuffPortal.Shared.Events) and Config subscribes to it, so a
            // single IMediator instance has to know about both assemblies' handlers.
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
                typeof(OrangepuffPortal.Identity.Infrastructure.ModuleRegistration).Assembly,
                typeof(OrangepuffPortal.Config.Infrastructure.ModuleRegistration).Assembly));

            return services;
        }
    }
}
