cmake_minimum_required(VERSION 3.20)

# Standard setup
project(DungeonEngine CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Use LLD if available
if(UNIX AND NOT APPLE)
    find_program(LLD_PATH NAMES lld)
    if(LLD_PATH)
        add_link_options("-fuse-ld=lld")
        message(STATUS "Linker: Using LLD")
    endif()
endif()

set(VCPKG_PREFER_SYSTEM_LIBS OFF)

# Dependencies
find_package(gRPC CONFIG REQUIRED)
find_package(Protobuf CONFIG REQUIRED)
find_package(raylib CONFIG REQUIRED)
find_package(glfw3 CONFIG REQUIRED)

# Executable
add_executable(dungeon_engine main.cpp)

# Protobuf and gRPC setup
set(PROTO_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../contracts")
set(PROTO_FILE "${PROTO_DIR}/game_protocol.proto")

# Locate vcpkg gRPC plugin
get_target_property(GRPC_CPP_PLUGIN_EXE gRPC::grpc_cpp_plugin LOCATION)

# Generate Standard Protobuf code
protobuf_generate(
        TARGET dungeon_engine
        PROTOS ${PROTO_FILE}
        IMPORT_DIRS ${PROTO_DIR}
)

# Generate gRPC specific code
protobuf_generate(
        TARGET dungeon_engine
        LANGUAGE grpc
        PROTOS ${PROTO_FILE}
        IMPORT_DIRS ${PROTO_DIR}
        PLUGIN "protoc-gen-grpc=${GRPC_CPP_PLUGIN_EXE}"
        GENERATE_EXTENSIONS .grpc.pb.h .grpc.pb.cc
)

# Include generated files and library headers
target_include_directories(dungeon_engine PRIVATE
        ${CMAKE_CURRENT_BINARY_DIR}
)

# Linkage
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