namespace OrangepuffPortal.Shared.Auditing;

/// <summary>
/// Cross-module read of every user id in [identity].[Users] — lets a module other than Identity
/// (e.g. Config, backfilling a newly-created config's default across existing users) enumerate users
/// without taking a hard project reference on Identity's domain/DbContext, mirroring how
/// <see cref="ICurrentUser"/> hides the current request's identity behind an interface.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<int>> GetAllUserIdsAsync(CancellationToken cancellationToken = default);
}
