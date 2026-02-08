#pragma once

#include "Core/dungeon_controller.grpc.pb.h"

namespace DungeonGame::Network {
    namespace GameProto = dungeon_game::core;

    class DungeonConnection {
        friend class DungeonService;

    public:
        explicit DungeonConnection();

        ~DungeonConnection();

        void Connect();

        DungeonConnection(const DungeonConnection &) = delete;

        DungeonConnection &operator=(const DungeonConnection &) = delete;

        DungeonConnection(DungeonConnection &&) noexcept = default;

        DungeonConnection &operator=(DungeonConnection &&) noexcept = default;

    private:
        static std::string GetServerAddress();

        std::unique_ptr<GameProto::DungeonController::Stub> _stub;
    };
}
