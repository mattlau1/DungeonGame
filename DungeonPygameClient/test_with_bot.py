"""Test with auto bot spawn."""

import asyncio
import sys
sys.path.insert(0, '.')

from game import Game
from models import GameConfig


async def test_with_bot():
    """Run game with auto bot spawn."""
    config = GameConfig()
    game = Game(config)
    
    # Initialize (connect and spawn player)
    if not await game.initialize():
        print("Failed to initialize")
        return 1
    
    print("Game initialized. Spawning bot in 2 seconds...")
    await asyncio.sleep(2)
    
    # Spawn a bot
    print("Spawning bot...")
    game._spawn_bot()
    
    # Let it run for 5 seconds
    print("Running for 5 seconds with bot...")
    await asyncio.sleep(5)
    
    # Check bot count
    print(f"\nBot count: {len(game._simulated_players)}")
    for bot in game._simulated_players:
        print(f"  - {bot.name}: player_id={bot.player_id}")
    
    # Cleanup
    await game._cleanup()
    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(test_with_bot())
    sys.exit(exit_code)
