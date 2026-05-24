#!/usr/bin/env python3
"""Entry point for the DungeonGame Pygame Client."""

import asyncio
import sys

from models import GameConfig
from game import Game


def parse_args() -> GameConfig:
    """Parse command line arguments."""
    config = GameConfig()
    
    if len(sys.argv) > 1:
        config.server_address = sys.argv[1]
    
    return config


async def main() -> int:
    """Main entry point."""
    config = parse_args()
    game = Game(config)
    return await game.run()


if __name__ == "__main__":
    try:
        exit_code = asyncio.run(main())
        sys.exit(exit_code)
    except Exception as e:
        print(f"Fatal error: {e}")
        sys.exit(1)
