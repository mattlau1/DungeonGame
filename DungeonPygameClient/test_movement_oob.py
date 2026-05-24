"""Test movement with actual key presses to trigger server OOB."""

import asyncio
import sys
sys.path.insert(0, '.')

from game import Game
from models import GameConfig


async def test_with_movement():
    """Run game and automatically move player to test boundaries."""
    config = GameConfig()
    game = Game(config)
    
    # Initialize
    if not await game.initialize():
        print("Failed to initialize")
        return 1
    
    print("Moving right for 3 seconds...")
    # Simulate holding D key (move right)
    for i in range(180):  # 3 seconds at 60fps
        game._input._current_input = (1.0, 0.0)  # Force right movement
        game._update()
        game._render()
        await asyncio.sleep(1/60)
        
        if i % 30 == 0:  # Log every 0.5 seconds
            if game._player_controller:
                server_pos = game._player_controller.get_server_position()
                render_pos = game._player_controller.get_render_position()
                room = game._room_snapshot
                if room:
                    print(f"  t={i/60:.1f}s: Render=({render_pos[0]:.1f}, {render_pos[1]:.1f}), "
                          f"Server=({server_pos[0]:.1f}, {server_pos[1]:.1f}), "
                          f"Room={room.width}x{room.height}")
    
    print("\nMoving left for 3 seconds...")
    # Simulate holding A key (move left)
    for i in range(180):
        game._input._current_input = (-1.0, 0.0)  # Force left movement
        game._update()
        game._render()
        await asyncio.sleep(1/60)
        
        if i % 30 == 0:
            if game._player_controller:
                server_pos = game._player_controller.get_server_position()
                render_pos = game._player_controller.get_render_position()
                room = game._room_snapshot
                if room:
                    print(f"  t={i/60:.1f}s: Render=({render_pos[0]:.1f}, {render_pos[1]:.1f}), "
                          f"Server=({server_pos[0]:.1f}, {server_pos[1]:.1f})")
    
    # Cleanup
    await game._cleanup()
    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(test_with_movement())
    sys.exit(exit_code)
