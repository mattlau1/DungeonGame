#include "DungeonClient.h"

namespace DungeonGame {
    DungeonClient::~DungeonClient() = default;

    DungeonClient::DungeonClient(std::shared_ptr<grpc::Channel> channel) : stub_(
        dungeon_game::protocol::DungeonArchitect::NewStub(channel)) {
    }

    void DungeonClient::GenerateRoom(int seed, int floorlevel) const {
        dungeon_game::protocol::RoomRequest request;
        request.set_seed(seed);
        request.set_floor_level(floorlevel);

        dungeon_game::protocol::DungeonRoom response;
        grpc::ClientContext context;

        stub_->GenerateRoom(&context, request, &response);
    }
}
