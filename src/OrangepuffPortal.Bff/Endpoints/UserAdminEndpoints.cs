using OrangepuffPortal.Bff.Infrastructure.IdentityGateway;

namespace OrangepuffPortal.Bff.Endpoints
{
    /// <summary>
    /// Maps /bff/admin/users onto the Identity module's application layer.
    /// Mapped under the /bff/admin group, which already requires the AdminOnly policy.
    /// </summary>
    public static class UserAdminEndpoints
    {
        public static void MapUserAdminEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/users", async (IIdentityGateway client, CancellationToken ct) =>
            {
                var result = await client.ListUsersAsync(ct);
                return Results.Ok(result);
            });

            app.MapPost("/users", async (AddUserRequest req, IIdentityGateway client, CancellationToken ct) =>
            {
                var result = await client.AddUserAsync(req.Username, req.Email, req.DisplayName, req.TemplateUserId, ct);
                return Results.Ok(result);
            });

            app.MapPut("/users/{id:int}", async (int id, UpdateUserRequest req, IIdentityGateway client, CancellationToken ct) =>
            {
                var result = await client.UpdateUserAsync(id, req.Email, req.DisplayName, req.IsTemplateUser, req.ParentId, ct);
                return Results.Ok(result);
            });

            app.MapDelete("/users/{id:int}", async (int id, IIdentityGateway client, CancellationToken ct) =>
            {
                var result = await client.DeleteUserAsync(id, ct);
                return Results.Ok(result);
            });
        }
    }
}
