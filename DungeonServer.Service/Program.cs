using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Dungeon.DungeonArchitect;
using DungeonServer.Application.Dungeon.DungeonController;
using DungeonServer.Service.Services.Core;
using DungeonServer.Service.Services.Dungeon;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.AddSingleton<IRoomStore, InMemoryRoomStore>();
builder.Services.AddSingleton<IPlayerStore, InMemoryPlayerStore>();

builder.Services.AddScoped<IDungeonArchitect, DungeonArchitect>();
builder.Services.AddScoped<IDungeonController, DungeonController>();

WebApplication app = builder.Build();

app.MapGrpcService<DungeonControllerService>();
app.MapGrpcService<DungeonArchitectService>();
app.MapGrpcService<RoomControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();