#include "Renderer.h"

namespace DungeonGame::Engine::Graphics {

Renderer::Renderer(int screenWidth, int screenHeight, const char* title) {
    InitWindow(screenWidth, screenHeight, title);
}

Renderer::~Renderer() {
    CloseWindow();
}

void Renderer::BeginFrame() {
    BeginDrawing();
}

void Renderer::EndFrame() {
    EndDrawing();
}

void Renderer::Clear(const Color color) {
    ClearBackground(color);
}

bool Renderer::ShouldClose() const {
    return WindowShouldClose();
}

}
