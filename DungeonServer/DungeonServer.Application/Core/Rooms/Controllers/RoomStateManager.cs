using System.Collections.Concurrent;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Application.Core.Rooms.Controllers;

public class RoomStateManager
{
    private readonly ConcurrentDictionary<int, RoomStateSnapshot> _roomStates = new();
    private readonly IRoomStore _roomStore;

    public RoomStateManager(IRoomStore roomStore)
    {
        _roomStore = roomStore;
    }

    public async Task<RoomStateSnapshot?> GetRoomStateAsync(int roomId, CancellationToken ct)
    {
        if (_roomStates.TryGetValue(roomId, out RoomStateSnapshot? cached))
        {
            return cached;
        }

        RoomStateSnapshot? room = await _roomStore.GetRoomAsync(roomId, ct);

        if (room != null)
        {
            _roomStates.TryAdd(roomId, room);
        }

        return room;
    }

    public void UpdateRoomState(RoomStateSnapshot room)
    {
        _roomStates[room.RoomId] = room;
    }

    public void RemoveRoom(int roomId)
    {
        _roomStates.TryRemove(roomId, out _);
    }
}