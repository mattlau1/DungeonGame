#pragma once
#include "game/models/Movement.h"

namespace dungeon_game::core {
    enum MovementInputStatusResult : int;
    class SetMovementInputResponse;
    class SetMovementInputRequest;
}

namespace DungeonGame::Mappings {
    Models::SetMovementInputRequest FromProto(const dungeon_game::core::SetMovementInputRequest &inputRequestProto);

    Models::MovementInputStatusResult FromProto(const dungeon_game::core::MovementInputStatusResult &statusResultProto);

    Models::SetMovementInputResponse FromProto(const dungeon_game::core::SetMovementInputResponse inputResponseProto);
}
