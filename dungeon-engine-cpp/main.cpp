#include "main.h"

#include <iostream>
#include "game_protocol.pb.h"
#include "game_protocol.grpc.pb.h"

int main() {
    std::cout << "Dungeon Server starting..." << std::endl;

    dungeon_game::protocol::Enemy enemy;
    enemy.set_type("Goblin");
    enemy.set_health(100);

    std::cout << "Spawned a " << enemy.type() << " with " << enemy.health() << " HP." << std::endl;
    return 0;
}