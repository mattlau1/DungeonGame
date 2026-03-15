using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.TickSystem.Contracts;
using DungeonServer.Application.Core.TickSystem.Controllers;
using DungeonServer.Application.Core.TickSystem.Simulation;
using DungeonServer.Application.External;
using DungeonServer.Infrastructure.Caching.Generic;
using DungeonServer.Infrastructure.Caching.Player;
using DungeonServer.Infrastructure.EntityFramework;
using DungeonServer.Infrastructure.EntityFramework.Stores.Player;
using DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;
using DungeonServer.Infrastructure.Messaging.Rooms;
using DungeonServer.Service.Services.Core;
using Grpc.AspNetCore.Server.Model;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// HTTP/2 flow control tuning for high-throughput streaming
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024 * 2; // 2 MB
    options.Limits.Http2.InitialStreamWindowSize = 1024 * 1024; // 1 MB
    
    // Keep-alive settings to prevent connection timeouts
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});

// TODO: Don't hard code configuration & add fallback options
string? redisConfiguration = builder.Configuration.GetSection("Redis:Configuration").Value;
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfiguration));

string? connectionString = builder.Configuration.GetConnectionString("DbConnection");
builder.Services.AddDbContextFactory<DungeonDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddGrpc();

builder.Services.AddSingleton<IRoomSubscriptionRegistry, RedisRoomSubscriptionRegistry>();

builder.Services.AddSingleton<IProtoCacheService, RedisProtoCacheService>();
builder.Services.AddSingleton<IPlayerCache, RedisPlayerCache>();

builder.Services.AddSingleton<IPlayerStore, EfPlayerStore>();
builder.Services.AddSingleton<IRoomStore, EfRoomStore>();

builder.Services.AddSingleton<ISimulationQueue, SimulationQueue>();
builder.Services.AddSingleton<ISimulation, PlayerSimulation>();
builder.Services.AddSingleton<ITickScheduler, TickRunner>();

builder.Services.AddSingleton<IPlayerInputManager, PlayerInputManager>();
builder.Services.AddSingleton<PlayerStateManager>();
builder.Services.AddSingleton<RoomStateManager>();

builder.Services.AddSingleton<IPlayerManager, PlayerManager>();
builder.Services.AddSingleton<IDungeonArchitect, DungeonArchitect>();
builder.Services.AddSingleton<IDungeonController, DungeonController>();
builder.Services.AddSingleton<IMovementManager, MovementManager>();

builder.Services.AddSingleton<IServiceMethodProvider<DungeonControllerService>, DungeonControllerMethodProvider>();

ThreadPool.SetMinThreads(500, 500);

WebApplication app = builder.Build();

app.Services.GetRequiredService<ITickScheduler>().Start();

app.MapGrpcService<DungeonControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();