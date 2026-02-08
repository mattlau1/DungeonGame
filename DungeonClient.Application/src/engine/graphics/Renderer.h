#pragma once

#include "raylib.h"

namespace DungeonGame::Engine::Graphics {

class Renderer {
public:
    Renderer(int screenWidth, int screenHeight, const char* title);
    ~Renderer();

    Renderer(const Renderer&) = delete;
    Renderer& operator=(const Renderer&) = delete;
    Renderer(Renderer&&) = delete;
    Renderer& operator=(Renderer&&) = delete;

    void BeginFrame();
    void EndFrame();
    void Clear(Color color);

    [[nodiscard]] bool ShouldClose() const;
};

}