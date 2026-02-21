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
using DungeonServer.Infrastructure.EntityFramework;
using DungeonServer.Infrastructure.EntityFramework.Stores.Player;
using DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;
using DungeonServer.Infrastructure.Messaging.Rooms;
using DungeonServer.Service.Services.Core;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DbConnection");
builder.Services.AddDbContext<DungeonDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddGrpc();

builder.Services.AddSingleton<IRoomSubscriptionRegistry, RoomSubscriptionRegistry>();
builder.Services.AddScoped<IPlayerStore, EfPlayerStore>();
builder.Services.AddScoped<IRoomStore, EfRoomStore>();

builder.Services.AddScoped<IPlayerManager, PlayerManager>();
builder.Services.AddScoped<IDungeonArchitect, DungeonArchitect>();
builder.Services.AddScoped<IDungeonController, DungeonController>();
builder.Services.AddScoped<IMovementManager, MovementManager>();

WebApplication app = builder.Build();

app.MapGrpcService<DungeonControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();