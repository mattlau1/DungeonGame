#pragma once
#include "DungeonConnection.h"
#include "game/models/PlayerInfo.h"

namespace DungeonGame::Network {
    class DungeonService {
    public:
        explicit DungeonService(DungeonConnection &connection) : _connection(connection) {
        }

        [[nodiscard]] Models::PlayerInfo SpawnPlayer() const;
        [[nodiscard]] Models::PlayerInfo GetPlayerInfo(int playerId) const;

    private:
        DungeonConnection &_connection;
    };
}
