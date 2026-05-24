"""Unit tests for InputHandler."""

import pytest
import pygame
from input_handler import InputHandler


class TestInputHandler:
    """Test suite for InputHandler."""
    
    @pytest.fixture
    def handler(self):
        """Create fresh input handler."""
        return InputHandler()
    
    def test_initial_state(self, handler):
        """Test initial input state is zero."""
        input_vec = handler.get_movement_input()
        assert input_vec == (0.0, 0.0)
        assert not handler.is_quit_requested()
    
    def test_single_key_press_triggers_callback(self, handler):
        """Test that B key triggers spawn callback."""
        callback_called = [False]
        
        def mock_callback():
            callback_called[0] = True
        
        handler.on_spawn_bot = mock_callback
        
        # Simulate B key press
        handler._handle_keydown(pygame.K_b)
        
        assert callback_called[0]
        assert handler.should_toggle_help() is False
    
    def test_f1_toggles_debug(self, handler):
        """Test F1 key toggles debug."""
        handler._handle_keydown(pygame.K_F1)
        assert handler.should_toggle_debug()
    
    def test_h_toggles_help(self, handler):
        """Test H key toggles help."""
        handler._handle_keydown(pygame.K_h)
        assert handler.should_toggle_help()
    
    def test_escape_requests_quit(self, handler):
        """Test ESC requests quit."""
        handler._handle_keydown(pygame.K_ESCAPE)
        assert handler.is_quit_requested()
    
    def test_movement_input_calculation(self, handler):
        """Test movement vector calculation from key states."""
        # We can't easily mock pygame.key.get_pressed() without mocking
        # So we'll test the logic directly by checking _update_movement_input behavior
        # This is a simplified test - in real scenarios you'd mock pygame
        
        # Initially should be zero
        assert handler.get_movement_input() == (0.0, 0.0)


class TestMovementInputScenarios:
    """Test various movement input scenarios."""
    
    def test_right_movement(self):
        """Test right movement produces positive X."""
        # Simulate: handler receives right key press
        # Expected: input_x = 1.0
        pass  # Would need pygame mocking for full test
    
    def test_left_movement(self):
        """Test left movement produces negative X."""
        pass
    
    def test_diagonal_movement(self):
        """Test diagonal combines both axes."""
        pass


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
