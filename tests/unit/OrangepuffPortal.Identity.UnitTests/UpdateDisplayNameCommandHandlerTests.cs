using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Identity.Application.Commands.UpdateDisplayName;
using OrangepuffPortal.Identity.Domain.Entity;
using OrangepuffPortal.Identity.Domain.Repositories;

namespace OrangepuffPortal.Identity.UnitTests;

public class UpdateDisplayNameCommandHandlerTests
{
    [Fact]
    public async Task Handle_updates_display_name_and_keeps_email()
    {
        var user = new User("alice", "alice@example.com", "Old Name", DateTime.UtcNow);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new UpdateDisplayNameCommandHandler(userRepo.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<UpdateDisplayNameCommandHandler>.Instance);

        var result = await handler.Handle(new UpdateDisplayNameCommand(1, "New Name"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New Name", user.DisplayName);
        Assert.Equal("alice@example.com", user.Email);
        userRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_rejects_blank_display_name()
    {
        var user = new User("alice", null, "Old Name", DateTime.UtcNow);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new UpdateDisplayNameCommandHandler(userRepo.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<UpdateDisplayNameCommandHandler>.Instance);

        var result = await handler.Handle(new UpdateDisplayNameCommand(1, "   "), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("display_name_required", result.RejectionReason);
        Assert.Equal("Old Name", user.DisplayName);
    }

    [Fact]
    public async Task Handle_rejects_when_user_not_found()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var handler = new UpdateDisplayNameCommandHandler(userRepo.Object, TestHelpers.CreateTransactionLogger().Object, NullLogger<UpdateDisplayNameCommandHandler>.Instance);

        var result = await handler.Handle(new UpdateDisplayNameCommand(1, "New Name"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("user_not_found", result.RejectionReason);
    }
}
