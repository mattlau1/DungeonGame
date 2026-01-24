using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Dungeon;
using DungeonServer.Application.Dungeon.Rooms.Storage;
using DungeonServer.Service.Services.Core;
using DungeonServer.Service.Services.Dungeon;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.AddSingleton<IRoomStore, InMemoryRoomStore>();
builder.Services.AddScoped<IDungeonArchitect, DungeonArchitect>();

var app = builder.Build();

app.MapGrpcService<MovementControllerService>();
app.MapGrpcService<PlayerControllerService>();
app.MapGrpcService<DungeonArchitectService>();
app.MapGrpcService<RoomControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();