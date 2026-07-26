using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Identity.Application.Commands.ProvisionGoogleUser;
using OrangepuffPortal.Identity.Domain.Entity;
using OrangepuffPortal.Identity.Domain.Repositories;
using OrangepuffPortal.Shared.Events;

namespace OrangepuffPortal.Identity.UnitTests;

public class ProvisionGoogleUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_provisions_a_new_user_and_publishes_UserCreatedNotification_with_system_actor()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByExternalLoginAsync("Google", "google-key", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        repo.Setup(r => r.GetByEmailAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        // Simulates the identity value EF would assign on SaveChangesAsync — the handler needs a non-zero
        // Id before it can construct an ExternalLogin, which a mocked repository won't do on its own.
        repo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, 7))
            .Returns(Task.CompletedTask);

        var registrationPolicy = new Mock<IUserRegistrationPolicy>();
        registrationPolicy.Setup(p => p.IsSelfRegistrationAllowedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var publisher = new Mock<IPublisher>();

        var handler = new ProvisionGoogleUserCommandHandler(repo.Object, registrationPolicy.Object, publisher.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<ProvisionGoogleUserCommandHandler>.Instance);

        var result = await handler.Handle(new ProvisionGoogleUserCommand("google-key", "new@example.com", EmailVerified: true, "New User"), CancellationToken.None);

        Assert.True(result.Success);
        repo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.Publish(It.Is<UserCreatedNotification>(n => n.ActorUserId == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_publish_when_rejected_for_unverified_email()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByExternalLoginAsync("Google", "google-key", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var publisher = new Mock<IPublisher>();

        var handler = new ProvisionGoogleUserCommandHandler(repo.Object, Mock.Of<IUserRegistrationPolicy>(), publisher.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<ProvisionGoogleUserCommandHandler>.Instance);

        var result = await handler.Handle(new ProvisionGoogleUserCommand("google-key", "new@example.com", EmailVerified: false, "New User"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("email_not_verified", result.RejectionReason);
        publisher.Verify(p => p.Publish(It.IsAny<UserCreatedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_does_not_publish_when_resolving_an_already_linked_user()
    {
        var existingUser = new User("google_existing", "existing@example.com", "Existing User", DateTime.UtcNow);

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByExternalLoginAsync("Google", "google-key", It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        var publisher = new Mock<IPublisher>();

        var handler = new ProvisionGoogleUserCommandHandler(repo.Object, Mock.Of<IUserRegistrationPolicy>(), publisher.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<ProvisionGoogleUserCommandHandler>.Instance);

        var result = await handler.Handle(new ProvisionGoogleUserCommand("google-key", "existing@example.com", EmailVerified: true, "Existing User"), CancellationToken.None);

        Assert.True(result.Success);
        publisher.Verify(p => p.Publish(It.IsAny<UserCreatedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
