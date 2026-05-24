"""Player controller with client-side prediction, smooth reconciliation, and latency tracking."""

import logging
import math
import time
from collections import deque
from typing import Optional
from models import RoomState, PlayerState

logger = logging.getLogger("PlayerController")


class PlayerController:
    """Manages local player state with prediction, smooth reconciliation, and latency tracking."""
    
    def __init__(self, player_id: int, room: RoomState, initial_x: float = 0.0, initial_y: float = 0.0):
        self.player_id = player_id
        self._room = room
        
        # Server authoritative position (for ghost rendering)
        self._server_x = initial_x
        self._server_y = initial_y
        
        # Ghost interpolated position (smoothly converges to server position)
        self._ghost_x = initial_x
        self._ghost_y = initial_y
        self._ghost_interpolation_speed = 8.0
        
        # Predicted position (client-side)
        self._predicted_x = initial_x
        self._predicted_y = initial_y
        
        # Render position
        self._render_x = initial_x
        self._render_y = initial_y
        
        # Margin from room edges
        self._margin = 1.0
        
        # Movement step size (units per input)
        self._movement_distance = 0.3
        
        # Track server update timestamp for reconciliation
        self._last_server_update_time = 0.0
        self._reconciliation_delay = 0.1
        
        # Simple latency tracking: measure time between input and next snapshot
        self._last_input_time: Optional[float] = None
        self._latency_history: deque[float] = deque(maxlen=60)
        self._current_latency: float = 0.0
        
    def record_input_sent(self, sequence: int, send_time: float):
        """Record when an input was sent to the server."""
        # Just track the last input time
        self._last_input_time = send_time
        
    def record_snapshot_received(self, sequence: int, receive_time: float):
        """Record when a snapshot was received, calculating latency."""
        if self._last_input_time is not None:
            latency = (receive_time - self._last_input_time) * 1000
            if latency > 0 and latency < 1000:  # Sanity check (0-1000ms)
                self._latency_history.append(latency)
                # Smooth with exponential moving average
                if self._current_latency == 0:
                    self._current_latency = latency
                else:
                    self._current_latency = self._current_latency * 0.9 + latency * 0.1
            
    def get_latency(self) -> float:
        """Get current smoothed latency in milliseconds."""
        return self._current_latency
    
    def get_latency_history(self) -> list[float]:
        """Get latency history for graphing."""
        return list(self._latency_history)
    
    def get_server_position(self) -> tuple[float, float]:
        """Get interpolated ghost position (smoothly converges to server)."""
        return (self._ghost_x, self._ghost_y)
    
    def update_room(self, room: RoomState):
        """Update room reference."""
        self._room = room
    
    def get_render_position(self) -> tuple[float, float]:
        """Get the render position."""
        return (self._render_x, self._render_y)
    
    def get_drift(self) -> float:
        """Get distance between predicted and server positions."""
        dx = self._predicted_x - self._server_x
        dy = self._predicted_y - self._server_y
        return math.sqrt(dx * dx + dy * dy)
    
    def calculate_movement_input(self, input_x: float, input_y: float) -> tuple[float, float]:
        """Calculate movement input with boundary checking."""
        # Calculate desired movement
        desired_delta_x = input_x * self._movement_distance
        desired_delta_y = input_y * self._movement_distance
        
        # Predict new position
        new_x = self._predicted_x + desired_delta_x
        new_y = self._predicted_y + desired_delta_y
        
        # Clamp to boundaries
        clamped_x = max(self._margin, min(new_x, self._room.width - self._margin))
        clamped_y = max(self._margin, min(new_y, self._room.height - self._margin))
        
        # Calculate actual delta after clamping
        actual_delta_x = clamped_x - self._predicted_x
        actual_delta_y = clamped_y - self._predicted_y
        
        return (actual_delta_x, actual_delta_y)
    
    def update(self, input_x: float, input_y: float, current_time: float, delta_time: float = 0.016):
        """Update player state - pure prediction with smooth ghost interpolation."""
        has_input = abs(input_x) > 0.01 or abs(input_y) > 0.01
        
        # 1. Update predicted position based on input
        if has_input:
            adjusted_input = self.calculate_movement_input(input_x, input_y)
            self._predicted_x += adjusted_input[0]
            self._predicted_y += adjusted_input[1]
        
        # 2. Render position = predicted position (ALWAYS instant, NO reconciliation ever)
        self._render_x = self._predicted_x
        self._render_y = self._predicted_y
        
        # 3. Ghost interpolation toward server position
        ghost_lerp_factor = 1.0 - math.exp(-self._ghost_interpolation_speed * delta_time)
        self._ghost_x += (self._server_x - self._ghost_x) * ghost_lerp_factor
        self._ghost_y += (self._server_y - self._ghost_y) * ghost_lerp_factor
        
        # 4. Clamp ghost to room bounds so it never goes off-screen
        self._ghost_x = max(self._margin, min(self._ghost_x, self._room.width - self._margin))
        self._ghost_y = max(self._margin, min(self._ghost_y, self._room.height - self._margin))
    
    def on_server_update(self, server_state: PlayerState, current_time: float):
        if server_state.x < 0 or server_state.x > self._room.width or \
           server_state.y < 0 or server_state.y > self._room.height:
            logger.error(f"[SERVER OOB] Ignoring bad server position: "
                        f"({server_state.x:.2f}, {server_state.y:.2f}) "
                        f"Room: {self._room.width}x{self._room.height}")
            return
        
        self._server_x = server_state.x
        self._server_y = server_state.y
        self._last_server_update_time = current_time
