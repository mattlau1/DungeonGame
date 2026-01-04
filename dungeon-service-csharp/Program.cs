using DungeonService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<DungeonArchitectService>();

app.MapGet("/", () => "Dungeon Architect is alive. Connect via gRPC.");

app.Run();