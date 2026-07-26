using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Config.Contract.Interfaces;
using OrangepuffPortal.Config.Infrastructure.Notifications;
using OrangepuffPortal.Shared.Events;

namespace OrangepuffPortal.Config.UnitTests;

public class ApplyConfigDefaultsOnUserCreatedHandlerTests
{
    [Fact]
    public async Task Handle_applies_defaults_for_the_new_user()
    {
        var service = new Mock<IConfigUserValueService>();
        var handler = new ApplyConfigDefaultsOnUserCreatedHandler(service.Object, NullLogger<ApplyConfigDefaultsOnUserCreatedHandler>.Instance);

        await handler.Handle(new UserCreatedNotification(UserId: 42, ActorUserId: 1), CancellationToken.None);

        service.Verify(s => s.ApplyDefaultsForNewUserAsync(42, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_swallows_an_exception_from_the_service()
    {
        var service = new Mock<IConfigUserValueService>();
        service.Setup(s => s.ApplyDefaultsForNewUserAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new ApplyConfigDefaultsOnUserCreatedHandler(service.Object, NullLogger<ApplyConfigDefaultsOnUserCreatedHandler>.Instance);

        // Must not throw — a failure applying defaults can never fail the user-creation request that published this.
        var exception = await Record.ExceptionAsync(() => handler.Handle(new UserCreatedNotification(UserId: 42, ActorUserId: 0), CancellationToken.None));

        Assert.Null(exception);
    }
}
