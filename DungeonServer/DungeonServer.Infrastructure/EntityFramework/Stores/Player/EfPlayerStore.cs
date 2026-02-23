using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.Caching.Player;
using DungeonServer.Infrastructure.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;
using PlayerInfoProto = DungeonGame.Core.PlayerInfo;
using LocationProto = DungeonGame.Shared.Location;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Player;

public class EfPlayerStore : IPlayerStore
{
    private readonly DungeonDbContext _dbContext;
    private readonly IPlayerCache _playerCache;

    public EfPlayerStore(DungeonDbContext dbContext, IPlayerCache playerCache)
    {
        _dbContext = dbContext;
        _playerCache = playerCache;
    }

    public async Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        var playerEntity = new PlayerEntity
        {
            X = initialLocation.X, Y = initialLocation.Y, RoomId = RoomConstants.InvalidRoomId, IsOnline = true
        };

        _dbContext.Players.Add(playerEntity);
        await _dbContext.SaveChangesAsync(ct);

        var playerInfo = new PlayerInfoProto
        {
            Id = playerEntity.Id,
            RoomId = playerEntity.RoomId,
            Location = new LocationProto { X = playerEntity.X, Y = playerEntity.Y }
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
        PlayerEntity? player = await _dbContext.Players.FindAsync([playerId], ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        player.X = location.X;
        player.Y = location.Y;
        player.RoomId = roomId;

        await _dbContext.SaveChangesAsync(ct);

        var playerInfo = new PlayerInfoProto
        {
            Id = player.Id,
            RoomId = player.RoomId,
            Location = new LocationProto { X = player.X, Y = player.Y }
        };
        
        await _playerCache.SetAsync(playerId, playerInfo, TimeSpan.FromSeconds(30), ct);

        return ToSnapshot(player);
    }

    public async Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerInfoProto playerInfo = await _playerCache.GetOrSetAsync(
            playerId,
            async () =>
            {
                PlayerEntity? player = await _dbContext.Players.FindAsync([playerId], ct);
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

        return new PlayerSnapshot(playerInfo.Id, playerInfo.RoomId, new Location(playerInfo.Location.X, playerInfo.Location.Y), true);
    }

    public async Task<int> GetActivePlayerCountAsync(CancellationToken ct)
    {
        return await _dbContext.Players.CountAsync(player => player.IsOnline, ct);
    }

    public async Task<PlayerSnapshot?> GetFirstActivePlayerAsync(CancellationToken ct)
    {
        PlayerEntity? firstPlayer = await _dbContext.Players.FirstOrDefaultAsync(
            (player) => player.IsOnline,
            ct);
        
        if (firstPlayer == null)
        {
            return null;
        }

        return ToSnapshot(firstPlayer);
    }

    public async Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerEntity? player = await _dbContext.Players.FindAsync([playerId], ct);
        if (player == null)
        {
            return;
        }

        player.IsOnline = false;
        
        await _dbContext.SaveChangesAsync(ct);
        
        await _playerCache.InvalidateAsync(playerId, ct);
        await _playerCache.InvalidateCountAsync(ct);
    }

    private static PlayerSnapshot ToSnapshot(PlayerEntity entity)
    {
        return new PlayerSnapshot(entity.Id, entity.RoomId, new Location(entity.X, entity.Y), entity.IsOnline);
    }
}