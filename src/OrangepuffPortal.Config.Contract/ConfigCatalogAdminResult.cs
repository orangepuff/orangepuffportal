namespace OrangepuffPortal.Config.Contract;

public sealed record ConfigCatalogAdminResult(bool Success, int? Id, string? RejectionReason)
{
    public static ConfigCatalogAdminResult Created(int id) => new(true, id, null);

    public static ConfigCatalogAdminResult Updated(int id) => new(true, id, null);

    public static ConfigCatalogAdminResult Deleted() => new(true, null, null);

    public static ConfigCatalogAdminResult Rejected(string reason) => new(false, null, reason);
}
