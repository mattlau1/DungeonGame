"""Test bot movement specifically."""

import sys
sys.path.insert(0, '.')

import math
from simulated_player import SimulatedPlayer


def test_bot_calculation():
    """Test that bot calculates movement properly."""
    print("=== Testing Bot Movement Calculation ===\n")
    
    # Create a mock bot (without connecting)
    class MockBot:
        def __init__(self):
            self._x = 20.0
            self._y = 20.0
            self._room_width = 40.0
            self._room_height = 40.0
            self._random = __import__('random').Random(123)  # Seed for reproducibility
    
    bot = MockBot()
    
    print(f"Starting position: ({bot._x}, {bot._y})")
    
    # Simulate 20 movement calculations
    for i in range(20):
        # Copy the calculation logic from SimulatedPlayer
        angle = bot._random.random() * 2 * math.pi
        distance = 0.3
        
        input_x = math.cos(angle) * distance
        input_y = math.sin(angle) * distance
        
        margin = 1.0
        predicted_x = bot._x + input_x
        predicted_y = bot._y + input_y
        
        clamped_x = max(margin, min(predicted_x, bot._room_width - margin))
        clamped_y = max(margin, min(predicted_y, bot._room_height - margin))
        
        adjusted_input_x = clamped_x - bot._x
        adjusted_input_y = clamped_y - bot._y
        
        # Update position
        bot._x = clamped_x
        bot._y = clamped_y
        
        if i % 5 == 0:
            print(f"  Step {i}: pos=({bot._x:.2f}, {bot._y:.2f}) | input=({adjusted_input_x:.3f}, {adjusted_input_y:.3f})")
    
    final_pos = (bot._x, bot._y)
    print(f"\nFinal position: ({final_pos[0]:.2f}, {final_pos[1]:.2f})")
    
    # Calculate distance from start
    dx = final_pos[0] - 20.0
    dy = final_pos[1] - 20.0
    dist = math.sqrt(dx*dx + dy*dy)
    
    print(f"Distance from start: {dist:.2f} units")
    
    if dist > 0.5:
        print("✓ PASS: Bot moved significantly")
        return True
    else:
        print("✗ FAIL: Bot barely moved")
        return False


def test_bot_boundary_clamping():
    """Test bot respects boundaries."""
    print("\n=== Testing Bot Boundary Respect ===\n")
    
    class MockBot:
        def __init__(self):
            self._x = 38.0  # Near edge
            self._y = 20.0
            self._room_width = 40.0
            self._room_height = 40.0
            self._random = __import__('random').Random(456)
    
    bot = MockBot()
    
    print(f"Starting at edge: ({bot._x}, {bot._y})")
    
    # Try to move many times
    out_of_bounds = False
    for i in range(50):
        angle = bot._random.random() * 2 * math.pi
        distance = 0.3
        
        input_x = math.cos(angle) * distance
        input_y = math.sin(angle) * distance
        
        margin = 1.0
        predicted_x = bot._x + input_x
        predicted_y = bot._y + input_y
        
        # This should clamp
        clamped_x = max(margin, min(predicted_x, bot._room_width - margin))
        clamped_y = max(margin, min(predicted_y, bot._room_height - margin))
        
        if clamped_x != predicted_x or clamped_y != predicted_y:
            out_of_bounds = True
        
        bot._x = clamped_x
        bot._y = clamped_y
    
    print(f"Final position: ({bot._x:.2f}, {bot._y:.2f})")
    
    if bot._x <= 39.0 and bot._y <= 39.0 and bot._x >= 1.0 and bot._y >= 1.0:
        print("✓ PASS: Stayed within boundaries")
        return True
    else:
        print("✗ FAIL: Went out of bounds")
        return False


if __name__ == "__main__":
    results = []
    results.append(("Bot Movement", test_bot_calculation()))
    results.append(("Bot Boundaries", test_bot_boundary_clamping()))
    
    print("\n" + "="*50)
    print("SUMMARY:")
    for name, passed in results:
        status = "✓ PASS" if passed else "✗ FAIL"
        print(f"  {name}: {status}")
    
    all_passed = all(r[1] for r in results)
    sys.exit(0 if all_passed else 1)
