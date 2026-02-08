#pragma once
#include "CommonTypes.h"

namespace DungeonGame::Models {
    struct SetMovementInputRequest {
        int playerId{};
        float inputX{};
        float inputY{};
    };

    enum MovementInputStatusResult {
        UNSPECIFIED = 0,
        OK = 1,
        BLOCKED = 2,
        INVALID_PLAYER = 3,
        TOO_FAST = 4,
    };

    struct SetMovementInputResponse {
        MovementInputStatusResult statusResponse{};
        Vector2 authoritativeLocation{};
    };
}
