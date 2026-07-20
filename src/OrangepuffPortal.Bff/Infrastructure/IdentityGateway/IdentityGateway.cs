using MediatR;
using OrangepuffPortal.Identity.Application.Commands.AddSecurityRuleCategory;
using OrangepuffPortal.Identity.Application.Commands.AddSecurityRuleItem;
using OrangepuffPortal.Identity.Application.Commands.AddUser;
using OrangepuffPortal.Identity.Application.Commands.DeleteSecurityRuleCategory;
using OrangepuffPortal.Identity.Application.Commands.DeleteSecurityRuleItem;
using OrangepuffPortal.Identity.Application.Commands.DeleteUser;
using OrangepuffPortal.Identity.Application.Commands.ProvisionGoogleUser;
using OrangepuffPortal.Identity.Application.Commands.UpdateSecurityRuleCategory;
using OrangepuffPortal.Identity.Application.Commands.UpdateSecurityRuleItem;
using OrangepuffPortal.Identity.Application.Commands.UpdateUser;
using OrangepuffPortal.Identity.Application.Commands.UpdateUserAvatar;
using OrangepuffPortal.Identity.Application.Queries.GetEffectivePermissions;
using OrangepuffPortal.Identity.Application.Queries.GetUserAvatar;
using OrangepuffPortal.Identity.Application.Queries.IsUserActive;
using OrangepuffPortal.Identity.Application.Queries.IsUserAdmin;
using OrangepuffPortal.Identity.Application.Queries.ListSecurityRuleCategories;
using OrangepuffPortal.Identity.Application.Queries.ListSecurityRuleItems;
using OrangepuffPortal.Identity.Application.Queries.ListUsers;
using OrangepuffPortal.Identity.Contract;
using OrangepuffPortal.Identity.Domain.Enums;

namespace OrangepuffPortal.Bff.Infrastructure.IdentityGateway
{
    /// <summary>
    /// In-process <see cref="IIdentityGateway"/> — dispatches straight to the Identity module's
    /// MediatR handlers (no HTTP hop) and maps its Application-layer results onto this facade's
    /// own DTOs, so the Bff's public surface doesn't leak Identity's internal request/result types.
    /// </summary>
    public class IdentityGateway(IMediator mediator) : IIdentityGateway
    {
        public async Task<GoogleProvisionResult> ProvisionGoogleUserAsync(string providerKey, string email, bool emailVerified, string? displayName, CancellationToken ct = default)
        {
            var result = await mediator.Send(new ProvisionGoogleUserCommand(providerKey, email, emailVerified, displayName), ct);
            return new GoogleProvisionResult(result.Success, result.UserId, result.RejectionReason);
        }

        public Task<bool> IsUserActiveAsync(int userId, CancellationToken ct = default) =>
            mediator.Send(new IsUserActiveQuery(userId), ct);

        public Task<bool> IsUserAdminAsync(int userId, CancellationToken ct = default) =>
            mediator.Send(new IsUserAdminQuery(userId), ct);

        public Task<IReadOnlyList<EffectivePermissionDto>> GetEffectivePermissionsAsync(int userId, CancellationToken ct = default) =>
            mediator.Send(new GetEffectivePermissionsQuery(userId), ct);

        public async Task<UpdateUserAvatarResult> UpdateAvatarAsync(int userId, byte[]? image, string? contentType, CancellationToken ct = default)
        {
            var result = await mediator.Send(new UpdateUserAvatarCommand(userId, image, contentType), ct);
            return new UpdateUserAvatarResult(result.Success, result.RejectionReason);
        }

        public Task<UserAvatarDto?> GetAvatarAsync(int userId, CancellationToken ct = default) =>
            mediator.Send(new GetUserAvatarQuery(userId), ct);

        public Task<IReadOnlyList<UserListItemDto>> ListUsersAsync(CancellationToken ct = default) =>
            mediator.Send(new ListUsersQuery(), ct);

        public Task<IReadOnlyList<SecurityRuleCategoryListItemDto>> ListSecurityRuleCategoriesAsync(CancellationToken ct = default) =>
            mediator.Send(new ListSecurityRuleCategoriesQuery(), ct);

        public Task<IReadOnlyList<SecurityRuleItemListItemDto>> ListSecurityRuleItemsAsync(int? categoryId, CancellationToken ct = default) =>
            mediator.Send(new ListSecurityRuleItemsQuery(categoryId), ct);

        public async Task<AddUserResult> AddUserAsync(string username, string? email, string? displayName, int? templateUserId, CancellationToken ct = default)
        {
            var result = await mediator.Send(new AddUserCommand(username, email, displayName, templateUserId), ct);
            return new AddUserResult(result.Success, result.UserId, result.RejectionReason);
        }

        public async Task<UpdateUserResult> UpdateUserAsync(int userId, string? email, string? displayName, bool isTemplateUser, int? parentId, CancellationToken ct = default)
        {
            var result = await mediator.Send(new UpdateUserCommand(userId, email, displayName, isTemplateUser, parentId), ct);
            return new UpdateUserResult(result.Success, result.RejectionReason);
        }

        public async Task<DeleteUserResult> DeleteUserAsync(int userId, CancellationToken ct = default)
        {
            var result = await mediator.Send(new DeleteUserCommand(userId), ct);
            return new DeleteUserResult(result.Success, result.RejectionReason);
        }

        public async Task<AddSecurityRuleCategoryResult> AddSecurityRuleCategoryAsync(string categoryDesc, string? textCode, CancellationToken ct = default)
        {
            var result = await mediator.Send(new AddSecurityRuleCategoryCommand(categoryDesc, textCode), ct);
            return new AddSecurityRuleCategoryResult(result.Success, result.CategoryId, result.RejectionReason);
        }

        public async Task<UpdateSecurityRuleCategoryResult> UpdateSecurityRuleCategoryAsync(int categoryId, string categoryDesc, string? textCode, bool hidden, CancellationToken ct = default)
        {
            var result = await mediator.Send(new UpdateSecurityRuleCategoryCommand(categoryId, categoryDesc, textCode, hidden), ct);
            return new UpdateSecurityRuleCategoryResult(result.Success, result.RejectionReason);
        }

        public async Task<DeleteSecurityRuleCategoryResult> DeleteSecurityRuleCategoryAsync(int categoryId, CancellationToken ct = default)
        {
            var result = await mediator.Send(new DeleteSecurityRuleCategoryCommand(categoryId), ct);
            return new DeleteSecurityRuleCategoryResult(result.Success, result.RejectionReason);
        }

        public async Task<AddSecurityRuleItemResult> AddSecurityRuleItemAsync(int categoryId, string code, string description, int ruleType, string? textCode, int? sortOrder, CancellationToken ct = default)
        {
            var result = await mediator.Send(new AddSecurityRuleItemCommand(categoryId, code, description, (RuleType)ruleType, textCode, sortOrder), ct);
            return new AddSecurityRuleItemResult(result.Success, result.RuleItemId, result.RejectionReason);
        }

        public async Task<UpdateSecurityRuleItemResult> UpdateSecurityRuleItemAsync(int ruleItemId, int categoryId, string description, int ruleType, string? textCode, int? sortOrder, bool hidden, CancellationToken ct = default)
        {
            var result = await mediator.Send(new UpdateSecurityRuleItemCommand(ruleItemId, categoryId, description, (RuleType)ruleType, textCode, sortOrder, hidden), ct);
            return new UpdateSecurityRuleItemResult(result.Success, result.RejectionReason);
        }

        public async Task<DeleteSecurityRuleItemResult> DeleteSecurityRuleItemAsync(int ruleItemId, CancellationToken ct = default)
        {
            var result = await mediator.Send(new DeleteSecurityRuleItemCommand(ruleItemId), ct);
            return new DeleteSecurityRuleItemResult(result.Success, result.RejectionReason);
        }
    }
}
