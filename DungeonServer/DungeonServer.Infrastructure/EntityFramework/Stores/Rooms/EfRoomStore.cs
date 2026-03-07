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
    private readonly IDbContextFactory<DungeonDbContext> _contextFactory;
    private readonly IRoomSubscriptionRegistry _subscriptionRegistry;

    public EfRoomStore(IDbContextFactory<DungeonDbContext> contextFactory, IRoomSubscriptionRegistry subscriptionRegistry)
    {
        _contextFactory = contextFactory;
        _subscriptionRegistry = subscriptionRegistry;
    }

    public async Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var roomEntity = new RoomEntity
        {
            Type = room.RoomType,
            Width = room.Width,
            Height = room.Height,
            Occupants = [],
            Exits = []
        };

        context.Rooms.Add(roomEntity);
        await context.SaveChangesAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomEntity.Id,
            Players = roomEntity.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomEntity.Id, playerUpdate, ct);

        return ToSnapshot(roomEntity);
    }

    public async Task<RoomStateSnapshot> AddPlayerToRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

        RoomEntity? room = await context.Rooms.Include(room => room.Occupants)
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

        PlayerEntity? player = await context.Players.FindAsync([playerId], ct);

        if (player == null)
        {
            throw new KeyNotFoundException($"Player {playerId} not found.");
        }

        room.Occupants.Add(player);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = room.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomId, playerUpdate, ct);

        return ToSnapshot(room);
    }

    public async Task<RoomStateSnapshot> RemovePlayerFromRoomAsync(int roomId, int playerId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

        RoomEntity? room = await context.Rooms.Include(r => r.Occupants)
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

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var playerUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = room.Occupants.Select(o => new PlayerSnapshot(o.Id, o.RoomId, new Location(o.X, o.Y), o.IsOnline)).ToList()
        };
        PublishRoomUpdateAsync(roomId, playerUpdate, ct);

        return ToSnapshot(room);
    }

    public async Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        RoomEntity? room = await context.Rooms.AsNoTracking()
            .Include(r => r.Occupants)
            .Include(r => r.Exits)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

        if (room == null)
        {
            return null;
        }

        return ToSnapshot(room);
    }

    public async Task PublishRoomUpdateAsync(int roomId, CancellationToken ct)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(ct);

        RoomEntity? room = await dbContext.Rooms.AsNoTracking()
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

        await _subscriptionRegistry.PublishUpdateAsync(roomId, playerUpdate, ct);
    }

    private void PublishRoomUpdateAsync(
        int roomId,
        RoomPlayerUpdate update,
        CancellationToken ct)
    {
        _subscriptionRegistry.PublishUpdateAsync(roomId, update, ct);
    }

    public async Task LinkRoomsAsync(int roomIdA, int roomIdB, Direction directionFromAToB, CancellationToken ct)
    {
        if (roomIdA == roomIdB)
        {
            throw new ArgumentException("Cannot link a room to itself.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        RoomEntity? roomA = await context.Rooms.FindAsync([roomIdA], ct);
        if (roomA == null)
        {
            throw new KeyNotFoundException($"Room Id {roomIdA} does not exist.");
        }

        RoomEntity? roomB = await context.Rooms.FindAsync([roomIdB], ct);
        if (roomB == null)
        {
            throw new KeyNotFoundException($"Room Id {roomIdB} does not exist.");
        }

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

        DbSet<RoomExitEntity> exitSet = context.Set<RoomExitEntity>();
        Direction directionFromBToA = Helpers.GetOppositeDirection(directionFromAToB);

        await UpsertExit(roomIdA, directionFromAToB, roomIdB, exitSet, ct);
        await UpsertExit(roomIdB, directionFromBToA, roomIdA, exitSet, ct);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await PublishRoomUpdateAsync(roomIdA, ct);
        await PublishRoomUpdateAsync(roomIdB, ct);
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
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

        List<RoomEntity> rooms = await context.Rooms.Include(r => r.Occupants)
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

        await context.SaveChangesAsync(ct);
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
        PublishRoomUpdateAsync(fromRoomId, ct);
        PublishRoomUpdateAsync(toRoomId, ct);
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