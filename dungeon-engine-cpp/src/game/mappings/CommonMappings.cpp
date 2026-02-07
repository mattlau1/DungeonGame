#include "CommonMappings.h"

#include "Shared/shared_types.pb.h"

namespace DungeonGame::Mappings {
    Models::Vector2 FromProto(const dungeon_game::shared::Location &locationProto) {
        return Models::Vector2{locationProto.x(), locationProto.y()};
    }
}
