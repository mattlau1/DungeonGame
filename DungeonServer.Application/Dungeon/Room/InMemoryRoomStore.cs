namespace DungeonServer.Application.Dungeon.Room;

public class InMemoryRoomStore : IRoomStore
{
    public Task<int> CreateRoomAsync(RoomState room, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<RoomStateSnapshot> UpdateRoomAsync(int roomId, Action<RoomState> updateAction, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}