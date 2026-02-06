#include "DungeonClient.h"
#include <grpcpp/create_channel.h>

namespace DungeonGame {
    DungeonClient::~DungeonClient() = default;

    DungeonClient::DungeonClient() = default;

    void DungeonClient::Connect() {
        bool alreadyConnected = (_stub != nullptr);
        if (alreadyConnected) {
            return;
        }

        _stub = GameProto::DungeonController::NewStub(
            grpc::CreateChannel(GetServerAddress(), grpc::InsecureChannelCredentials()));
    }

    std::string DungeonClient::GetServerAddress() {
        return "localhost:5142";
    }
}
