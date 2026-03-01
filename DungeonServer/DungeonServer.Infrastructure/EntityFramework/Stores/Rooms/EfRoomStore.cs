using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Infrastructure.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;
using DungeonServer.Application.Core.Shared;
using Microsoft.EntityFrameworkCore.Storage;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;

public class EfRoomStore : IRoomStore
{
    private readonly DungeonDbContext _dbContext;
    private readonly IRoomSubscriptionRegistry _subscriptionRegistry;

    public EfRoomStore(DungeonDbContext dbContext, IRoomSubscriptionRegistry subscriptionRegistry)
    {
        _dbContext = dbContext;
        _subscriptionRegistry = subscriptionRegistry;
    }

    public async Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct)
    {
        var roomEntity = new RoomEntity
        {
            Type = room.RoomType,
            Width = room.Width,
            Height = room.Height,
            Occupants = [],
            Exits = []
        };

        _dbContext.Rooms.Add(roomEntity);
        await _dbContext.SaveChangesAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomEntity.Id,
            Players = roomEntity.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomEntity.Id, playerUpdate, RoomUpdateContext.Broadcast(), ct);

        return ToSnapshot(roomEntity);
    }

    public async Task<RoomStateSnapshot> AddPlayerToRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        RoomEntity? room = await _dbContext.Rooms.Include(room => room.Occupants)
            .Include(room => room.Exits)
            .FirstOrDefaultAsync(room => room.Id == roomId, ct);

        if (room == null)
        {
            throw new KeyNotFoundException($"Room Id {roomId} does not exist.");
        }

        if (room.Occupants.Any(p => p.Id == playerId))
        {
            return ToSnapshot(room);
        }

        PlayerEntity? player = await _dbContext.Players.FindAsync([playerId], ct);

        if (player == null)
        {
            throw new KeyNotFoundException($"Player {playerId} not found.");
        }

        room.Occupants.Add(player);

        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = room.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomId, playerUpdate, RoomUpdateContext.Broadcast(), ct);

        return ToSnapshot(room);
    }

    public async Task<RoomStateSnapshot> RemovePlayerFromRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        RoomEntity? room = await _dbContext.Rooms.Include(r => r.Occupants)
            .Include(r => r.Exits)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room == null)
        {
            throw new KeyNotFoundException($"Room Id {roomId} does not exist.");
        }

        PlayerEntity? occupant = room.Occupants.FirstOrDefault(p => p.Id == playerId);
        if (occupant == null)
        {
            throw new ArgumentException($"Player {playerId} is not currently in Room {roomId}.", nameof(playerId));
        }

        room.Occupants.Remove(occupant);

        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = room.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomId, playerUpdate, RoomUpdateContext.Broadcast(), ct);

        return ToSnapshot(room);
    }

    public async Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct)
    {
        RoomEntity? room = await _dbContext.Rooms.AsNoTracking()
            .Include(r => r.Occupants)
            .Include(r => r.Exits)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room == null)
        {
            return null;
        }

        return ToSnapshot(room);
    }

    public async Task PublishRoomUpdateAsync(int roomId, RoomUpdateContext context, CancellationToken ct)
    {
        RoomEntity? room = await _dbContext.Rooms.AsNoTracking()
            .Include(r => r.Occupants)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room == null)
        {
            return;
        }

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = room.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };

        await _subscriptionRegistry.PublishUpdateAsync(roomId, playerUpdate, context, ct);
    }

    private void PublishRoomUpdateAsync(
        int roomId,
        RoomPlayerUpdate update,
        RoomUpdateContext context,
        CancellationToken ct)
    {
        _subscriptionRegistry.PublishUpdateAsync(roomId, update, context, ct);
    }

    public async Task LinkRoomsAsync(int roomIdA, int roomIdB, Direction directionFromAToB, CancellationToken ct)
    {
        if (roomIdA == roomIdB)
        {
            throw new ArgumentException("Cannot link a room to itself.");
        }

        RoomEntity? roomA = await _dbContext.Rooms.FindAsync([roomIdA], ct);
        if (roomA == null)
        {
            throw new KeyNotFoundException($"Room Id {roomIdA} does not exist.");
        }

        RoomEntity? roomB = await _dbContext.Rooms.FindAsync([roomIdB], ct);
        if (roomB == null)
        {
            throw new KeyNotFoundException($"Room Id {roomIdB} does not exist.");
        }

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        DbSet<RoomExitEntity> exitSet = _dbContext.Set<RoomExitEntity>();
        Direction directionFromBToA = Helpers.GetOppositeDirection(directionFromAToB);

        await UpsertExit(roomIdA, directionFromAToB, roomIdB, exitSet, ct);
        await UpsertExit(roomIdB, directionFromBToA, roomIdA, exitSet, ct);

        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await PublishRoomUpdateAsync(roomIdA, RoomUpdateContext.Broadcast(), ct);
        await PublishRoomUpdateAsync(roomIdB, RoomUpdateContext.Broadcast(), ct);
    }

    private static async Task UpsertExit(
        int fromId,
        Direction dir,
        int toId,
        DbSet<RoomExitEntity> set,
        CancellationToken ct)
    {
        RoomExitEntity? exit = await set.FirstOrDefaultAsync(e => e.FromRoomId == fromId && e.ExitDirection == dir, ct);

        if (exit == null)
        {
            await set.AddAsync(new RoomExitEntity { FromRoomId = fromId, ExitDirection = dir, ToRoomId = toId }, ct);
        }
        else
        {
            exit.ToRoomId = toId;
        }
    }

    public async Task SwapRoomsAsync(int playerId, int fromRoomId, int toRoomId, CancellationToken ct)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        List<RoomEntity> rooms = await _dbContext.Rooms.Include(r => r.Occupants)
            .Include(r => r.Exits)
            .Where(r => r.Id == fromRoomId || r.Id == toRoomId)
            .ToListAsync(ct);

        RoomEntity? fromRoom = rooms.FirstOrDefault(r => r.Id == fromRoomId);
        RoomEntity? toRoom = rooms.FirstOrDefault(r => r.Id == toRoomId);

        if (fromRoom == null || toRoom == null)
        {
            throw new KeyNotFoundException("One or both rooms do not exist.");
        }

        PlayerEntity? occupant = fromRoom.Occupants.FirstOrDefault(p => p.Id == playerId);
        if (occupant == null)
        {
            throw new KeyNotFoundException($"Player {playerId} not in source room.");
        }

        fromRoom.Occupants.Remove(occupant);

        if (toRoom.Occupants.All(p => p.Id != playerId))
        {
            toRoom.Occupants.Add(occupant);
        }

        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var playerUpdateFrom = new RoomPlayerUpdate
        {
            RoomId = fromRoomId,
            Players = fromRoom.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        var playerUpdateTo = new RoomPlayerUpdate
        {
            RoomId = toRoomId,
            Players = toRoom.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(fromRoomId, playerUpdateFrom, RoomUpdateContext.Broadcast(), ct);
        PublishRoomUpdateAsync(toRoomId, playerUpdateTo, RoomUpdateContext.Broadcast(), ct);
    }

    public IAsyncEnumerable<RoomPlayerUpdate> SubscribeRoomAsync(
        int subscriberPlayerId,
        int roomId,
        CancellationToken ct)
    {
        return _subscriptionRegistry.SubscribeAsync(subscriberPlayerId, roomId, ct);
    }

    private static RoomStateSnapshot ToSnapshot(RoomEntity roomEntity)
    {
        var players = roomEntity.Occupants
            .Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline))
            .ToList();

        Dictionary<Direction, int> exits = roomEntity.Exits.ToDictionary(e => e.ExitDirection, e => e.ToRoomId);

        return new RoomStateSnapshot(
            roomEntity.Id,
            roomEntity.Type,
            roomEntity.Width,
            roomEntity.Height,
            players,
            exits);
    }
}