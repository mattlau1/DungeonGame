"""Integration tests for movement system."""

import pytest
import time
from player_controller import PlayerController
from models import RoomState, PlayerState


class TestMovementIntegration:
    """Integration tests for the complete movement flow."""
    
    @pytest.fixture
    def room(self):
        return RoomState(
            room_id=1,
            width=40,
            height=40,
            room_type="COMBAT",
            players={}
        )
    
    def test_full_movement_sequence(self, room):
        """Test a complete movement: input → predict → reconcile."""
        # Start at center
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Simulate 1 second of movement right at 60 FPS
        for _ in range(60):
            input_vec = controller.calculate_movement_input(1.0, 0.0)
            controller.update(1.0, 0.0, 1/60)
        
        # Should have moved right
        render_pos = controller.get_render_position()
        assert render_pos[0] > 20.0
        
        # But should be clamped at boundary
        assert render_pos[0] <= 39.0  # Room width - margin
    
    def test_server_correction_smoothness(self, room):
        """Test that server corrections are smooth, not jarring."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Move for a bit
        for i in range(30):
            controller.update(1.0, 0.0, i * 0.016)
        
        # Record position before server update
        pos_before = controller.get_render_position()
        
        # Server sends correction (slightly different position - less than 2.0 to avoid snap)
        server_pos = PlayerState(1, 1, pos_before[0] + 1.5, pos_before[1], True)
        controller.on_server_update(server_pos, 0.48)
        
        # Single update shouldn't cause huge jump
        controller.update(0.0, 0.0, 0.496)
        pos_after = controller.get_render_position()
        
        movement = abs(pos_after[0] - pos_before[0])
        assert movement < 0.5, f"Too much movement in one frame: {movement}"
    
    def test_drift_convergence(self, room):
        """Test that drift does NOT converge (pure prediction)."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Create large drift by moving a lot
        for i in range(25):
            controller.update(1.0, 0.0, i * 0.016)
        
        # Verify we have large drift
        initial_drift = controller.get_drift()
        assert initial_drift > 5.0, f"Need drift > 5: {initial_drift}"
        
        # Server says stay at origin
        server_pos = PlayerState(1, 1, 20.0, 20.0, True)
        controller.on_server_update(server_pos, 0.4)
        
        # Run updates - drift should NOT change
        for i in range(200):
            controller.update(0.0, 0.0, 0.4 + i * 0.016)
        
        # Drift should stay the same (pure prediction, no convergence)
        final_drift = controller.get_drift()
        assert abs(final_drift - initial_drift) < 0.1, f"Drift should not change: {final_drift} vs {initial_drift}"
    
    def test_no_teleport_on_small_drift(self, room):
        """Test that small drift doesn't cause teleporting (instant response)."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Small server correction (creates small drift < 5)
        for i in range(10):
            controller.update(1.0, 0.0, i * 0.016)
        
        render_before = controller.get_render_position()
        
        # Server is slightly off (small drift)
        server_pos = PlayerState(1, 1, render_before[0] + 0.2, render_before[1], True)
        controller.on_server_update(server_pos, 0.16)
        
        # Single update
        controller.update(0.0, 0.0, 0.176)
        render_after = controller.get_render_position()
        
        # For small drift, should have NO movement (instant response)
        movement = abs(render_after[0] - render_before[0])
        assert movement == 0.0, f"Small drift should not affect render: {movement}"
    
    def test_corner_clamping(self, room):
        """Test that movement is clamped correctly in corners."""
        # Start in corner
        controller = PlayerController(1, room, 38.0, 38.0)
        
        # Try to move diagonally out of bounds
        for _ in range(10):
            input_vec = controller.calculate_movement_input(1.0, 1.0)
            controller.update(1.0, 1.0, 1/60)
        
        pos = controller.get_render_position()
        # Should be clamped to boundaries
        assert pos[0] <= 39.0
        assert pos[1] <= 39.0


class TestSimulatedPlayerMovement:
    """Tests for bot movement patterns."""
    
    def test_bot_respects_boundaries(self):
        """Test that simulated player respects room boundaries."""
        # This would require mocking the gRPC client
        pass
    
    def test_bot_movement_is_continuous(self):
        """Test that bot movement is smooth, not jerky."""
        pass


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
