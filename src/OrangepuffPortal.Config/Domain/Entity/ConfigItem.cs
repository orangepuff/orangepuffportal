namespace OrangepuffPortal.Config.Domain.Entity;

/// <summary>
/// A single configurable setting within a <see cref="ConfigSection"/>. Maps to [config].[Configs] —
/// named <c>ConfigItem</c> rather than <c>Config</c> to avoid colliding with this project's own root
/// namespace, <c>OrangepuffPortal.Config</c>.
/// </summary>
/// <remarks>
/// Does not use the shared portal <c>AuditableEntity</c>, for the same reason as
/// <see cref="ConfigSection"/> — seeded by unattended startup code, no signed-in user available.
/// </remarks>
public class ConfigItem
{
    public int Id { get; private set; }
    public int SectionId { get; private set; }
    public string ConfigCode { get; private set; } = string.Empty;
    public string ConfigName { get; private set; } = string.Empty;
    public string TextCode { get; private set; } = string.Empty;
    public int ConfigType { get; private set; }
    public bool Show { get; private set; } = true;
    public bool AllowUserEdit { get; private set; }
    public int? InsertedUserId { get; private set; }
    public DateTime? InsertedTime { get; private set; }
    public int? UpdatedUserId { get; private set; }
    public DateTime? UpdatedTime { get; private set; }

    private ConfigItem() { } // EF

    public ConfigItem(int sectionId, string configCode, string configName, string textCode, int configType, bool show, bool allowUserEdit, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(configCode))
        {
            throw new ArgumentException("ConfigCode is required.", nameof(configCode));
        }

        if (string.IsNullOrWhiteSpace(configName))
        {
            throw new ArgumentException("ConfigName is required.", nameof(configName));
        }

        if (string.IsNullOrWhiteSpace(textCode))
        {
            throw new ArgumentException("TextCode is required.", nameof(textCode));
        }

        SectionId = sectionId;
        ConfigCode = configCode.Trim();
        ConfigName = configName.Trim();
        TextCode = textCode.Trim();
        ConfigType = configType;
        Show = show;
        AllowUserEdit = allowUserEdit;
        InsertedTime = utcNow;
    }

    /// <summary>Overwrite an already-seeded config (only reached when the seed entry sets btReplace) — including moving it to a different section.</summary>
    public void Replace(int sectionId, string configName, string textCode, int configType, bool show, bool allowUserEdit, DateTime utcNow)
    {
        SectionId = sectionId;
        ConfigName = configName.Trim();
        TextCode = textCode.Trim();
        ConfigType = configType;
        Show = show;
        AllowUserEdit = allowUserEdit;
        UpdatedTime = utcNow;
    }
}
