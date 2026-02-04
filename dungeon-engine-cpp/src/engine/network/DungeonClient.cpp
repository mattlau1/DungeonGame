#include "DungeonClient.h"

namespace DungeonGame {
    DungeonClient::~DungeonClient() = default;

    DungeonClient::DungeonClient(std::shared_ptr<grpc::Channel> channel) {
    }
}
