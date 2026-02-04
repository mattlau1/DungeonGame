#include "main.h"
#include "raylib.h"
#include "game_protocol.pb.h"
#include "game_protocol.grpc.pb.h"
#include "grpcpp/create_channel.h"
#include "engine/network/DungeonClient.h"
#include "engine/graphics/Renderer.h"

int main() {
    // TODO: change default window size
    constexpr int screenWidth = 1280;
    constexpr int screenHeight = 1280;

    DungeonGame::Engine::Graphics::Renderer renderer(screenWidth, screenHeight, "DungeonGame");

    auto const serverAddress = "localhost:5142";
    auto const channel = grpc::CreateChannel(serverAddress, grpc::InsecureChannelCredentials());
    DungeonGame::DungeonClient dungeonClient{channel};

    while (!renderer.ShouldClose()) {
        renderer.BeginFrame();

        renderer.Clear(BLACK);
        DrawFPS(0, 0);

        renderer.EndFrame();
    }

    return 0;
}
