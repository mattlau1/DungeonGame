"""pytest configuration."""

import pytest
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))


# Mock pygame for headless testing
class MockPygame:
    """Mock pygame for testing without display."""
    
    K_UP = 273
    K_DOWN = 274
    K_LEFT = 276
    K_RIGHT = 275
    K_w = 119
    K_a = 97
    K_s = 115
    K_d = 100
    K_ESCAPE = 27
    K_F1 = 282
    K_h = 104
    K_b = 98
    QUIT = 12
    KEYDOWN = 2
    
    @staticmethod
    def init():
        pass
    
    @staticmethod
    def quit():
        pass


@pytest.fixture(autouse=True)
def mock_pygame(monkeypatch):
    """Automatically mock pygame for all tests."""
    monkeypatch.setattr("pygame.K_UP", MockPygame.K_UP)
    monkeypatch.setattr("pygame.K_DOWN", MockPygame.K_DOWN)
    monkeypatch.setattr("pygame.K_LEFT", MockPygame.K_LEFT)
    monkeypatch.setattr("pygame.K_RIGHT", MockPygame.K_RIGHT)
    monkeypatch.setattr("pygame.K_w", MockPygame.K_w)
    monkeypatch.setattr("pygame.K_a", MockPygame.K_a)
    monkeypatch.setattr("pygame.K_s", MockPygame.K_s)
    monkeypatch.setattr("pygame.K_d", MockPygame.K_d)
    monkeypatch.setattr("pygame.K_ESCAPE", MockPygame.K_ESCAPE)
    monkeypatch.setattr("pygame.K_F1", MockPygame.K_F1)
    monkeypatch.setattr("pygame.K_h", MockPygame.K_h)
    monkeypatch.setattr("pygame.K_b", MockPygame.K_b)
    monkeypatch.setattr("pygame.QUIT", MockPygame.QUIT)
    monkeypatch.setattr("pygame.KEYDOWN", MockPygame.KEYDOWN)
    monkeypatch.setattr("pygame.init", MockPygame.init)
    monkeypatch.setattr("pygame.quit", MockPygame.quit)
