using System.Text.RegularExpressions;

namespace OrangepuffPortal.Theme.Contract;

/// <summary>
/// Converts a <see cref="ThemeDto"/> into a flat CSS custom-property map.
/// Variable names follow the pattern <c>--op-{section}-{element}-{property}</c> in kebab-case.
/// </summary>
public static class ThemeCssVarBuilder
{
    private static readonly Regex UpperCasePattern = new(@"([A-Z])", RegexOptions.Compiled);

    public static Dictionary<string, string> Build(ThemeDto theme)
    {
        var result = new Dictionary<string, string>();

        foreach (var section in theme.Sections)
        {
            foreach (var element in section.Elements)
            {
                foreach (var detail in element.Details)
                {
                    if (detail.PropertyValue is null)
                    {
                        continue;
                    }

                    var varName = $"--op-{ToKebab(section.SectionCode)}-{ToKebab(element.ElementCode)}-{ToKebab(detail.PropertyKey)}";
                    var varValue = detail.PropertyType == "number_unit" && detail.Unit is not null
                        ? detail.PropertyValue + detail.Unit
                        : detail.PropertyValue;

                    result[varName] = varValue;
                }
            }
        }

        return result;
    }

    private static string ToKebab(string s) =>
        UpperCasePattern.Replace(s, m => "-" + m.Value.ToLowerInvariant())
            .TrimStart('-')
            .Replace(" ", "-")
            .ToLowerInvariant();
}
