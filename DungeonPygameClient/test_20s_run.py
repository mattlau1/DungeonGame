#!/usr/bin/env python3
"""Run client for 20 seconds with logging to observe server behavior."""

import asyncio
import sys
import time
sys.path.insert(0, '.')

from game import Game
from models import GameConfig


async def run_with_logging():
    """Run game client for 20 seconds with detailed logging."""
    config = GameConfig()
    game = Game(config)
    
    print("=" * 60)
    print("Starting 20-second test run")
    print("=" * 60)
    
    # Initialize
    if not await game.initialize():
        print("Failed to initialize")
        return 1
    
    start_time = time.time()
    frame_count = 0
    snapshot_count = 0
    last_snapshot_pos = None
    
    print(f"\nStarted at {time.strftime('%H:%M:%S')}")
    print("You have 20 seconds to move around!")
    print("Press WASD to move, F1 for debug, ESC to quit early\n")
    
    game._running = True
    
    try:
        while game._running and (time.time() - start_time) < 20:
            # Handle input
            game._running = game._input.process_events()
            
            if game._input.is_quit_requested():
                break
            
            # Update
            game._update()
            
            # Track snapshot changes
            if game._room_snapshot and game._client.player_id in game._room_snapshot.players:
                my_state = game._room_snapshot.players[game._client.player_id]
                current_pos = (my_state.x, my_state.y)
                
                if last_snapshot_pos != current_pos:
                    snapshot_count += 1
                    last_snapshot_pos = current_pos
                    if snapshot_count % 10 == 0:
                        print(f"  Server position update #{snapshot_count}: ({my_state.x:.2f}, {my_state.y:.2f})")
            
            # Render
            game._render()
            
            # Cap FPS
            game._clock.tick(60)
            frame_count += 1
            
            # Allow async tasks
            await asyncio.sleep(0)
    
    except KeyboardInterrupt:
        print("\nInterrupted")
    
    elapsed = time.time() - start_time
    print(f"\n{'=' * 60}")
    print(f"Test complete!")
    print(f"Duration: {elapsed:.1f}s")
    print(f"Frames: {frame_count}")
    print(f"Server position updates: {snapshot_count}")
    
    if game._snapshot_times:
        avg_interval = sum(game._snapshot_times) / len(game._snapshot_times)
        print(f"Avg snapshot interval: {avg_interval:.1f}ms")
        print(f"Tick rate: {1000/avg_interval:.0f}Hz")
    
    print(f"{'=' * 60}\n")
    
    # Cleanup
    await game._cleanup()
    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(run_with_logging())
    sys.exit(exit_code)
