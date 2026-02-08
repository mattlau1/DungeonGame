#pragma once
#include "game/models/CommonTypes.h"

namespace dungeon_game::shared {
    class Location;
}

namespace DungeonGame::Mappings {
    Models::Vector2 FromProto(const dungeon_game::shared::Location &locationProto);
}
