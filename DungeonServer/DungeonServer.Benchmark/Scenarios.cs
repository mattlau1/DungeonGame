namespace DungeonServer.Benchmark;

public static class BenchmarkScenarios
{
    public static List<TestScenario> GetDefaultScenarios() =>
    [
        new TestScenario
        {
            Name = "Single Room Capacity",
            Description = "Tests increasing player counts",
            PlayerCounts = [10, 50, 100, 150, 200],
            MovementHz = 60,
            EnableRoomTransitions = false
        },

        new TestScenario
        {
            Name = "Movement Frequency Stress",
            Description = "Tests different movement rates",
            PlayerCount = 20,
            MovementRates = [10, 30, 60, 120],
            EnableRoomTransitions = false
        },

        new TestScenario
        {
            Name = "EfPlayerStore_GetPlayerAsync_CacheStress",
            Description = "High player count with room subscriptions - tests GetPlayerAsync cache performance",
            PlayerCounts = [50, 200],
            MovementHz = 10,
            EnableRoomTransitions = false
        },

        new TestScenario
        {
            Name = "EfPlayerStore_UpdateLocation_CacheStress",
            Description = "Moderate players with high movement rate - tests UpdateLocationAsync cache performance",
            PlayerCounts = [50, 150],
            MovementHz = 60,
            EnableRoomTransitions = false
        },

        new TestScenario
        {
            Name = "EfPlayerStore_RandomChurn",
            Description = "Random player connect/disconnect churn - tests GetPlayerAsync and GetActivePlayerCount caching",
            PlayerCounts = [50, 100],
            MovementHz = 10,
            EnableRoomTransitions = false,
            EnableChurn = true,
            MinLifetimeMs = 1000,
            MaxLifetimeMs = 5000,
            SpawnDelaySpreadMs = 2000
        },

        new TestScenario
        {
            Name = "Room Transition Stress",
            Description = "Tests room transitions between connected rooms",
            PlayerCounts = [10, 25, 50],
            MovementHz = 30,
            EnableRoomTransitions = true
        }
    ];

    public static List<TestScenario> GetStressScenarios() =>
    [
        new TestScenario
        {
            Name = "Fixed Player Counts",
            Description = "Tests fixed player counts to find capacity",
            PlayerCounts = [500, 1000, 1500, 2000],
            MovementHz = 60,
            EnableRoomTransitions = false
        }
    ];

    public static List<TestScenario> GetQuickScenarios() =>
    [
        new TestScenario
        {
            Name = "Quick Smoke Test",
            Description = "Quick sanity check",
            PlayerCounts = [5, 10],
            MovementHz = 30,
            EnableRoomTransitions = false
        }
    ];
}
