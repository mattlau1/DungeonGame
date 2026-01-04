cmake_minimum_required(VERSION 3.20)

# Standard setup
project(DungeonEngine CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(VCPKG_PREFER_SYSTEM_LIBS OFF)

# Dependencies
find_package(gRPC CONFIG REQUIRED)
find_package(Protobuf CONFIG REQUIRED)

# Executable
add_executable(dungeon_engine main.cpp)

# Protobuf and gRPC setup
set(PROTO_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../contracts/game_protocol.proto")
get_target_property(GRPC_CPP_PLUGIN_EXE gRPC::grpc_cpp_plugin LOCATION)
protobuf_generate(
        TARGET dungeon_engine
        LANGUAGE cpp
        PROTOS ${PROTO_FILE}
        IMPORT_DIRS "${CMAKE_CURRENT_SOURCE_DIR}/../contracts"
)
protobuf_generate(
        TARGET dungeon_engine
        LANGUAGE grpc
        PROTOS ${PROTO_FILE}
        IMPORT_DIRS "${CMAKE_CURRENT_SOURCE_DIR}/../contracts"
        PLUGIN "protoc-gen-grpc=${GRPC_CPP_PLUGIN_EXE}"
        GENERATE_EXTENSIONS .grpc.pb.h .grpc.pb.cc
)

# Link
target_include_directories(dungeon_engine PRIVATE
        ${CMAKE_CURRENT_BINARY_DIR}
)

target_link_libraries(dungeon_engine
        PRIVATE
        gRPC::grpc++
        protobuf::libprotobuf
)