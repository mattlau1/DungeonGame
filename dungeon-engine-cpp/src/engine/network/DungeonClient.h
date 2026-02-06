#pragma once
#include <memory>

#include "Core/dungeon_controller.grpc.pb.h"

namespace DungeonGame {
    namespace GameProto = dungeon_game::core;

    class DungeonClient {
    public:
        explicit DungeonClient();

        ~DungeonClient();

        void Connect();

        DungeonClient(const DungeonClient &) = delete;

        DungeonClient &operator=(const DungeonClient &) = delete;

        DungeonClient(DungeonClient &&) noexcept = default;

        DungeonClient &operator=(DungeonClient &&) noexcept = default;

    private:
        static std::string GetServerAddress();
        std::unique_ptr<GameProto::DungeonController::Stub> _stub;
    };


}
