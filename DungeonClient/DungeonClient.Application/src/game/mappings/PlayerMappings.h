#pragma once
#include "game/models/PlayerInfo.h"

namespace dungeon_game::core {
    class PlayerInfo;
}

namespace DungeonGame::Mappings {
    Models::PlayerInfo FromProto(const dungeon_game::core::PlayerInfo &playerInfoProto);
}
