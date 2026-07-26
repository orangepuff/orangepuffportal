namespace OrangepuffPortal.ConfigText.Contract;

public sealed record ConfigTextAdminResult(bool Success, int? Id, string? RejectionReason)
{
    public static ConfigTextAdminResult Created(int id) => new(true, id, null);

    public static ConfigTextAdminResult Updated(int id) => new(true, id, null);

    public static ConfigTextAdminResult Deleted() => new(true, null, null);

    public static ConfigTextAdminResult Rejected(string reason) => new(false, null, reason);
}
