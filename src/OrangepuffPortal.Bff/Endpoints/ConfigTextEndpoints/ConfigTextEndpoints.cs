using OrangepuffPortal.ConfigText.Contract.Interfaces;

namespace OrangepuffPortal.Bff.Endpoints.ConfigTextEndpoints
{
    /// <summary>
    /// Maps /bff/config-text.
    /// </summary>
    public static class ConfigTextEndpoints
    {
        public static void MapConfigTextEndpoints(this WebApplication app)
        {
            app.MapGet("/bff/config-text", async (string culture, IConfigTextReader reader, CancellationToken ct) =>
            {
                var entries = await reader.GetAllAsync(culture, ct);
                return Results.Ok(entries);
            }).RequireAuthorization();
        }
    }
}
