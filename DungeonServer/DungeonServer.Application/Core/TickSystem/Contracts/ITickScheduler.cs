namespace DungeonServer.Application.Core.TickSystem.Contracts;

public interface ITickScheduler
{
    void Start();
    
    void Stop();
}