using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.Caching.Player;
using DungeonServer.Infrastructure.EntityFramework.Entities;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using PlayerInfoProto = DungeonGame.Core.PlayerInfo;
using LocationProto = DungeonGame.Shared.Location;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Player;

public class EfPlayerStore : IPlayerStore
{
    private readonly IDbContextFactory<DungeonDbContext> _contextFactory;
    private readonly IPlayerCache _playerCache;

    public EfPlayerStore(IDbContextFactory<DungeonDbContext> contextFactory, IPlayerCache playerCache)
    {
        _contextFactory = contextFactory;
        _playerCache = playerCache;
    }

    public async Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        PlayerEntity playerEntity = new PlayerEntity
        {
            X = initialLocation.X, Y = initialLocation.Y, RoomId = RoomConstants.InvalidRoomId, IsOnline = true
        };

        context.Players.Add(playerEntity);
        await context.SaveChangesAsync(ct);

        PlayerInfoProto playerInfo = new PlayerInfoProto
        {
            Id = playerEntity.Id,
            RoomId = playerEntity.RoomId,
            Location = new LocationProto { X = playerEntity.X, Y = playerEntity.Y },
            IsOnline = true
        };

        await _playerCache.SetAsync(playerEntity.Id, playerInfo, TimeSpan.FromSeconds(30), ct);
        await _playerCache.InvalidateCountAsync(ct);

        return ToSnapshot(playerEntity);
    }

    public async Task<PlayerSnapshot> UpdateLocationAsync(
        int playerId,
        Location location,
        int roomId,
        CancellationToken ct)
    {
        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        PlayerEntity? player = await context.Players.FindAsync([playerId], ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        player.X = location.X;
        player.Y = location.Y;
        player.RoomId = roomId;

        await context.SaveChangesAsync(ct);

        var playerInfo = new PlayerInfoProto
        {
            Id = player.Id,
            RoomId = player.RoomId,
            Location = new LocationProto { X = player.X, Y = player.Y },
            IsOnline = player.IsOnline
        };

        await _playerCache.SetAsync(playerId, playerInfo, TimeSpan.FromSeconds(30), ct);

        return ToSnapshot(player);
    }

    public async Task UpdateLocationsBatchAsync(IEnumerable<PlayerUpdate> updates, CancellationToken ct)
    {
        List<PlayerUpdate> updateList = updates.ToList();
        if (updateList.Count == 0)
        {
            return;
        }

        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        List<PlayerEntity> entities = updateList.Select(u => new PlayerEntity
            {
                Id = u.PlayerId, X = u.Location.X, Y = u.Location.Y, RoomId = u.RoomId
            })
            .ToList();

        await context.BulkUpdateAsync(
            entities,
            options =>
            {
                options.PropertiesToInclude =
                [
                    nameof(PlayerEntity.X),
                    nameof(PlayerEntity.Y),
                    nameof(PlayerEntity.RoomId)
                ];
            },
            cancellationToken: ct);

        IEnumerable<(int PlayerId, PlayerInfoProto)> cacheItems = updateList.Select(u => (
            u.PlayerId,
            new PlayerInfoProto
            {
                Id = u.PlayerId,
                RoomId = u.RoomId,
                Location = new LocationProto { X = u.Location.X, Y = u.Location.Y },
                IsOnline = true
            }));
        
        await _playerCache.SetManyAsync(cacheItems, TimeSpan.FromSeconds(30), ct);
    }

    public async Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerInfoProto playerInfo = await _playerCache.GetOrSetAsync(
            playerId,
            async () =>
            {
                await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);
                PlayerEntity? player = await context.Players.FindAsync([playerId], ct);
                if (player == null)
                {
                    throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
                }

                return new PlayerInfoProto
                {
                    Id = player.Id,
                    RoomId = player.RoomId,
                    Location = new LocationProto { X = player.X, Y = player.Y }
                };
            },
            TimeSpan.FromSeconds(30),
            ct);

        return new PlayerSnapshot(
            playerInfo.Id,
            playerInfo.RoomId,
            new Location(playerInfo.Location.X, playerInfo.Location.Y),
            playerInfo.IsOnline);
    }

    public async Task<int> GetActivePlayerCountAsync(CancellationToken ct)
    {
        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Players.CountAsync(player => player.IsOnline, ct);
    }

    public async Task<PlayerSnapshot?> GetFirstActivePlayerAsync(CancellationToken ct)
    {
        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);
        PlayerEntity? firstPlayer = await context.Players.FirstOrDefaultAsync((player) => player.IsOnline, ct);

        if (firstPlayer == null)
        {
            return null;
        }

        return ToSnapshot(firstPlayer);
    }

    public async Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        await using DungeonDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        PlayerEntity? player = await context.Players.FindAsync([playerId], ct);
        if (player == null)
        {
            return;
        }

        player.IsOnline = false;

        await context.SaveChangesAsync(ct);

        await _playerCache.InvalidateAsync(playerId, ct);
        await _playerCache.InvalidateCountAsync(ct);
    }

    private static PlayerSnapshot ToSnapshot(PlayerEntity entity)
    {
        return new PlayerSnapshot(entity.Id, entity.RoomId, new Location(entity.X, entity.Y), entity.IsOnline);
    }
}