#!/usr/bin/env python3
"""Generate Python gRPC files from .proto contracts."""

import subprocess
import sys
from pathlib import Path

# Paths
CONTRACTS_DIR = Path(__file__).parent.parent / "contracts"
PROTO_DIR = Path(__file__).parent / "proto"

def generate_proto():
    """Compile all .proto files to Python."""
    PROTO_DIR.mkdir(exist_ok=True)
    
    proto_files = [
        "Shared/shared_types.proto",
        "Core/player.proto",
        "Core/room.proto",
        "Core/movement.proto",
        "Core/status.proto",
        "Core/dungeon_controller.proto",
    ]
    
    for proto_file in proto_files:
        proto_path = CONTRACTS_DIR / proto_file
        if not proto_path.exists():
            print(f"Warning: {proto_path} not found, skipping...")
            continue
            
        print(f"Generating {proto_file}...")
        
        # Calculate import path (contracts dir itself for proper imports)
        import_path = CONTRACTS_DIR
        
        cmd = [
            sys.executable, "-m", "grpc_tools.protoc",
            f"--proto_path={import_path}",
            f"--python_out={PROTO_DIR}",
            f"--grpc_python_out={PROTO_DIR}",
            proto_file
        ]
        
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            print(f"Error generating {proto_file}:")
            print(result.stderr)
            return False
        else:
            print(f"  ✓ Generated successfully")
    
    # Create __init__.py for proto package
    (PROTO_DIR / "__init__.py").write_text("")
    
    print("\nAll proto files generated!")
    return True

if __name__ == "__main__":
    success = generate_proto()
    sys.exit(0 if success else 1)
