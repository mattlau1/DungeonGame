namespace DungeonServer.Application.Core.TickSystem.Contracts;

public interface ITickScheduler
{
    void Start();
    
    void Stop();

    public void RegisterRoom(int roomId);
    
    public void UnregisterRoom(int roomId);
}