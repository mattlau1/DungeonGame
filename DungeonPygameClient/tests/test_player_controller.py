"""Unit tests for PlayerController."""

import pytest
import math
from player_controller import PlayerController
from models import RoomState, PlayerState


class TestPlayerController:
    """Test suite for PlayerController."""
    
    @pytest.fixture
    def room(self):
        """Create a standard test room."""
        return RoomState(
            room_id=1,
            width=40,
            height=40,
            room_type="COMBAT",
            players={}
        )
    
    @pytest.fixture
    def controller(self, room):
        """Create a player controller at center of room."""
        return PlayerController(
            player_id=1,
            room=room,
            initial_x=20.0,
            initial_y=20.0
        )
    
    def test_initial_state(self, controller):
        """Test controller initializes correctly."""
        render_x, render_y = controller.get_render_position()
        assert render_x == 20.0
        assert render_y == 20.0
        assert controller.get_drift() == 0.0
    
    def test_movement_input_basic(self, controller):
        """Test basic movement calculation."""
        # Move right
        input_x, input_y = controller.calculate_movement_input(1.0, 0.0)
        assert input_x > 0  # Should move right
        assert input_y == 0  # No vertical movement
        assert abs(input_x - 0.3) < 0.01  # Should be ~0.3 (movement distance)
    
    def test_boundary_clamping_right(self, room):
        """Test that movement is clamped at right boundary."""
        # Start near right edge
        controller = PlayerController(1, room, initial_x=38.0, initial_y=20.0)
        
        # Try to move right (should be clamped)
        input_x, input_y = controller.calculate_movement_input(1.0, 0.0)
        
        # Should only move to margin boundary (39.0), not beyond
        assert input_x < 0.3  # Less than full movement
        assert input_x > 0  # But still some movement
    
    def test_boundary_clamping_left(self, room):
        """Test that movement is clamped at left boundary."""
        # Start near left edge
        controller = PlayerController(1, room, initial_x=2.0, initial_y=20.0)
        
        # Try to move left (should be clamped)
        input_x, input_y = controller.calculate_movement_input(-1.0, 0.0)
        
        # Should only move to margin boundary (1.0), not beyond
        # Use approximate comparison for floating point
        assert input_x >= -0.3 - 0.01  # Less than or equal to full movement (negative)
        assert input_x < 0  # But still some movement
    
    def test_update_moves_predicted(self, controller):
        """Test that update() moves predicted position."""
        initial_render = controller.get_render_position()
        
        # Move right
        controller.update(1.0, 0.0, 0.016)  # 60 FPS frame
        
        new_render = controller.get_render_position()
        assert new_render[0] > initial_render[0]  # X increased
    
    def test_server_reconciliation(self, controller):
        """Test that server position is tracked but doesn't affect render (instant response)."""
        # Start at (20, 20)
        controller.update(1.0, 0.0, 0.016)  # Move right a bit
        
        initial_render = controller.get_render_position()
        
        # Server says we're at a different position
        server_state = PlayerState(
            id=1,
            room_id=1,
            x=19.5,  # Slightly left
            y=20.0,
            is_online=True
        )
        
        controller.on_server_update(server_state, 1.0)
        
        # Render position should NOT change (instant response, no reconciliation)
        final_render = controller.get_render_position()
        assert final_render[0] == initial_render[0], "Render should not reconcile"
        
        # But server position should be tracked
        server_pos = controller.get_server_position()
        assert server_pos[0] == 19.5, "Server position should be tracked"
    
    def test_large_drift_snap(self, controller):
        """Test that huge drift causes snap to server."""
        # Move predicted far from server
        controller.update(1.0, 0.0, 1.0)  # 1 second of movement
        controller.update(1.0, 0.0, 1.0)
        
        # Server says we're back at spawn (huge drift)
        server_state = PlayerState(
            id=1,
            room_id=1,
            x=20.0,
            y=20.0,
            is_online=True
        )
        
        # Apply with timestamp
        controller.on_server_update(server_state, 2.0)
        
        # Update to trigger reconciliation
        controller.update(0.0, 0.0, 2.016)
        
        # Should have snapped close to server (drift > 5.0 causes snap)
        drift = controller.get_drift()
        assert drift < 5.0
    
    def test_diagonal_movement(self, controller):
        """Test diagonal movement input."""
        input_x, input_y = controller.calculate_movement_input(1.0, 1.0)
        
        # Both should be non-zero
        assert input_x > 0
        assert input_y > 0
        # Movement should be similar magnitude
        assert abs(abs(input_x) - abs(input_y)) < 0.01
    
    def test_no_movement_on_zero_input(self, controller):
        """Test that zero input produces no movement."""
        initial = controller.get_render_position()
        
        input_x, input_y = controller.calculate_movement_input(0.0, 0.0)
        
        assert input_x == 0.0
        assert input_y == 0.0


class TestRoomState:
    """Test RoomState helper methods."""
    
    def test_clamp_position_inside(self):
        """Test clamping a position inside bounds."""
        room = RoomState(1, 40, 40, "COMBAT", {})
        x, y = room.clamp_position(20.0, 20.0)
        assert x == 20.0
        assert y == 20.0
    
    def test_clamp_position_outside(self):
        """Test clamping a position outside bounds."""
        room = RoomState(1, 40, 40, "COMBAT", {})
        x, y = room.clamp_position(50.0, -10.0)
        assert x == 40.0  # Clamped to width
        assert y == 0.0   # Clamped to 0
    
    def test_is_out_of_bounds(self):
        """Test out of bounds detection."""
        room = RoomState(1, 40, 40, "COMBAT", {})
        assert not room.is_out_of_bounds(20.0, 20.0)
        assert room.is_out_of_bounds(50.0, 20.0)
        assert room.is_out_of_bounds(20.0, -5.0)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
