#include "CommonMappings.h"
#include "MovementMappings.h"

#include "Core/movement.pb.h"

namespace DungeonGame::Mappings {
    Models::SetMovementInputRequest FromProto(const dungeon_game::core::SetMovementInputRequest &inputRequestProto) {
        return Models::SetMovementInputRequest{
            inputRequestProto.player_id(),
            inputRequestProto.input_x(),
            inputRequestProto.input_y(),
        };
    }

    Models::SetMovementInputResponse FromProto(const dungeon_game::core::SetMovementInputResponse &inputResponseProto) {
        return Models::SetMovementInputResponse{
            FromProto(inputResponseProto.status_response()),
            FromProto(inputResponseProto.authoritative_location())
        };
    }

    Models::MovementInputStatusResult
    FromProto(const dungeon_game::core::MovementInputStatusResult statusResultProto) {
        return static_cast<Models::MovementInputStatusResult>(statusResultProto);
    }
}
