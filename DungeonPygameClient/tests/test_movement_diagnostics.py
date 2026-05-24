"""Diagnostic tests to identify movement issues."""

import pytest
from player_controller import PlayerController
from models import RoomState, PlayerState


class TestMovementDiagnostics:
    """Diagnostic tests to find the root cause of movement issues."""
    
    @pytest.fixture
    def room(self):
        return RoomState(1, 40, 40, "COMBAT", {})
    
    def test_step_by_step_movement(self, room):
        """Test each movement step in detail."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        print("\n=== Step by Step Movement ===")
        print(f"Initial: {controller.get_render_position()}")
        
        # Step 1: Calculate input
        input_x, input_y = controller.calculate_movement_input(1.0, 0.0)
        print(f"Input: ({input_x}, {input_y})")
        
        # Step 2: Update
        controller.update(1.0, 0.0, 1/60)
        print(f"After 1 frame: {controller.get_render_position()}")
        
        # Check values
        render_pos = controller.get_render_position()
        assert render_pos[0] > 20.0, "Should have moved right"
        
    def test_reconciliation_amount(self, room):
        """Test that small drift doesn't cause visible reconciliation (instant response)."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Move for 10 frames
        for i in range(10):
            controller.update(1.0, 0.0, i * 0.016)
        
        predicted_pos = controller.get_render_position()
        print(f"\n=== Reconciliation Test ===")
        print(f"Predicted position: {predicted_pos}")
        
        # Server says we're at a different position (small drift < 5 units)
        server_pos = PlayerState(1, 1, 22.0, 20.0, True)  # Small drift
        controller.on_server_update(server_pos, 0.16)
        
        # One frame of update
        controller.update(0.0, 0.0, 0.176)
        
        new_render_pos = controller.get_render_position()
        print(f"After server update: {new_render_pos}")
        
        movement = abs(new_render_pos[0] - predicted_pos[0])
        print(f"Movement from reconciliation: {movement}")
        
        # For small drift, render should NOT move (instant response)
        assert movement == 0.0, f"Small drift should not cause reconciliation: {movement}"
    
    def test_drift_calculation(self, room):
        """Test drift is calculated correctly."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Move predicted only
        controller.update(1.0, 0.0, 0.016)
        
        # Server says stay at origin
        server_pos = PlayerState(1, 1, 20.0, 20.0, True)
        controller.on_server_update(server_pos, 0.016)
        
        drift = controller.get_drift()
        print(f"\n=== Drift Test ===")
        print(f"Drift: {drift}")
        
        # Should be positive since we moved
        assert drift > 0, "Drift should be positive after movement"
    
    def test_convergence_rate(self, room):
        """Test that drift does NOT converge (pure prediction, no reconciliation)."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Create large drift (> 5 units)
        for i in range(30):
            controller.update(1.0, 0.0, i * 0.016)
        
        server_pos = PlayerState(1, 1, 20.0, 20.0, True)
        controller.on_server_update(server_pos, 0.48)
        
        initial_drift = controller.get_drift()
        print(f"\n=== No Convergence Test ===")
        print(f"Initial drift: {initial_drift}")
        
        # Drift should be large (> 5)
        assert initial_drift > 5.0, f"Need large drift for this test: {initial_drift}"
        
        # Run many updates - drift should stay the same (no reconciliation)
        for i in range(300):
            controller.update(0.0, 0.0, 0.48 + i * 0.016)
        
        final_drift = controller.get_drift()
        print(f"Drift after 300 frames: {final_drift}")
        
        # Drift should stay the same (pure prediction, no convergence)
        assert abs(final_drift - initial_drift) < 0.1, f"Drift should not change: {final_drift} vs {initial_drift}"
    
    def test_render_vs_predicted(self, room):
        """Test that render position follows predicted but with smoothing."""
        controller = PlayerController(1, room, 20.0, 20.0)
        
        # Check initial
        render = controller.get_render_position()
        print(f"\n=== Render vs Predicted ===")
        print(f"Initial render: {render}")
        
        # Move for several frames
        for i in range(10):
            controller.update(1.0, 0.0, 1/60)
            render = controller.get_render_position()
            print(f"Frame {i+1}: render={render}")
        
        # Should have moved
        assert render[0] > 20.0, "Render position should have moved"


class TestCoordinateSystem:
    """Test coordinate system understanding."""
    
    def test_room_origin(self):
        """Test room origin is (0,0) top-left."""
        room = RoomState(1, 40, 40, "COMBAT", {})
        
        # (0,0) should be valid but at edge
        assert not room.is_out_of_bounds(0.0, 0.0)
        
        # Negative should be out of bounds
        assert room.is_out_of_bounds(-1.0, 0.0)
        assert room.is_out_of_bounds(0.0, -1.0)
        
        # Beyond width/height should be out of bounds
        assert room.is_out_of_bounds(41.0, 20.0)
        assert room.is_out_of_bounds(20.0, 41.0)


if __name__ == "__main__":
    pytest.main([__file__, "-v", "-s"])
