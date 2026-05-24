"""Input handling for keyboard and mouse."""

import pygame
from typing import Callable, Optional


class InputHandler:
    """Handles all input events and keyboard state."""
    
    def __init__(self):
        self._quit_requested = False
        self._toggle_debug = False
        self._toggle_help = False
        self._spawn_bot_requested = False
        self._current_input = (0.0, 0.0)
        
        # Callbacks
        self.on_spawn_bot: Optional[Callable[[], None]] = None
    
    def process_events(self) -> bool:
        """Process pygame events. Returns False if quit requested."""
        self._toggle_debug = False
        self._toggle_help = False
        self._spawn_bot_requested = False
        
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self._quit_requested = True
            elif event.type == pygame.KEYDOWN:
                self._handle_keydown(event.key)
        
        # Process continuous keyboard state for movement
        self._update_movement_input()
        
        return not self._quit_requested
    
    def _handle_keydown(self, key: int):
        """Handle single key press events."""
        if key == pygame.K_ESCAPE:
            self._quit_requested = True
        elif key == pygame.K_F1:
            self._toggle_debug = True
        elif key == pygame.K_h:
            self._toggle_help = True
        elif key == pygame.K_b:
            self._spawn_bot_requested = True
            if self.on_spawn_bot:
                self.on_spawn_bot()
    
    def _update_movement_input(self):
        """Update continuous movement input from keyboard state."""
        keys = pygame.key.get_pressed()
        
        input_x = 0.0
        input_y = 0.0
        
        # Horizontal movement
        if keys[pygame.K_LEFT] or keys[pygame.K_a]:
            input_x = -1.0
        elif keys[pygame.K_RIGHT] or keys[pygame.K_d]:
            input_x = 1.0
        
        # Vertical movement
        if keys[pygame.K_UP] or keys[pygame.K_w]:
            input_y = -1.0
        elif keys[pygame.K_DOWN] or keys[pygame.K_s]:
            input_y = 1.0
        
        self._current_input = (input_x, input_y)
    
    def get_movement_input(self) -> tuple[float, float]:
        """Get current movement input vector."""
        return self._current_input
    
    def should_toggle_debug(self) -> bool:
        """Check if debug toggle was requested this frame."""
        return self._toggle_debug
    
    def should_toggle_help(self) -> bool:
        """Check if help toggle was requested this frame."""
        return self._toggle_help
    
    def is_quit_requested(self) -> bool:
        """Check if quit was requested."""
        return self._quit_requested
