using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;

public class EfRoomStore : IRoomStore
{
    public Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<RoomStateSnapshot> AddPlayerToRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<RoomStateSnapshot> RemovePlayerFromRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task PublishRoomUpdateAsync(int roomId, RoomUpdateContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task LinkRoomsAsync(int roomIdA, int roomIdB, Direction directionFromAToB, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SwapRoomsAsync(int playerId, int fromRoomId, int toRoomId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<RoomStateSnapshot> SubscribeRoomAsync(int subscriberPlayerId, int roomId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}