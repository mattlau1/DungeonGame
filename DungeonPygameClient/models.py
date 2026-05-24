"""Data models for the game client."""

from dataclasses import dataclass
from typing import Optional


@dataclass
class PlayerState:
    """Local representation of a player."""
    id: int
    room_id: int
    x: float
    y: float
    is_online: bool


@dataclass
class RoomState:
    """Local representation of room state."""
    room_id: int
    width: int
    height: int
    room_type: str
    players: dict[int, PlayerState]
    
    def clamp_position(self, x: float, y: float) -> tuple[float, float]:
        """Clamp coordinates within room boundaries.
        
        Returns:
            Tuple of (clamped_x, clamped_y)
        """
        clamped_x = max(0.0, min(float(x), float(self.width)))
        clamped_y = max(0.0, min(float(y), float(self.height)))
        return (clamped_x, clamped_y)
    
    def is_out_of_bounds(self, x: float, y: float) -> bool:
        """Check if position is outside room boundaries."""
        return x < 0 or x > self.width or y < 0 or y > self.height


@dataclass
class GameConfig:
    """Game configuration."""
    server_address: str = "localhost:5142"
    scale: float = 15.0
    show_debug: bool = True
    
    def copy(self) -> 'GameConfig':
        """Create a copy of this configuration."""
        return GameConfig(
            server_address=self.server_address,
            scale=self.scale,
            show_debug=self.show_debug
        )
