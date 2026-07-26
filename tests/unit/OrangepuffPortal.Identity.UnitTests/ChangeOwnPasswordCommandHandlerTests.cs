using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Identity.Application.Commands.ChangeOwnPassword;
using OrangepuffPortal.Identity.Domain.Entity;
using OrangepuffPortal.Identity.Domain.Repositories;

namespace OrangepuffPortal.Identity.UnitTests;

public class ChangeOwnPasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_changes_password_when_current_password_is_correct()
    {
        var hasher = TestHelpers.CreatePasswordHasher();
        var user = new User("alice", null, null, DateTime.UtcNow);
        user.SetPasswordHash(hasher.HashPassword(user, "OldPassword1"), DateTime.UtcNow);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ChangeOwnPasswordCommandHandler(userRepo.Object, hasher, TestHelpers.CreateTransactionLogger().Object, NullLogger<ChangeOwnPasswordCommandHandler>.Instance);

        var result = await handler.Handle(new ChangeOwnPasswordCommand(1, "OldPassword1", "NewPassword1"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash!, "NewPassword1"));
        userRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_rejects_wrong_current_password()
    {
        var hasher = TestHelpers.CreatePasswordHasher();
        var user = new User("alice", null, null, DateTime.UtcNow);
        user.SetPasswordHash(hasher.HashPassword(user, "OldPassword1"), DateTime.UtcNow);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ChangeOwnPasswordCommandHandler(userRepo.Object, hasher, TestHelpers.CreateTransactionLogger().Object, NullLogger<ChangeOwnPasswordCommandHandler>.Instance);

        var result = await handler.Handle(new ChangeOwnPasswordCommand(1, "WrongPassword", "NewPassword1"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_current_password", result.RejectionReason);
    }

    [Fact]
    public async Task Handle_rejects_when_user_has_no_password_set()
    {
        var user = new User("googleuser", null, null, DateTime.UtcNow); // Google-only account, no password hash

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ChangeOwnPasswordCommandHandler(userRepo.Object, TestHelpers.CreatePasswordHasher(), TestHelpers.CreateTransactionLogger().Object, NullLogger<ChangeOwnPasswordCommandHandler>.Instance);

        var result = await handler.Handle(new ChangeOwnPasswordCommand(1, "anything", "NewPassword1"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("no_password_set", result.RejectionReason);
    }
}
