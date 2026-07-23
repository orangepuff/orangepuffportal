namespace OrangepuffPortal.Bff.Endpoints.UserAdminEndpoints
{
    public record UpdateUserRequest(string? Email, string? DisplayName, bool IsTemplateUser, int? ParentId);
}
