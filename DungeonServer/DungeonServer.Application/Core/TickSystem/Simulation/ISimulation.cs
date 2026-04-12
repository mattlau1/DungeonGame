using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.TickSystem.Simulation;

public interface ISimulation
{
    Task<List<PlayerState>> SimulateAsync(RoomStateSnapshot room, CancellationToken ct);
}