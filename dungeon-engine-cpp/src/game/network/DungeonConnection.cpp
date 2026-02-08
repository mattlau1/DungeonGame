#include "DungeonConnection.h"

namespace DungeonGame::Network {
    DungeonConnection::~DungeonConnection() = default;

    DungeonConnection::DungeonConnection() = default;

    void DungeonConnection::Connect() {
        const bool alreadyConnected = (_stub != nullptr);
        if (alreadyConnected) {
            return;
        }

        _stub = GameProto::DungeonController::NewStub(
            grpc::CreateChannel(GetServerAddress(), grpc::InsecureChannelCredentials()));
    }

    std::string DungeonConnection::GetServerAddress() {
        return "localhost:5142";
    }
}
