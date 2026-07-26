using OrangepuffPortal.ConfigText.Contract.Interfaces;
using OrangepuffPortal.Shared.Modules;

namespace OrangepuffPortal.Host.ConfigText;

/// <summary>
/// Seeds the portal shell's own default text (see <see cref="PortalShellTextSeed"/>) into
/// [configtext].[ConfigTextDefinition] at every consuming app's startup, insert-only unless a seed
/// entry sets <c>BtReplace</c> — same semantics as <see cref="IConfigTextWriter"/> always has. Runs
/// automatically through <c>MigratePortalModulesAsync()</c>, so no consuming app needs its own call
/// site for this (unlike a consuming app's own module text, which is each app's own responsibility).
/// </summary>
internal sealed class PortalShellTextPortalModule(IConfigTextWriter writer) : IPortalModule
{
    private const string CultureCode = "en-US";

    public string Name => "PortalShellText";

    public Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) =>
        writer.UpsertManyAsync(CultureCode, PortalShellTextSeed.Load(CultureCode), cancellationToken);
}
