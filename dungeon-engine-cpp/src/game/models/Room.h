#pragma once
#include "PlayerInfo.h"

namespace DungeonGame::Models {
    struct RoomSnapshot {
        int roomId{};
        std::vector<PlayerInfo> players;
    };
}
