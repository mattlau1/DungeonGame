#include "main.h"
#include "raylib.h"
#include "game_protocol.pb.h"
#include "game_protocol.grpc.pb.h"
#include "grpcpp/create_channel.h"
#include "src/DungeonClient.h"

int main() {
    // TODO: change default window size
    constexpr int screenWidth = 1280;
    constexpr int screenHeight = 1280;

    InitWindow(screenWidth, screenHeight, "DungeonGame");

    auto const serverAddress = "localhost:5142";
    auto const channel = grpc::CreateChannel(serverAddress, grpc::InsecureChannelCredentials());
    DungeonGame::DungeonClient dungeonClient{channel};

    while (!WindowShouldClose()) {
        BeginDrawing();

        ClearBackground(BLACK);
        DrawFPS(0, 0);

        EndDrawing();
    }

    CloseWindow();

    return 0;
}
