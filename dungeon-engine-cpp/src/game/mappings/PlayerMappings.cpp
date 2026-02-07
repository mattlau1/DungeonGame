#include "PlayerMappings.h"

#include "Core/player.pb.h"

namespace DungeonGame::Mappings {
    Models::PlayerInfo FromProto(const dungeon_game::core::PlayerInfo &playerInfoProto) {
        return Models::PlayerInfo{
            playerInfoProto.id(),
            playerInfoProto.room_id(),
            Models::Vector2{playerInfoProto.location().x(), playerInfoProto.location().y()}
        };
    }
}
