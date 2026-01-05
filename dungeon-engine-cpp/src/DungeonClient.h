#pragma once
#include <memory>

#include "game_protocol.grpc.pb.h"
#include "grpcpp/channel.h"

namespace DungeonGame {
    class DungeonClient {
    public:
        explicit DungeonClient(std::shared_ptr<grpc::Channel> channel);

        ~DungeonClient();

        DungeonClient(const DungeonClient &) = delete;

        DungeonClient &operator=(const DungeonClient &) = delete;

        DungeonClient(DungeonClient &&) noexcept = default;

        DungeonClient &operator=(DungeonClient &&) noexcept = default;

        void GenerateRoom(int seed, int floorLevel) const;
    private:
        std::unique_ptr<dungeon_game::protocol::DungeonArchitect::Stub> stub_;
    };
}
