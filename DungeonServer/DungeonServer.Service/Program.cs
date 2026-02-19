using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.External;
using DungeonServer.Infrastructure.Messaging.Rooms;
using DungeonServer.Infrastructure.Persistence;
using DungeonServer.Infrastructure.Persistence.InMemory.Player;
using DungeonServer.Infrastructure.Persistence.InMemory.Rooms;
using DungeonServer.Service.Services.Core;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DbConnection");
builder.Services.AddDbContext<DungeonDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddGrpc();

builder.Services.AddSingleton<IRoomSubscriptionRegistry, RoomSubscriptionRegistry>();
builder.Services.AddSingleton<IRoomStore, InMemoryRoomStore>();
builder.Services.AddSingleton<IPlayerStore, InMemoryPlayerStore>();

builder.Services.AddScoped<IPlayerManager, PlayerManager>();
builder.Services.AddScoped<IDungeonArchitect, DungeonArchitect>();
builder.Services.AddScoped<IDungeonController, DungeonController>();
builder.Services.AddScoped<IMovementManager, MovementManager>();

WebApplication app = builder.Build();

app.MapGrpcService<DungeonControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();