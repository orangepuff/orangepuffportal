using OrangepuffPortal.Bff.Endpoints;
using OrangepuffPortal.Bff.Infrastructure;

namespace OrangepuffPortal.Bff
{
    /// <summary>
    /// Maps Portal's Bff-owned routes: /bff/login, /bff/logout, /bff/me(/permissions),
    /// /bff/me/avatar, /bff/users/{id}/avatar, and the AdminOnly-gated /bff/admin/* group.
    /// </summary>
    public static class PortalBffEndpointRouteBuilderExtensions
    {
        public static WebApplication MapPortalBffEndpoints(this WebApplication app)
        {
            app.MapAuthEndpoints();
            app.MapAvatarEndpoints();

            var adminGroup = app.MapGroup("/bff/admin").RequireAuthorization(PortalBffConstants.AdminOnlyPolicy);
            adminGroup.MapUserAdminEndpoints();
            adminGroup.MapSecurityRuleCategoryAdminEndpoints();
            adminGroup.MapSecurityRuleItemAdminEndpoints();

            return app;
        }
    }
}
