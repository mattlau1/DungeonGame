#include "RoomMappings.h"
#include "PlayerMappings.h"
#include "Core/room.pb.h"

namespace DungeonGame::Mappings {
    Models::RoomSnapshot FromProto(const dungeon_game::core::RoomSnapshot &snapshotProto) {
        Models::RoomSnapshot snapshot;
        snapshot.roomId = snapshotProto.room_id();

        snapshot.players.reserve(snapshotProto.players_size());

        for (const auto &protoPlayer: snapshotProto.players()) {
            snapshot.players.push_back(FromProto(protoPlayer));
        }

        return snapshot;
    }
}
