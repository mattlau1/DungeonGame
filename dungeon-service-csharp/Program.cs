
using DungeonService.Services.Core;
using DungeonService.Services.Dungeon;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<MovementControllerService>();
app.MapGrpcService<PlayerControllerService>();
app.MapGrpcService<DungeonArchitectService>();
app.MapGrpcService<RoomControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();