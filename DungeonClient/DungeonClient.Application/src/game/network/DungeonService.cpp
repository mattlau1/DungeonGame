#include "DungeonService.h"

#include "game/mappings/PlayerMappings.h"

namespace DungeonGame::Network {
    Models::PlayerInfo DungeonService::SpawnPlayer() const {
        grpc::ClientContext context;
        GameProto::SpawnRequest request;
        GameProto::PlayerInfo response;

        const grpc::Status status = _connection._stub->SpawnPlayer(&context, request, &response);
        if (status.ok()) {
            return Mappings::FromProto(response);
        }

        return {};
    }

    Models::PlayerInfo DungeonService::GetPlayerInfo(const int playerId) const {
        grpc::ClientContext context;
        GameProto::PlayerInfoRequest request;
        request.set_player_id(playerId);

        GameProto::PlayerInfo response;

        const grpc::Status status = _connection._stub->GetPlayerInfo(&context, request, &response);
        if (status.ok()) {
            return Mappings::FromProto(response);
        }

        return {};
    }
}
