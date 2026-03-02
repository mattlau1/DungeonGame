namespace DungeonServer.Application.Core.TickSystem.Simulation;

public interface ISimulationQueue
{
    void Register(int roomId, Type simulationType);
    void Unregister(int roomId, Type simulationType);
    IEnumerable<int> GetActiveRooms();
    IEnumerable<Type> GetSimulationTypesForRoom(int roomId);
}