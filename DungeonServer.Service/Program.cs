
using DungeonGame.Application.Abstractions.Dungeon;
using DungeonGame.Application.Dungeon;
using DungeonServer.Service.Services.Core;
using DungeonServer.Service.Services.Dungeon;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services.AddScoped<IDungeonArchitect, DungeonArchitect>();

var app = builder.Build();

app.MapGrpcService<MovementControllerService>();
app.MapGrpcService<PlayerControllerService>();
app.MapGrpcService<DungeonArchitectService>();
app.MapGrpcService<RoomControllerService>();

app.MapGet("/", () => "Dungeon Game Service is live. Connect via gRPC.");

app.Run();