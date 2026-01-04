cmake_minimum_required(VERSION 3.20)
project(DungeonGameServer CXX)

set(CMAKE_CXX_STANDARD 20)

find_package(PkgConfig REQUIRED)

pkg_check_modules(PROTOBUF REQUIRED protobuf)
pkg_check_modules(GRPC REQUIRED grpc++)

set(SOURCES
        main.cpp
        game_protocol.pb.cc
        game_protocol.grpc.pb.cc
)

add_executable(GameServer ${SOURCES})

# Link the libraries found by PkgConfig
target_link_libraries(GameServer
        ${PROTOBUF_LIBRARIES}
        ${GRPC_LIBRARIES}
        pthread
)

# Include the headers found by PkgConfig
target_include_directories(GameServer PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${PROTOBUF_INCLUDE_DIRS}
        ${GRPC_INCLUDE_DIRS}
)