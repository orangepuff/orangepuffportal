namespace OrangepuffPortal.Config.Contract;

/// <summary>
/// One consuming app's default config, nested under the section it belongs to in that app's own
/// source-controlled seed file. Field names mirror the DB column names deliberately — this is a
/// wire/seed contract, not a domain model.
/// </summary>
/// <param name="SConfigCode">Technical lookup key, unique across every module, e.g. "OCRWeb.MaxUploadSizeMb".</param>
/// <param name="SConfigName">Plain fallback display name, always present.</param>
/// <param name="STextCode">Pointer into ConfigTextDefinition.sTextCode for the localized label.</param>
/// <param name="IConfigType">See <see cref="ConfigValueType"/>.</param>
/// <param name="BtShow">Whether this config is shown in a settings UI.</param>
/// <param name="BtAllowUserEdit">Whether a user may self-service edit this config's value.</param>
/// <param name="BtReplace">
/// Seed-file-only override: when true, an existing row (matched by SConfigCode) is updated instead
/// of skipped, including which section it belongs to. Never persisted as a DB column.
/// </param>
public sealed record ConfigSeedEntry(
    string SConfigCode,
    string SConfigName,
    string STextCode,
    int IConfigType,
    bool BtShow = true,
    bool BtAllowUserEdit = false,
    bool BtReplace = false);
