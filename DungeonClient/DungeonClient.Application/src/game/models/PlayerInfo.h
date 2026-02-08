#pragma once
#include "CommonTypes.h"

namespace DungeonGame::Models {
    struct PlayerInfo {
        int id{};
        int roomId{};
        Vector2 location{};
    };
}
