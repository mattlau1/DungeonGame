#include "DungeonService.h"

#include "game/mappings/PlayerMappings.h"

namespace DungeonGame::Network {
    Models::PlayerInfo DungeonService::SpawnPlayer() const {
        grpc::ClientContext context;
        GameProto::SpawnRequest request;
        GameProto::PlayerInfo response;

        grpc::Status status = _connection._stub->SpawnPlayer(&context, request, &response);
        if (status.ok()) {
            return Mappings::FromProto(response);
        }

        return {};
    }
}
