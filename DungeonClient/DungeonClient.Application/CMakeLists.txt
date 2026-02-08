cmake_minimum_required(VERSION 3.20)
project(DungeonEngine CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# LLD Setup
if (UNIX AND NOT APPLE)
    find_program(LLD_PATH NAMES lld)
    if (LLD_PATH)
        add_link_options("-fuse-ld=lld")
    endif ()
endif ()

# Dependencies
find_package(gRPC CONFIG REQUIRED)
find_package(Protobuf CONFIG REQUIRED)
find_package(raylib CONFIG REQUIRED)
find_package(glfw3 CONFIG REQUIRED)

# Proto Discovery
set(PROTO_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../contracts")
file(GLOB_RECURSE PROTO_FILES "${PROTO_DIR}/*.proto")

# Create Executable
add_executable(dungeon_engine
        main.cpp
        src/game/network/DungeonConnection.h
        src/game/network/DungeonConnection.cpp
        src/engine/graphics/Renderer.h
        src/engine/graphics/Renderer.cpp
        src/game/mappings/PlayerMappings.h
        src/game/mappings/PlayerMappings.cpp
        src/game/models/PlayerInfo.h
        src/game/models/CommonTypes.h
        src/game/mappings/MovementMappings.cpp
        src/game/mappings/MovementMappings.h
        src/game/models/Movement.h
        src/game/mappings/CommonMappings.h
        src/game/mappings/CommonMappings.cpp
        src/game/models/Room.h
        src/game/mappings/RoomMappings.h
        src/game/mappings/RoomMappings.cpp
        src/game/network/DungeonService.cpp
        src/game/network/DungeonService.h
)

get_target_property(GRPC_CPP_PLUGIN_EXE gRPC::grpc_cpp_plugin LOCATION)

protobuf_generate(
        TARGET dungeon_engine
        PROTOS ${PROTO_FILES}
        IMPORT_DIRS ${PROTO_DIR}
)

# gRPC Services
protobuf_generate(
        TARGET dungeon_engine
        LANGUAGE grpc
        PROTOS ${PROTO_FILES}
        IMPORT_DIRS ${PROTO_DIR}
        PLUGIN "protoc-gen-grpc=${GRPC_CPP_PLUGIN_EXE}"
        GENERATE_EXTENSIONS .grpc.pb.h .grpc.pb.cc
)

# Includes and Linkage
target_include_directories(dungeon_engine PRIVATE
        ${CMAKE_CURRENT_BINARY_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_link_libraries(dungeon_engine
        PRIVATE
        gRPC::grpc++
        protobuf::libprotobuf
        glfw
        raylib
)

# Precompiled Headers
target_precompile_headers(dungeon_engine PRIVATE
        <grpcpp/grpcpp.h>
        <memory>
        <iostream>
        <string>
        <vector>
)