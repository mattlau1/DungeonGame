#include "main.h"
#include "raylib.h"
#include "engine/network/DungeonClient.h"
#include "engine/graphics/Renderer.h"

int main() {
    // TODO: change default window size
    constexpr int screenWidth = 1280;
    constexpr int screenHeight = 1280;

    DungeonGame::Engine::Graphics::Renderer renderer(screenWidth, screenHeight, "DungeonGame");

    DungeonGame::DungeonClient dungeonClient{};
    dungeonClient.Connect();

    while (!renderer.ShouldClose()) {
        renderer.BeginFrame();

        renderer.Clear(BLACK);
        DrawFPS(0, 0);

        renderer.EndFrame();
    }

    return 0;
}
