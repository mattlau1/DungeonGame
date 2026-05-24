"""Main game orchestrator that coordinates all components."""

import asyncio
import logging
import time
from typing import Optional, Set

import pygame

from config import FPS, WINDOW_WIDTH, WINDOW_HEIGHT, DEFAULT_SCALE, DEFAULT_SERVER_ADDRESS
from models import GameConfig, RoomState
from grpc_client import GrpcClient
from simulated_player import SimulatedPlayer
from input_handler import InputHandler
from renderer import Renderer
from player_controller import PlayerController

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("DungeonGame")


class Game:
    """Main game class that orchestrates all components following SRP."""
    
    def __init__(self, config: GameConfig):
        self.config = config
        
        # Components
        self._client = GrpcClient(config.server_address)
        self._input = InputHandler()
        self._renderer: Optional[Renderer] = None
        
        # Simulated players
        self._simulated_players: list[SimulatedPlayer] = []
        self._next_bot_id = 1
        
        # State
        self._running = False
        self._room_snapshot: Optional[RoomState] = None
        self._last_snapshot_time = 0.0
        self._show_debug = config.show_debug
        self._show_help = False
        self._clock: Optional[pygame.time.Clock] = None
        self._last_update_time = 0.0
        self._frame_interval = 1.0 / FPS
        
        # Tick rate monitoring
        self._snapshot_times: list[float] = []
        
        # Setup callbacks
        self._input.on_spawn_bot = self._spawn_bot
    
    async def initialize(self) -> bool:
        """Initialize pygame and connect to server."""
        # Init pygame
        pygame.init()
        pygame.display.set_caption("DungeonGame Pygame Client")
        screen = pygame.display.set_mode((WINDOW_WIDTH, WINDOW_HEIGHT), pygame.DOUBLEBUF)
        self._clock = pygame.time.Clock()
        self._last_update_time = time.time()
        
        self._renderer = Renderer(screen, scale=self.config.scale)
        
        # Connect to server
        print(f"Connecting to {self.config.server_address}...")
        
        if not await self._client.connect():
            print("Failed to connect to server")
            return False
        
        player = await self._client.spawn_player()
        if not player:
            print("Failed to spawn player")
            return False
        
        # Setup camera
        if self._client.room_info:
            self._renderer.set_camera_offset(
                self._client.room_info.width,
                self._client.room_info.height
            )
        
        # Create initial room state for player controller
        initial_room = RoomState(
            room_id=player.room_id,
            width=self._client.room_info.width if self._client.room_info else 40,
            height=self._client.room_info.height if self._client.room_info else 40,
            room_type="UNKNOWN",
            players={}
        )
        
        # Create player controller with boundary checking
        self._player_controller = PlayerController(
            player_id=player.id,
            room=initial_room,
            initial_x=player.x,
            initial_y=player.y
        )
        
        # Start streaming
        await self._client.start_streams(self._on_snapshot)
        
        print(f"Player {player.id} spawned in room {player.room_id}")
        return True
    
    def _on_snapshot(self, snapshot: RoomState):
        """Handle room snapshot updates."""
        current_time = time.time()
        
        # Track snapshot intervals for tick rate monitoring
        if self._last_snapshot_time > 0:
            interval = (current_time - self._last_snapshot_time) * 1000
            self._snapshot_times.append(interval)
            if len(self._snapshot_times) > 60:
                self._snapshot_times.pop(0)
        
        self._last_snapshot_time = current_time
        self._room_snapshot = snapshot
        
        # DEBUG: Check for out of bounds positions
        if self._client.player_id in snapshot.players:
            my_state = snapshot.players[self._client.player_id]
            if my_state.x < 0 or my_state.x > snapshot.width or \
               my_state.y < 0 or my_state.y > snapshot.height:
                logger.error(f"[SERVER OOB] My position from server: "
                            f"({my_state.x:.2f}, {my_state.y:.2f}) "
                            f"Room: {snapshot.width}x{snapshot.height}")
        
        # Update player controller with server data
        if self._player_controller:
            self._player_controller.update_room(snapshot)
            
            # Notify controller of server position with timestamp
            if self._client.player_id in snapshot.players:
                my_state = snapshot.players[self._client.player_id]
                self._player_controller.on_server_update(my_state, current_time)
                
                # Record latency: time from last input to this snapshot
                self._player_controller.record_snapshot_received(0, current_time)
    
    def _spawn_bot(self):
        """Spawn a new simulated player."""
        # Create bot with copy of config
        bot_config = self.config.copy()
        bot_name = f"Bot{self._next_bot_id}"
        self._next_bot_id += 1
        
        bot = SimulatedPlayer(bot_config.server_address, name=bot_name)
        
        # Add to tracking list immediately (will be populated with player_id when ready)
        self._simulated_players.append(bot)
        
        # Start bot asynchronously
        asyncio.create_task(self._start_bot(bot))
    
    async def _start_bot(self, bot: SimulatedPlayer):
        """Start a bot and track it."""
        success = await bot.start()
        if success:
            print(f"Spawned simulated player: {bot.name} (ID: {bot.player_id})")
        else:
            print(f"Failed to spawn simulated player: {bot.name}")
            # Remove from tracking if failed
            if bot in self._simulated_players:
                self._simulated_players.remove(bot)
    
    def _get_bot_ids(self) -> Set[int]:
        """Get set of all bot player IDs."""
        return {bot.player_id for bot in self._simulated_players if bot.player_id}
    
    def _update(self):
        """Update game state with smooth prediction and reconciliation."""
        # Calculate delta time
        current_time = time.time()
        delta_time = current_time - self._last_update_time
        self._last_update_time = current_time
        
        # Get input
        input_vector = self._input.get_movement_input()
        
        # Update player controller (handles prediction + smooth reconciliation)
        if self._player_controller:
            # Calculate input to send to server
            adjusted_input = self._player_controller.calculate_movement_input(
                input_vector[0], input_vector[1]
            )
            
            # Send to server (always, for latency tracking even if zero)
            self._client.send_input(adjusted_input[0], adjusted_input[1])
            
            # Record input time for latency tracking (always record when we send)
            if self._player_controller:
                self._player_controller.record_input_sent(self._client.get_sequence(), current_time)
            
            # Update local prediction and reconciliation (pass current time and delta)
            self._player_controller.update(input_vector[0], input_vector[1], current_time, delta_time)
        else:
            self._client.send_input(input_vector[0], input_vector[1])
        
        # Toggle debug if requested
        if self._input.should_toggle_debug():
            self._show_debug = not self._show_debug
        
        # Toggle help if requested
        if self._input.should_toggle_help():
            self._show_help = not self._show_help
    
    def _render(self):
        """Render the game frame."""
        self._renderer.clear()
        
        if self._room_snapshot:
            self._renderer.draw_room(self._room_snapshot)
            
            # Get positions for rendering
            render_pos = None
            server_pos = None
            latency_history = []
            current_latency = 0.0
            
            my_id = self._client.player_id
            
            if self._player_controller:
                render_pos = self._player_controller.get_render_position()
                server_pos = self._player_controller.get_server_position()
                latency_history = self._player_controller.get_latency_history()
                current_latency = self._player_controller.get_latency()
            
            self._renderer.draw_players(
                self._room_snapshot,
                self._client.player_id,
                self._get_bot_ids(),
                render_pos,
                server_pos
            )
            
            # Draw latency graph
            if latency_history:
                self._renderer.draw_latency_graph(latency_history, current_latency)
        
        if self._show_debug and not self._show_help:
            # Get positions for debug display
            render_pos = None
            drift = 0.0
            latency = 0.0
            latency_history = []
            if self._player_controller:
                render_pos = self._player_controller.get_render_position()
                drift = self._player_controller.get_drift()
                latency = self._player_controller.get_latency()
                latency_history = self._player_controller.get_latency_history()
            
            self._renderer.draw_debug_info(
                server_address=self.config.server_address,
                connected=True,
                my_player_id=self._client.player_id,
                room=self._room_snapshot,
                input_vector=self._input.get_movement_input(),
                sequence=self._client.get_sequence(),
                fps=int(self._clock.get_fps()),
                last_snapshot_time=self._last_snapshot_time,
                bot_count=len(self._simulated_players),
                show_help=self._show_help,
                render_pos=render_pos,
                drift=drift,
                latency=latency,
                latency_history=latency_history,
                snapshot_intervals=self._snapshot_times
            )
        
        if self._show_help:
            self._renderer.draw_help_menu()
        else:
            self._renderer.draw_instructions()
            
            # Draw ping (always visible in top right)
            ping = 0.0
            if self._player_controller:
                ping = self._player_controller.get_latency()
            self._renderer.draw_ping(ping)
        
        self._renderer.flip()
    
    async def run(self) -> int:
        """Main game loop."""
        if not await self.initialize():
            return 1
        
        self._running = True
        
        try:
            while self._running:
                frame_start = time.time()
                
                # Yield to let gRPC receive messages, then drain all pending snapshots
                await asyncio.sleep(0)
                self._client.process_snapshots()
                
                # Handle input
                self._running = self._input.process_events()
                
                if self._input.is_quit_requested():
                    break
                
                # Update game state
                self._update()
                
                # Yield again before render to let more gRPC messages through
                await asyncio.sleep(0)
                self._client.process_snapshots()
                
                # Render
                self._render()
                
                # Update FPS counter (tick(0) = count frame without sleeping)
                self._clock.tick(0)
                
                # Yield remaining frame time to asyncio for gRPC processing
                elapsed = time.time() - frame_start
                remaining = self._frame_interval - elapsed
                await asyncio.sleep(max(0.001, remaining))
        
        except KeyboardInterrupt:
            print("\nInterrupted by user")
        finally:
            await self._cleanup()
        
        return 0
    
    async def _cleanup(self):
        """Cleanup all resources."""
        print("Cleaning up...")
        
        # Stop all bots
        if self._simulated_players:
            print(f"Stopping {len(self._simulated_players)} simulated players...")
            stop_tasks = [bot.stop() for bot in self._simulated_players]
            await asyncio.gather(*stop_tasks, return_exceptions=True)
        
        # Disconnect main client
        await self._client.disconnect()
        
        # Quit pygame
        pygame.quit()
