using OrangepuffPortal.Bff.Infrastructure.ConfigGateway;

namespace OrangepuffPortal.Bff.Endpoints.ConfigAdminEndpoints
{
    /// <summary>
    /// Maps /bff/admin/users/{userId}/config/{configCode} onto the Config module.
    /// Mapped under the /bff/admin group, which already requires the AdminOnly policy — writes are
    /// admin-only regardless of Configs.btAllowUserEdit (see docs/config-settings-design.md).
    /// </summary>
    public static class ConfigAdminEndpoints
    {
        public static void MapConfigAdminEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPut("/users/{userId:int}/config/{configCode}", async (int userId, string configCode, SetConfigValueRequest req, IConfigGateway gateway, CancellationToken ct) =>
            {
                await gateway.SetConfigValueAsync(userId, configCode, req.Value, ct);
                return Results.NoContent();
            });
        }
    }
}
