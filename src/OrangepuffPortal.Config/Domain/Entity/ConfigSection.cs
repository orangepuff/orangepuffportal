namespace OrangepuffPortal.Config.Domain.Entity;

/// <summary>
/// A group of related <see cref="ConfigItem"/>s. Maps to [config].[ConfigSections].
/// </summary>
/// <remarks>
/// Does not use the shared portal <c>AuditableEntity</c>: rows are written by unattended startup
/// seeding (<see cref="Infrastructure.ConfigCatalogWriter"/>), which has no signed-in user to attribute
/// the change to, so both audit user-id columns are nullable here — same reasoning as
/// <c>OrangepuffPortal.ConfigText</c>'s <c>ConfigTextDefinition</c>.
/// </remarks>
public class ConfigSection
{
    public int Id { get; private set; }
    public string Module { get; private set; } = string.Empty;
    public string SectionDesc { get; private set; } = string.Empty;
    public string TextCode { get; private set; } = string.Empty;
    public bool Show { get; private set; } = true;
    public int? SortOrder { get; private set; }
    public int? InsertedUserId { get; private set; }
    public DateTime? InsertedTime { get; private set; }
    public int? UpdatedUserId { get; private set; }
    public DateTime? UpdatedTime { get; private set; }

    private ConfigSection() { } // EF

    public ConfigSection(string module, string sectionDesc, string textCode, bool show, DateTime utcNow, int? sortOrder = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new ArgumentException("Module is required.", nameof(module));
        }

        if (string.IsNullOrWhiteSpace(sectionDesc))
        {
            throw new ArgumentException("SectionDesc is required.", nameof(sectionDesc));
        }

        if (string.IsNullOrWhiteSpace(textCode))
        {
            throw new ArgumentException("TextCode is required.", nameof(textCode));
        }

        Module = module.Trim();
        SectionDesc = sectionDesc.Trim();
        TextCode = textCode.Trim();
        Show = show;
        SortOrder = sortOrder;
        InsertedTime = utcNow;
    }

    /// <summary>Overwrite an already-seeded section (only reached when the seed entry sets btReplace).</summary>
    public void Replace(string sectionDesc, bool show, DateTime utcNow, int? sortOrder = null)
    {
        SectionDesc = sectionDesc.Trim();
        Show = show;
        SortOrder = sortOrder;
        UpdatedTime = utcNow;
    }
}
