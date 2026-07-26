using Diagnostics.Abstractions.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrangepuffPortal.Identity.Domain.Entity;
using OrangepuffPortal.Identity.Domain.Repositories;

namespace OrangepuffPortal.Identity.Application.Commands.VerifyPassword
{
    public class VerifyPasswordCommandHandler(
        IUserRepository repository
        , IPasswordHasher<User> passwordHasher
        , ITransactionLogger transactionLogger
        , ILogger<VerifyPasswordCommandHandler> logger) : IRequestHandler<VerifyPasswordCommand, VerifyPasswordResult>
    {
        private const string LogPrefix = nameof(VerifyPasswordCommandHandler) + "." + nameof(Handle);

        public async Task<VerifyPasswordResult> Handle(VerifyPasswordCommand request, CancellationToken cancellationToken)
        {
            using var transaction = transactionLogger.BeginTransaction("VerifyPassword", $"Password sign-in attempt for {request.UsernameOrEmail}");

            var user = await repository.GetByUsernameAsync(request.UsernameOrEmail, cancellationToken)
                ?? await repository.GetByEmailAsync(request.UsernameOrEmail, cancellationToken);

            if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            {
                logger.LogWarning("{LogPrefix}: rejected, no matching account with a password for {UsernameOrEmail}", LogPrefix, request.UsernameOrEmail);
                transaction.SetCustomAttribute("outcome", "invalid_credentials");
                return VerifyPasswordResult.Rejected("invalid_credentials");
            }

            transaction.SetUser(user.Id.ToString());

            if (!user.IsActive)
            {
                logger.LogWarning("{LogPrefix}: rejected, user {UserId} is inactive", LogPrefix, user.Id);
                transaction.SetCustomAttribute("outcome", "account_inactive");
                return VerifyPasswordResult.Rejected("account_inactive");
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                logger.LogWarning("{LogPrefix}: rejected, wrong password for user {UserId}", LogPrefix, user.Id);
                transaction.SetCustomAttribute("outcome", "invalid_credentials");
                return VerifyPasswordResult.Rejected("invalid_credentials");
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password), DateTime.UtcNow);
                await repository.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation("{LogPrefix}: verified password sign-in for user {UserId}", LogPrefix, user.Id);
            return VerifyPasswordResult.Allowed(user.Id, user.Email, user.DisplayName, user.CultureCode);
        }
    }
}
