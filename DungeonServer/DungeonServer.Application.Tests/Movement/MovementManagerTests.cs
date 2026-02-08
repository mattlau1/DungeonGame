using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Movement;

public class MovementManagerTests
{
    [Fact]
    public async Task SetMovementInput_Applies_WhenWithinSpeedLimit()
    {
        var playerStore = new InMemoryPlayerStore();
        var manager = new MovementManager(playerStore);
        
        PlayerSnapshot snapshot = await playerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);

        var requested = new Location(1f, 0f);  

        MovementInputResponse resp = await manager.SetMovementInput(new MovementInputRequest(snapshot.PlayerId, requested), CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.status);
        Assert.Equal(requested.X, resp.location.X);
        Assert.Equal(requested.Y, resp.location.Y);
    }

    [Fact]
    public async Task SetMovementInput_Returns_TooFast_When_MoveExceedsLimit()
    {
        var playerStore = new InMemoryPlayerStore();
        var manager = new MovementManager(playerStore);

        PlayerSnapshot snapshot = await playerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);

        var requested = new Location(100f, 0f); 

        MovementInputResponse resp = await manager.SetMovementInput(new MovementInputRequest(snapshot.PlayerId, requested), CancellationToken.None);

        Assert.Equal(MovementRequestStatus.TooFast, resp.status);
        Assert.Equal(0f, resp.location.X);
        Assert.Equal(0f, resp.location.Y);
    }

    [Fact]
    public async Task SetMovementInput_Throws_When_PlayerMissing()
    {
        var playerStore = new InMemoryPlayerStore();
        var manager = new MovementManager(playerStore);

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await manager.SetMovementInput(new MovementInputRequest(9999, new Location(1,1)), CancellationToken.None);
        });
    }
}
