namespace OrangepuffPortal.Bff.Infrastructure.IdentityGateway
{
    public record GoogleProvisionResult(bool Success, int? UserId, string? RejectionReason);
}
