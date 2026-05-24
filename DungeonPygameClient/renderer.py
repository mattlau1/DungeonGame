"""Rendering logic for the game."""

import time
from typing import Optional, Set

import pygame

from config import (
    WINDOW_WIDTH, WINDOW_HEIGHT,
    COLOR_BG, COLOR_ROOM_BORDER, COLOR_PLAYER_SELF,
    COLOR_PLAYER_OTHER, COLOR_BOT, COLOR_TEXT,
    COLOR_DEBUG_BG, COLOR_PLAYER_ID, PLAYER_RADIUS
)
from models import RoomState


class Renderer:
    """Handles all rendering of the game."""
    
    def __init__(self, screen: pygame.Surface, scale: float = 15.0):
        self.screen = screen
        self.scale = scale
        self.font = pygame.font.SysFont("monospace", 14)
        self.font_large = pygame.font.SysFont("monospace", 18)
        self.font_title = pygame.font.SysFont("monospace", 24, bold=True)
        
        self.camera_x = 0
        self.camera_y = 0
    
    def set_camera_offset(self, room_width: int, room_height: int):
        """Center the room in the window."""
        room_width_px = room_width * self.scale
        room_height_px = room_height * self.scale
        self.camera_x = (WINDOW_WIDTH - room_width_px) // 2
        self.camera_y = (WINDOW_HEIGHT - room_height_px) // 2
    
    def world_to_screen(self, x: float, y: float) -> tuple[int, int]:
        """Convert world coordinates to screen coordinates."""
        screen_x = int(x * self.scale) + self.camera_x
        screen_y = int(y * self.scale) + self.camera_y
        return (screen_x, screen_y)
    
    def clear(self):
        """Clear the screen."""
        self.screen.fill(COLOR_BG)
    
    def draw_room(self, room: RoomState):
        """Draw room boundary."""
        width = room.width * self.scale
        height = room.height * self.scale
        
        room_rect = pygame.Rect(self.camera_x, self.camera_y, width, height)
        pygame.draw.rect(self.screen, COLOR_ROOM_BORDER, room_rect, 3)
        
        # Room info label
        room_text = f"Room {room.room_id} ({room.width}x{room.height})"
        text_surface = self.font.render(room_text, True, COLOR_TEXT)
        self.screen.blit(text_surface, (self.camera_x, self.camera_y - 20))
    
    def draw_players(self, room: RoomState, my_player_id: Optional[int], bot_ids: Set[int], 
                     my_render_pos: Optional[tuple[float, float]] = None,
                     my_server_pos: Optional[tuple[float, float]] = None):
        """Draw all players as circles with ID labels.
        
        Args:
            room: Current room state from server
            my_player_id: This client's player ID
            bot_ids: Set of bot player IDs
            my_render_pos: Render position (predicted, smoothly interpolated toward server)
            my_server_pos: Server-authoritative position (for ghost rendering)
        """
        # Draw server ghost first (so it appears behind the player)
        if my_player_id and my_server_pos:
            ghost_screen_pos = self.world_to_screen(my_server_pos[0], my_server_pos[1])
            # Draw larger semi-transparent ghost (trails behind)
            ghost_surface = pygame.Surface((PLAYER_RADIUS * 3, PLAYER_RADIUS * 3), pygame.SRCALPHA)
            # Brighter red/pink ghost with higher alpha
            pygame.draw.circle(ghost_surface, (255, 80, 80, 200), 
                             (PLAYER_RADIUS * 1.5, PLAYER_RADIUS * 1.5), PLAYER_RADIUS + 3)
            self.screen.blit(ghost_surface, 
                           (ghost_screen_pos[0] - PLAYER_RADIUS * 1.5, ghost_screen_pos[1] - PLAYER_RADIUS * 1.5))
            # Ghost label with background
            ghost_label = self.font.render("SERVER", True, (255, 100, 100))
            label_bg = pygame.Surface((ghost_label.get_width() + 4, ghost_label.get_height() + 2), pygame.SRCALPHA)
            label_bg.fill((0, 0, 0, 150))
            self.screen.blit(label_bg, (ghost_screen_pos[0] - ghost_label.get_width()//2 - 2, 
                                       ghost_screen_pos[1] + PLAYER_RADIUS + 8))
            self.screen.blit(ghost_label, 
                          (ghost_screen_pos[0] - ghost_label.get_width()//2, 
                           ghost_screen_pos[1] + PLAYER_RADIUS + 8))
        
        for player_id, player in room.players.items():
            # Use render position for local player (smoothly interpolated)
            if player_id == my_player_id and my_render_pos:
                x, y = my_render_pos
            else:
                x, y = player.x, player.y
            
            screen_pos = self.world_to_screen(x, y)
            
            # Choose color based on player type
            if player_id == my_player_id:
                color = COLOR_PLAYER_SELF  # Green
            elif player_id in bot_ids:
                color = COLOR_BOT  # Orange
            else:
                color = COLOR_PLAYER_OTHER  # Blue
            
            # Draw circle
            pygame.draw.circle(self.screen, color, screen_pos, PLAYER_RADIUS)
            pygame.draw.circle(self.screen, (255, 255, 255), screen_pos, PLAYER_RADIUS, 2)
            
            # Draw player ID label
            id_text = f"P{player_id}"
            text_surface = self.font.render(id_text, True, COLOR_PLAYER_ID)
            text_rect = text_surface.get_rect(center=(screen_pos[0], screen_pos[1] - PLAYER_RADIUS - 10))
            self.screen.blit(text_surface, text_rect)
    
    def draw_latency_graph(self, latency_history: list[float], current_latency: float):
        """Draw a small latency graph in the bottom right corner."""
        if not latency_history:
            return
            
        # Graph dimensions
        graph_width = 200
        graph_height = 60
        graph_x = WINDOW_WIDTH - graph_width - 10
        graph_y = WINDOW_HEIGHT - graph_height - 30
        
        # Draw background
        graph_bg = pygame.Surface((graph_width, graph_height), pygame.SRCALPHA)
        graph_bg.fill((0, 0, 0, 180))
        self.screen.blit(graph_bg, (graph_x, graph_y))
        
        # Draw border
        pygame.draw.rect(self.screen, (100, 100, 100), 
                        (graph_x, graph_y, graph_width, graph_height), 1)
        
        # Calculate scale (max 200ms)
        max_latency = max(200, max(latency_history) if latency_history else 200)
        
        # Draw grid lines
        for i in range(1, 4):
            y = graph_y + graph_height - (i * graph_height // 4)
            pygame.draw.line(self.screen, (50, 50, 50), 
                           (graph_x, y), (graph_x + graph_width, y))
        
        # Draw latency line
        if len(latency_history) > 1:
            points = []
            for i, latency in enumerate(latency_history):
                x = graph_x + (i / (len(latency_history) - 1)) * graph_width
                y = graph_y + graph_height - (latency / max_latency) * graph_height
                points.append((x, y))
            
            if len(points) > 1:
                pygame.draw.lines(self.screen, (0, 255, 0), False, points, 2)
        
        # Draw current latency value
        latency_text = f"{current_latency:.0f}ms"
        text_surface = self.font.render(latency_text, True, (0, 255, 0))
        self.screen.blit(text_surface, (graph_x + 5, graph_y - 15))
    
    def draw_debug_info(self, 
                       server_address: str,
                       connected: bool,
                       my_player_id: Optional[int],
                       room: Optional[RoomState],
                       input_vector: tuple[float, float],
                       sequence: int,
                       fps: int,
                       last_snapshot_time: float,
                       bot_count: int,
                       show_help: bool,
                       render_pos: Optional[tuple[float, float]] = None,
                       drift: float = 0.0,
                       latency: float = 0.0,
                       latency_history: Optional[list[float]] = None,
                       snapshot_intervals: Optional[list[float]] = None):
        """Draw debug overlay panel."""
        lines = []
        
        lines.append(f"Server: {server_address}")
        lines.append(f"Connected: {connected}")
        
        if my_player_id:
            lines.append(f"Player ID: {my_player_id}")
        
        if room:
            lines.append(f"Room ID: {room.room_id}")
            lines.append(f"Room Size: {room.width}x{room.height}")
            lines.append(f"Players: {len(room.players)}")
        
        lines.append(f"Bots: {bot_count}")
        lines.append(f"Input: ({input_vector[0]:.1f}, {input_vector[1]:.1f})")
        lines.append(f"Sequence: {sequence}")
        lines.append(f"FPS: {fps}")
        
        if last_snapshot_time > 0:
            snapshot_age = (time.time() - last_snapshot_time) * 1000
            lines.append(f"Snapshot Age: {snapshot_age:.0f}ms")
        
        if snapshot_intervals and len(snapshot_intervals) > 1:
            avg_interval = sum(snapshot_intervals) / len(snapshot_intervals)
            tick_rate = 1000 / avg_interval if avg_interval > 0 else 0
            lines.append(f"Tick Rate: {tick_rate:.0f}Hz ({avg_interval:.1f}ms)")
        
        if render_pos:
            lines.append(f"Render: ({render_pos[0]:.1f}, {render_pos[1]:.1f})")
        
        if drift > 0.01:
            lines.append(f"Drift: {drift:.2f}")
        
        lines.append(f"Ping: {latency:.0f}ms ({len(latency_history) if latency_history else 0} samples)")
        
        # Draw background panel
        panel_height = len(lines) * 18 + 10
        panel = pygame.Surface((280, panel_height), pygame.SRCALPHA)
        panel.fill(COLOR_DEBUG_BG)
        self.screen.blit(panel, (10, 10))
        
        # Draw text
        for i, line in enumerate(lines):
            text_surface = self.font.render(line, True, COLOR_TEXT)
            self.screen.blit(text_surface, (15, 15 + i * 18))
        
        # Draw help hint
        hint = "Press H for help" if not show_help else "Press H to close help"
        hint_surface = self.font.render(hint, True, (150, 150, 150))
        self.screen.blit(hint_surface, (15, 15 + len(lines) * 18 + 5))
    
    def draw_help_menu(self):
        """Draw help menu overlay showing all controls."""
        # Semi-transparent overlay
        overlay = pygame.Surface((WINDOW_WIDTH, WINDOW_HEIGHT), pygame.SRCALPHA)
        overlay.fill((0, 0, 0, 200))
        self.screen.blit(overlay, (0, 0))
        
        # Title
        title = self.font_title.render("Controls", True, COLOR_TEXT)
        title_rect = title.get_rect(center=(WINDOW_WIDTH // 2, 120))
        self.screen.blit(title, title_rect)
        
        # Control groups
        controls = [
            ("Movement", [
                "W / UP Arrow - Move up",
                "A / LEFT Arrow - Move left",
                "S / DOWN Arrow - Move down",
                "D / RIGHT Arrow - Move right",
            ]),
            ("Actions", [
                "B - Spawn simulated player (bot)",
            ]),
            ("UI", [
                "F1 - Toggle debug overlay",
                "H - Toggle this help menu",
                "ESC - Quit game",
            ]),
        ]
        
        y_offset = 180
        for group_name, items in controls:
            # Group header
            header = self.font_large.render(group_name, True, COLOR_PLAYER_SELF)
            self.screen.blit(header, (WINDOW_WIDTH // 2 - 200, y_offset))
            y_offset += 30
            
            # Items
            for item in items:
                text = self.font.render(item, True, COLOR_TEXT)
                self.screen.blit(text, (WINDOW_WIDTH // 2 - 180, y_offset))
                y_offset += 22
            
            y_offset += 15  # Space between groups
        
        # Legend
        y_offset += 20
        legend_items = [
            (COLOR_PLAYER_SELF, "You"),
            (COLOR_BOT, "Simulated Player (Bot)"),
            (COLOR_PLAYER_OTHER, "Other Human Player"),
        ]
        
        for color, label in legend_items:
            pygame.draw.circle(self.screen, color, (WINDOW_WIDTH // 2 - 100, y_offset + 8), 8)
            pygame.draw.circle(self.screen, (255, 255, 255), (WINDOW_WIDTH // 2 - 100, y_offset + 8), 8, 2)
            text = self.font.render(label, True, COLOR_TEXT)
            self.screen.blit(text, (WINDOW_WIDTH // 2 - 80, y_offset))
            y_offset += 25
        
        # Footer
        footer = self.font.render("Press H or ESC to close", True, (150, 150, 150))
        footer_rect = footer.get_rect(center=(WINDOW_WIDTH // 2, WINDOW_HEIGHT - 50))
        self.screen.blit(footer, footer_rect)
    
    def draw_instructions(self):
        """Draw control instructions and ping at bottom."""
        # Show ping if available
        # Get latency from game if available (we'll store it in a class variable)
        instructions = "WASD: Move | B: Spawn Bot | H: Help | F1: Debug"
        text_surface = self.font.render(instructions, True, (180, 180, 180))
        text_rect = text_surface.get_rect(center=(WINDOW_WIDTH // 2, WINDOW_HEIGHT - 20))
        self.screen.blit(text_surface, text_rect)
    
    def draw_ping(self, latency: float):
        """Draw ping in top right corner."""
        if latency > 0:
            # Color based on latency
            if latency < 50:
                color = (0, 255, 0)  # Green - good
            elif latency < 100:
                color = (255, 255, 0)  # Yellow - ok
            else:
                color = (255, 0, 0)  # Red - bad
            
            ping_text = f"{latency:.0f}ms"
            text_surface = self.font.render(ping_text, True, color)
            # Draw in top right
            self.screen.blit(text_surface, (WINDOW_WIDTH - text_surface.get_width() - 10, 10))
        else:
            # Show connecting if no latency yet
            text_surface = self.font.render("--ms", True, (150, 150, 150))
            self.screen.blit(text_surface, (WINDOW_WIDTH - text_surface.get_width() - 10, 10))
    
    def flip(self):
        """Flip the display buffer."""
        pygame.display.flip()
