using Microsoft.EntityFrameworkCore;
using OrangepuffPortal.Theme.Domain.Entity;

namespace OrangepuffPortal.Theme.Infrastructure.Seeding;

/// <summary>
/// Idempotent seeder: creates the "Default" theme with all standard sections, elements, and property
/// definitions if it does not already exist. Runs on every startup when DoMigration = true.
/// </summary>
public class ThemeDbSeeder(ThemeDbContext db)
{
    private static readonly DateTime SeedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Themes.AnyAsync(x => x.ThemeCode == Domain.Entity.Theme.DefaultThemeCode, ct))
        {
            return;
        }

        var theme = new Domain.Entity.Theme(Domain.Entity.Theme.DefaultThemeCode, "Default light theme for the application", isActive: true, SeedTime);
        await db.Themes.AddAsync(theme, ct);
        await db.SaveChangesAsync(ct);

        await SeedSectionsAsync(theme.Id, ct);
    }

    private async Task SeedSectionsAsync(int themeId, CancellationToken ct)
    {
        var sections = new[]
        {
            ("Project",       "Manage colors and styles for project-related components",  1),
            ("Buttons",       "Button styles including primary, secondary and danger",     2),
            ("Typography",    "Font families, sizes, weights and line heights",            3),
            ("Navigation",    "Sidebar, top bar and breadcrumb visual settings",          4),
            ("FormControls",  "Input, select, checkbox, radio and toggle styles",         5),
            ("DataDisplay",   "Badge, chip, tag and status indicator styles",             6),
            ("Table",         "Data table, pagination and row styles",                    7),
            ("DialogPopup",   "Modal dialog and overlay visual settings",                 8),
            ("AlertFeedback", "Alert, toast and snackbar notification styles",            9),
            ("Layout",        "Page layout, spacing, container and grid settings",       10),
            ("Others",        "Miscellaneous component and utility styles",              11),
        };

        foreach (var (code, desc, sort) in sections)
        {
            var section = new ThemeSection(themeId, code, desc, sort, SeedTime);
            await db.ThemeSections.AddAsync(section, ct);
            await db.SaveChangesAsync(ct);

            await SeedElementsAsync(section.Id, code, ct);
        }
    }

    private async Task SeedElementsAsync(int sectionId, string sectionCode, CancellationToken ct)
    {
        var elementMap = GetElementMap();
        if (!elementMap.TryGetValue(sectionCode, out var elements))
        {
            return;
        }

        foreach (var (code, desc, sort) in elements)
        {
            var element = new ThemeElement(sectionId, code, desc, sort, SeedTime);
            await db.ThemeElements.AddAsync(element, ct);
            await db.SaveChangesAsync(ct);

            await SeedDetailsAsync(element.Id, sectionCode, code, ct);
        }
    }

    private async Task SeedDetailsAsync(int elementId, string sectionCode, string elementCode, CancellationToken ct)
    {
        var detailMap = GetDetailMap();
        var key = $"{sectionCode}.{elementCode}";
        if (!detailMap.TryGetValue(key, out var details))
        {
            return;
        }

        foreach (var (propKey, label, description, type, allowedValues, value, unit, sort) in details)
        {
            var detail = new ThemeDetail(elementId, propKey, label, description, type, allowedValues, value, unit, sort, SeedTime);
            await db.ThemeDetails.AddAsync(detail, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Element definitions per section ─────────────────────────────────────────

    private static Dictionary<string, (string Code, string Desc, int Sort)[]> GetElementMap() => new()
    {
        ["Project"] =
        [
            ("Card",     "Card component",           1),
            ("Panel",    "Panel container",          2),
            ("Badge",    "Badge / chip indicator",   3),
            ("Tag",      "Tag label",                4),
            ("Progress", "Progress bar",             5),
            ("Stepper",  "Stepper component",        6),
        ],
        ["Buttons"] =
        [
            ("Primary",   "Primary action button",   1),
            ("Secondary", "Secondary action button", 2),
            ("Danger",    "Destructive action button",3),
            ("Ghost",     "Ghost / text button",     4),
        ],
        ["Typography"] =
        [
            ("Heading1", "H1 headline",   1),
            ("Heading2", "H2 headline",   2),
            ("Heading3", "H3 headline",   3),
            ("Body",     "Body text",     4),
            ("Caption",  "Caption text",  5),
            ("Label",    "Form label",    6),
        ],
        ["Navigation"] =
        [
            ("Sidebar",     "Sidebar navigation",    1),
            ("TopBar",      "Top navigation bar",    2),
            ("Breadcrumb",  "Breadcrumb trail",      3),
        ],
        ["FormControls"] =
        [
            ("Input",    "Text input field",   1),
            ("Select",   "Select / dropdown",  2),
            ("Checkbox", "Checkbox control",   3),
            ("Radio",    "Radio button",       4),
            ("Toggle",   "Toggle / switch",    5),
        ],
        ["DataDisplay"] =
        [
            ("Badge",   "Status badge",     1),
            ("Chip",    "Chip / tag",       2),
            ("Tooltip", "Tooltip bubble",   3),
        ],
        ["Table"] =
        [
            ("Header",     "Table header row",  1),
            ("Row",        "Table body row",    2),
            ("Pagination", "Pagination bar",    3),
        ],
        ["DialogPopup"] =
        [
            ("Dialog",   "Modal dialog",     1),
            ("Overlay",  "Overlay backdrop", 2),
        ],
        ["AlertFeedback"] =
        [
            ("Success", "Success alert",   1),
            ("Warning", "Warning alert",   2),
            ("Error",   "Error alert",     3),
            ("Info",    "Info alert",      4),
            ("Snackbar","Snackbar toast",  5),
        ],
        ["Layout"] =
        [
            ("Page",      "Page background",     1),
            ("Container", "Content container",   2),
            ("Divider",   "Divider line",        3),
        ],
    };

    // ── Property definitions per element ────────────────────────────────────────

    private static readonly string ShadowOptions = "None;Small;Medium;Large;Extra Large";
    private static readonly string UnitOptions = "px;rem;%";

    private static Dictionary<string, (string Key, string Label, string Desc, string Type, string? Allowed, string? Value, string? Unit, int Sort)[]> GetDetailMap() => new()
    {
        ["Project.Card"] =
        [
            ("backgroundColor",  "Background Color",  "Background color of card",      "color",       null,          "#FFFFFF", null, 1),
            ("borderColor",      "Border Color",      "Border color of card",          "color",       null,          "#E2E8F0", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius of card",         "number_unit", UnitOptions,   "12",      "px", 3),
            ("shadow",           "Shadow",            "Shadow elevation of card",      "dropdown",    ShadowOptions, "Medium",  null, 4),
            ("padding",          "Padding",           "Inner spacing of card",         "number_unit", UnitOptions,   "16",      "px", 5),
            ("margin",           "Margin",            "Outer spacing of card",         "number_unit", UnitOptions,   "16",      "px", 6),
            ("titleColor",       "Title Color",       "Color of card title text",      "color",       null,          "#1E293B", null, 7),
            ("bodyTextColor",    "Body Text Color",   "Color of body text",            "color",       null,          "#475569", null, 8),
        ],
        ["Project.Panel"] =
        [
            ("backgroundColor",  "Background Color",  "Background color of panel",    "color",       null,          "#F8FAFC", null, 1),
            ("borderColor",      "Border Color",      "Border color of panel",        "color",       null,          "#E2E8F0", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius of panel",       "number_unit", UnitOptions,   "8",       "px", 3),
            ("padding",          "Padding",           "Inner spacing of panel",       "number_unit", UnitOptions,   "24",      "px", 4),
            ("titleColor",       "Title Color",       "Color of panel title",         "color",       null,          "#1E293B", null, 5),
        ],
        ["Project.Badge"] =
        [
            ("backgroundColor",  "Background Color",  "Badge background",             "color",       null,          "#EFF6FF", null, 1),
            ("textColor",        "Text Color",        "Badge text color",             "color",       null,          "#1D4ED8", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius of badge",       "number_unit", UnitOptions,   "4",       "px", 3),
            ("fontSize",         "Font Size",         "Badge font size",              "number_unit", UnitOptions,   "12",      "px", 4),
        ],
        ["Project.Tag"] =
        [
            ("backgroundColor",  "Background Color",  "Tag background",               "color",       null,          "#F1F5F9", null, 1),
            ("textColor",        "Text Color",        "Tag text color",               "color",       null,          "#475569", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius of tag",         "number_unit", UnitOptions,   "4",       "px", 3),
        ],
        ["Project.Progress"] =
        [
            ("trackColor",       "Track Color",       "Progress track background",    "color",       null,          "#E2E8F0", null, 1),
            ("fillColor",        "Fill Color",        "Progress fill color",          "color",       null,          "#0EA5E9", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "999",     "px", 3),
            ("height",           "Height",            "Progress bar height",          "number_unit", UnitOptions,   "8",       "px", 4),
        ],
        ["Project.Stepper"] =
        [
            ("activeColor",      "Active Color",      "Active step color",            "color",       null,          "#0EA5E9", null, 1),
            ("completedColor",   "Completed Color",   "Completed step color",         "color",       null,          "#22C55E", null, 2),
            ("inactiveColor",    "Inactive Color",    "Inactive step color",          "color",       null,          "#CBD5E1", null, 3),
            ("labelColor",       "Label Color",       "Step label text color",        "color",       null,          "#475569", null, 4),
        ],
        ["Buttons.Primary"] =
        [
            ("backgroundColor",  "Background Color",  "Button background",            "color",       null,          "#0EA5E9", null, 1),
            ("textColor",        "Text Color",        "Button text color",            "color",       null,          "#FFFFFF", null, 2),
            ("hoverColor",       "Hover Color",       "Background on hover",          "color",       null,          "#0284C7", null, 3),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "6",       "px", 4),
            ("fontSize",         "Font Size",         "Button font size",             "number_unit", UnitOptions,   "14",      "px", 5),
            ("padding",          "Padding",           "Inner padding",                "number_unit", UnitOptions,   "10",      "px", 6),
        ],
        ["Buttons.Secondary"] =
        [
            ("backgroundColor",  "Background Color",  "Button background",            "color",       null,          "#FFFFFF", null, 1),
            ("textColor",        "Text Color",        "Button text color",            "color",       null,          "#0EA5E9", null, 2),
            ("borderColor",      "Border Color",      "Button border",                "color",       null,          "#0EA5E9", null, 3),
            ("hoverColor",       "Hover Color",       "Background on hover",          "color",       null,          "#F0F9FF", null, 4),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "6",       "px", 5),
        ],
        ["Buttons.Danger"] =
        [
            ("backgroundColor",  "Background Color",  "Button background",            "color",       null,          "#EF4444", null, 1),
            ("textColor",        "Text Color",        "Button text color",            "color",       null,          "#FFFFFF", null, 2),
            ("hoverColor",       "Hover Color",       "Background on hover",          "color",       null,          "#DC2626", null, 3),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "6",       "px", 4),
        ],
        ["Buttons.Ghost"] =
        [
            ("textColor",        "Text Color",        "Button text color",            "color",       null,          "#475569", null, 1),
            ("hoverColor",       "Hover Color",       "Background on hover",          "color",       null,          "#F1F5F9", null, 2),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "6",       "px", 3),
        ],
        ["Typography.Body"] =
        [
            ("fontFamily",       "Font Family",       "Body font family",             "text",        null,          "Inter, sans-serif", null, 1),
            ("fontSize",         "Font Size",         "Body font size",               "number_unit", UnitOptions,   "14",      "px",  2),
            ("lineHeight",       "Line Height",       "Body line height",             "number_unit", UnitOptions,   "1.5",     "rem", 3),
            ("color",            "Text Color",        "Body text color",              "color",       null,          "#374151", null,  4),
        ],
        ["Typography.Heading1"] =
        [
            ("fontSize",         "Font Size",         "H1 font size",                 "number_unit", UnitOptions,   "32",      "px", 1),
            ("fontWeight",       "Font Weight",       "H1 font weight",               "dropdown",    "300;400;500;600;700;800;900", "700", null, 2),
            ("color",            "Text Color",        "H1 text color",                "color",       null,          "#111827", null, 3),
            ("lineHeight",       "Line Height",       "H1 line height",               "number_unit", UnitOptions,   "1.2",     "rem", 4),
        ],
        ["Typography.Heading2"] =
        [
            ("fontSize",         "Font Size",         "H2 font size",                 "number_unit", UnitOptions,   "24",      "px", 1),
            ("fontWeight",       "Font Weight",       "H2 font weight",               "dropdown",    "300;400;500;600;700;800;900", "600", null, 2),
            ("color",            "Text Color",        "H2 text color",                "color",       null,          "#111827", null, 3),
        ],
        ["Navigation.Sidebar"] =
        [
            ("backgroundColor",  "Background Color",  "Sidebar background",           "color",       null,          "#1E293B", null, 1),
            ("textColor",        "Text Color",        "Sidebar text color",           "color",       null,          "#CBD5E1", null, 2),
            ("activeItemColor",  "Active Item Color", "Active nav item background",   "color",       null,          "#334155", null, 3),
            ("activeTextColor",  "Active Text Color", "Active nav item text",         "color",       null,          "#FFFFFF", null, 4),
            ("width",            "Width",             "Sidebar width",                "number_unit", UnitOptions,   "240",     "px", 5),
        ],
        ["Navigation.TopBar"] =
        [
            ("backgroundColor",  "Background Color",  "Top bar background",           "color",       null,          "#FFFFFF", null, 1),
            ("textColor",        "Text Color",        "Top bar text color",           "color",       null,          "#1E293B", null, 2),
            ("borderColor",      "Border Color",      "Top bar bottom border",        "color",       null,          "#E2E8F0", null, 3),
            ("height",           "Height",            "Top bar height",               "number_unit", UnitOptions,   "64",      "px", 4),
        ],
        ["FormControls.Input"] =
        [
            ("backgroundColor",  "Background Color",  "Input background",             "color",       null,          "#FFFFFF", null, 1),
            ("borderColor",      "Border Color",      "Input border",                 "color",       null,          "#CBD5E1", null, 2),
            ("focusBorderColor", "Focus Border Color","Border color when focused",    "color",       null,          "#0EA5E9", null, 3),
            ("textColor",        "Text Color",        "Input text color",             "color",       null,          "#1E293B", null, 4),
            ("borderRadius",     "Border Radius",     "Corner radius",               "number_unit", UnitOptions,   "6",       "px", 5),
            ("fontSize",         "Font Size",         "Input font size",              "number_unit", UnitOptions,   "14",      "px", 6),
        ],
        ["Table.Header"] =
        [
            ("backgroundColor",  "Background Color",  "Header row background",        "color",       null,          "#F8FAFC", null, 1),
            ("textColor",        "Text Color",        "Header text color",            "color",       null,          "#475569", null, 2),
            ("borderColor",      "Border Color",      "Header bottom border",         "color",       null,          "#E2E8F0", null, 3),
            ("fontSize",         "Font Size",         "Header font size",             "number_unit", UnitOptions,   "12",      "px", 4),
        ],
        ["Table.Row"] =
        [
            ("backgroundColor",  "Background Color",  "Row background",               "color",       null,          "#FFFFFF", null, 1),
            ("hoverColor",       "Hover Color",       "Row hover background",         "color",       null,          "#F8FAFC", null, 2),
            ("textColor",        "Text Color",        "Row text color",               "color",       null,          "#374151", null, 3),
            ("borderColor",      "Border Color",      "Row divider color",            "color",       null,          "#F1F5F9", null, 4),
        ],
        ["AlertFeedback.Success"] =
        [
            ("backgroundColor",  "Background Color",  "Success alert background",     "color",       null,          "#F0FDF4", null, 1),
            ("textColor",        "Text Color",        "Success alert text",           "color",       null,          "#15803D", null, 2),
            ("borderColor",      "Border Color",      "Success alert border",         "color",       null,          "#86EFAC", null, 3),
        ],
        ["AlertFeedback.Error"] =
        [
            ("backgroundColor",  "Background Color",  "Error alert background",       "color",       null,          "#FEF2F2", null, 1),
            ("textColor",        "Text Color",        "Error alert text",             "color",       null,          "#DC2626", null, 2),
            ("borderColor",      "Border Color",      "Error alert border",           "color",       null,          "#FCA5A5", null, 3),
        ],
        ["AlertFeedback.Warning"] =
        [
            ("backgroundColor",  "Background Color",  "Warning alert background",     "color",       null,          "#FFFBEB", null, 1),
            ("textColor",        "Text Color",        "Warning alert text",           "color",       null,          "#D97706", null, 2),
            ("borderColor",      "Border Color",      "Warning alert border",         "color",       null,          "#FCD34D", null, 3),
        ],
        ["Layout.Page"] =
        [
            ("backgroundColor",  "Background Color",  "Page background",              "color",       null,          "#F1F5F9", null, 1),
            ("textColor",        "Text Color",        "Page default text color",      "color",       null,          "#374151", null, 2),
        ],
    };
}
