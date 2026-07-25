using MediatR;

namespace OrangepuffPortal.Identity.Application.Commands.AddUser
{
    /// <summary>
    /// Admin-created user. If <paramref name="TemplateUserId"/> is set, the new user inherits
    /// permissions from that template user instead of getting its own SecurityUserRuleItems rows.
    /// <paramref name="Password"/> is optional — a user created without one can still sign in via
    /// Google (or have a password set for them later via SetUserPasswordCommand).
    /// </summary>
    public record AddUserCommand(string Username, string? Email, string? DisplayName, int? TemplateUserId, string? Password) : IRequest<AddUserResult>;
}
