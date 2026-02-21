using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Player;

public class EfPlayerStore : IPlayerStore
{
    private readonly DungeonDbContext _dbContext;

    public EfPlayerStore(DungeonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        var playerEntity = new PlayerEntity
        {
            X = initialLocation.X, Y = initialLocation.Y, RoomId = RoomConstants.InvalidRoomId, IsOnline = true
        };

        _dbContext.Players.Add(playerEntity);
        await _dbContext.SaveChangesAsync(ct);

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

        return ToSnapshot(player);
    }

    public async Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerEntity? player = await _dbContext.Players.FindAsync([playerId], ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        return ToSnapshot(player);
    }

    public async Task<int> GetPlayerCountAsync(CancellationToken ct)
    {
        return await _dbContext.Players.CountAsync(ct);
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
    }

    private static PlayerSnapshot ToSnapshot(PlayerEntity entity)
    {
        return new PlayerSnapshot(entity.Id, entity.RoomId, new Location(entity.X, entity.Y), entity.IsOnline);
    }
}