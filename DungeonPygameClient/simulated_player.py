"""Simulated player (bot) that autonomously moves around."""

import asyncio
import logging
import math
import random
from typing import Optional

from grpc_client import GrpcClient

logger = logging.getLogger("DungeonClient")


class SimulatedPlayer:
    """A bot player that automatically moves around with AI behavior."""
    
    def __init__(self, server_address: str, name: str = "Bot"):
        self.name = name
        self._client = GrpcClient(server_address)
        self.player_id: Optional[int] = None
        self._ai_task: Optional[asyncio.Task] = None
        self._running = False
        
        # Room info for boundary checking
        self._room_id: int = 0
        self._room_width: float = 0.0
        self._room_height: float = 0.0
        self._x: float = 0.0
        self._y: float = 0.0
        
        # AI state
        self._random = random.Random()
        self._movement_interval_ms: int = 50
    
    async def start(self) -> bool:
        """Connect, spawn, and start AI behavior."""
        if not await self._client.connect():
            logger.error(f"[{self.name}] Failed to connect")
            return False
        
        spawn_response = await self._client.spawn_player()
        if not spawn_response:
            logger.error(f"[{self.name}] Failed to spawn")
            return False
        
        self.player_id = spawn_response.id
        self._room_id = spawn_response.room_id
        self._x = spawn_response.x
        self._y = spawn_response.y
        
        self._random.seed(self.player_id)
        
        if self._client.room_info:
            self._room_width = float(self._client.room_info.width)
            self._room_height = float(self._client.room_info.height)
            
            margin = 1.0
            self._x = max(margin, min(self._x, self._room_width - margin))
            self._y = max(margin, min(self._y, self._room_height - margin))
        
        logger.info(f"[{self.name}] Spawned as player {self.player_id} in room {self._room_id} "
                    f"({self._room_width:.0f}x{self._room_height:.0f})")
        
        # Start streams - bot ignores snapshots but still has its own stream
        await self._client.start_streams(lambda _: None)
        
        self._running = True
        self._ai_task = asyncio.create_task(self._ai_loop(), name=f"ai_{self.name}")
        
        return True
    
    async def stop(self):
        """Stop the simulated player."""
        self._running = False
        
        if self._ai_task:
            self._ai_task.cancel()
            try:
                await self._ai_task
            except asyncio.CancelledError:
                pass
        
        await self._client.disconnect()
        logger.info(f"[{self.name}] Stopped")
    
    async def _ai_loop(self):
        """AI behavior: wander with boundary-aware movement."""
        try:
            current_angle = self._random.random() * 2 * math.pi
            direction_duration = 0
            
            while self._running:
                direction_duration += 1
                if direction_duration >= 20:
                    direction_duration = 0
                    current_angle = self._random.random() * 2 * math.pi
                
                input_x, input_y = self._calculate_movement_input_with_angle(current_angle)
                
                if self._client._input_queue.qsize() < 10:
                    self._client.send_input(input_x, input_y)
                
                await asyncio.sleep(self._movement_interval_ms / 1000.0)
                
        except asyncio.CancelledError:
            pass
        except Exception as e:
            logger.error(f"[{self.name}] AI error: {e}")
    
    def _calculate_movement_input_with_angle(self, angle: float) -> tuple[float, float]:
        """Calculate movement input with a specific angle."""
        distance = 0.3
        
        input_x = math.cos(angle) * distance
        input_y = math.sin(angle) * distance
        
        margin = 1.0
        predicted_x = self._x + input_x
        predicted_y = self._y + input_y
        
        clamped_x = max(margin, min(predicted_x, self._room_width - margin))
        clamped_y = max(margin, min(predicted_y, self._room_height - margin))
        
        adjusted_input_x = clamped_x - self._x
        adjusted_input_y = clamped_y - self._y
        
        self._x = clamped_x
        self._y = clamped_y
        
        return (adjusted_input_x, adjusted_input_y)