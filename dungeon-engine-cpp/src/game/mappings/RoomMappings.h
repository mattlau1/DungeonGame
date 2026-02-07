#pragma once
#include "game/models/Room.h"

namespace dungeon_game::core {
    class RoomSnapshot;
}

namespace DungeonGame::Mappings {
    Models::RoomSnapshot FromProto(const dungeon_game::core::RoomSnapshot &snapshotProto);
}
