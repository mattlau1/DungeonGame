using System.Collections.Concurrent;

namespace DungeonServer.Application.Core.TickSystem.Simulation;

public class SimulationQueue : ISimulationQueue
{
    private readonly ConcurrentDictionary<int, HashSet<Type>> _roomSimulations = new();

    public void Register(int roomId, Type simulationType)
    {
        _roomSimulations.AddOrUpdate(
            roomId,
            addValue: [simulationType],
            updateValueFactory: (_, existing) =>
            {
                existing.Add(simulationType);
                return existing;
            });
    }

    public void Unregister(int roomId, Type simulationType)
    {
        if (!_roomSimulations.TryGetValue(roomId, out HashSet<Type>? types))
        {
            return;
        }

        lock (types)
        {
            types.Remove(simulationType);
            if (types.Count == 0)
            {
                _roomSimulations.TryRemove(roomId, out _);
            }
        }
    }

    public IEnumerable<int> GetActiveRooms()
    {
        return _roomSimulations.Keys;
    }

    public IEnumerable<Type> GetSimulationTypesForRoom(int roomId)
    {
        if (!_roomSimulations.TryGetValue(roomId, out HashSet<Type>? types))
        {
            return [];
        }

        lock (types)
        {
            return types.ToList();
        }
    }
}