using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.TickSystem.Simulation;

public interface ISimulation
{
    Task SimulateAsync(RoomStateSnapshot room, CancellationToken ct);
}