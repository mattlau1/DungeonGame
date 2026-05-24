"""Direct movement test - simulates key presses and verifies movement."""

import sys
import time
sys.path.insert(0, '.')

from player_controller import PlayerController
from models import RoomState


import time

def test_movement():
    """Test that movement actually works."""
    print("=== Testing Movement System ===\n")
    
    # Create room and controller
    room = RoomState(1, 40, 40, "COMBAT", {})
    controller = PlayerController(1, room, 20.0, 20.0)
    
    print(f"Initial position: {controller.get_render_position()}")
    
    # Simulate 5 seconds (300 frames) of moving back and forth
    print("\nSimulating 5 seconds of movement (moving right for 2s, left for 2s)...")
    positions = []
    
    start_time = time.time()
    
    for frame in range(300):
        current_time = start_time + (frame / 60.0)
        
        # Move right for 2 seconds, then left for 2 seconds
        if frame < 120:
            input_x = 1.0  # Right
        elif frame < 240:
            input_x = -1.0  # Left
        else:
            input_x = 0.0  # Stop
        
        adjusted = controller.calculate_movement_input(input_x, 0.0)
        controller.update(input_x, 0.0, current_time)
        
        if frame % 30 == 0:  # Log every 0.5 seconds
            pos = controller.get_render_position()
            server_pos = controller._server_x  # Show where server thinks we are
            drift = controller.get_drift()
            print(f"  Frame {frame:3d} (t={frame/60:.1f}s): pos=({pos[0]:5.2f}, {pos[1]:5.2f}) | server={server_pos:5.2f} | drift={drift:.2f}")
        
        positions.append(controller.get_render_position()[0])
    
    final_pos = controller.get_render_position()
    print(f"\nFinal position: {final_pos}")
    print(f"Position history (first 10): {positions[:10]}")
    print(f"Position history (last 10): {positions[-10:]}")
    
    # Check if we actually oscillated or got stuck
    max_pos = max(positions)
    min_pos = min(positions)
    print(f"\nMax position: {max_pos:.2f}")
    print(f"Min position: {min_pos:.2f}")
    print(f"Range: {max_pos - min_pos:.2f}")
    
    if max_pos - min_pos > 5.0:
        print("✓ PASS: Significant movement range detected")
        return True
    else:
        print("✗ FAIL: Movement range too small - likely stuck")
        return False


def test_all_directions():
    """Test movement in all directions."""
    print("\n=== Testing All Directions ===\n")
    
    directions = [
        ("Right", 1.0, 0.0),
        ("Left", -1.0, 0.0),
        ("Up", 0.0, -1.0),
        ("Down", 0.0, 1.0),
    ]
    
    all_passed = True
    for name, input_x, input_y in directions:
        room = RoomState(1, 40, 40, "COMBAT", {})
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Move for 30 frames
        start_time = time.time()
        for i in range(30):
            controller.update(input_x, input_y, start_time + i/60)
        
        pos = controller.get_render_position()
        movement = abs(pos[0] - 20.0) + abs(pos[1] - 20.0)
        
        if movement > 1.0:
            print(f"  {name}: ✓ (moved {movement:.2f} units)")
        else:
            print(f"  {name}: ✗ (moved only {movement:.2f} units)")
            all_passed = False
    
    return all_passed


def test_boundary_stop():
    """Test that we stop at boundaries."""
    print("\n=== Testing Boundary Stopping ===\n")
    
    # Start near right edge
    room = RoomState(1, 40, 40, "COMBAT", {})
    controller = PlayerController(1, room, 38.0, 20.0)
    
    print(f"Starting at: {controller.get_render_position()}")
    print("Moving right toward boundary...")
    
    # Try to move right for 60 frames
    start_time = time.time()
    for frame in range(60):
        controller.update(1.0, 0.0, start_time + frame/60)
        
        if frame % 15 == 0:
            pos = controller.get_render_position()
            print(f"  Frame {frame}: pos=({pos[0]:.2f}, {pos[1]:.2f})")
    
    final_pos = controller.get_render_position()
    print(f"\nFinal position: {final_pos}")
    
    # Should be clamped at ~39.0 (40 - 1.0 margin)
    if final_pos[0] <= 39.5:
        print(f"✓ PASS: Stopped at boundary ({final_pos[0]:.2f} <= 39.5)")
        return True
    else:
        print(f"✗ FAIL: Went past boundary ({final_pos[0]:.2f} > 39.5)")
        return False


if __name__ == "__main__":
    results = []
    
    results.append(("Movement", test_movement()))
    results.append(("All Directions", test_all_directions()))
    results.append(("Boundary", test_boundary_stop()))
    
    print("\n" + "="*50)
    print("SUMMARY:")
    for name, passed in results:
        status = "✓ PASS" if passed else "✗ FAIL"
        print(f"  {name}: {status}")
    
    all_passed = all(r[1] for r in results)
    sys.exit(0 if all_passed else 1)
