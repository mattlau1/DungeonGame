#include "main.h"
#include "engine/graphics/Renderer.h"
#include "game/network/DungeonConnection.h"

int main() {
    // TODO: change default window size
    constexpr int screenWidth = 1280;
    constexpr int screenHeight = 1280;

    DungeonGame::Engine::Graphics::Renderer renderer(screenWidth, screenHeight, "DungeonGame");

    DungeonGame::Network::DungeonConnection dungeonClient{};
    dungeonClient.Connect();

    while (!renderer.ShouldClose()) {
        renderer.BeginFrame();

        renderer.Clear(BLACK);
        DrawFPS(0, 0);

        renderer.EndFrame();
    }

    return 0;
}
