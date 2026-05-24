"""gRPC client for DungeonController service."""

import asyncio
import logging
import queue
import threading
from typing import Callable, Optional

import grpc
from google.protobuf import empty_pb2

from proto.Core import dungeon_controller_pb2_grpc
from proto.Core import movement_pb2
from proto.Core import player_pb2
from proto.Core import room_pb2
from models import PlayerState, RoomState

logger = logging.getLogger("DungeonClient")

CHANNEL_OPTIONS = [
    ('grpc.keepalive_time_ms', 10000),
    ('grpc.keepalive_timeout_ms', 5000),
    ('grpc.http2.max_pings_without_data', 0),
    ('grpc.http2.min_time_between_pings_ms', 10000),
    ('grpc.http2.min_ping_interval_without_data_ms', 5000),
]


class GrpcClient:
    """Handles all gRPC communication with the server."""
    
    def __init__(self, server_address: str = "localhost:5142"):
        self.server_address = server_address
        self._channel: Optional[grpc.aio.Channel] = None
        self._stub: Optional[dungeon_controller_pb2_grpc.DungeonControllerStub] = None
        
        self.player_id: Optional[int] = None
        self.room_id: Optional[int] = None
        self.room_info: Optional[room_pb2.RoomInfo] = None
        
        self._input_sequence = 0
        self._input_queue: asyncio.Queue = asyncio.Queue()
        self._running = False
        
        self._input_task: Optional[asyncio.Task] = None
        
        self._snapshot_thread: Optional[threading.Thread] = None
        self._snapshot_queue: queue.Queue = queue.Queue()
        self._snapshot_callback: Optional[Callable[[RoomState], None]] = None
        self._sync_channel: Optional[grpc.Channel] = None
    
    async def connect(self) -> bool:
        try:
            self._channel = grpc.aio.insecure_channel(self.server_address, options=CHANNEL_OPTIONS)
            self._stub = dungeon_controller_pb2_grpc.DungeonControllerStub(self._channel)
            
            await self._stub.GetServerStatus(empty_pb2.Empty())
            logger.info(f"Connected to {self.server_address}")
            return True
        except Exception as e:
            logger.error(f"Failed to connect: {e}")
            return False
    
    async def disconnect(self):
        self._running = False
        
        if self._input_task:
            self._input_task.cancel()
            try:
                await self._input_task
            except asyncio.CancelledError:
                pass
        
        if self._sync_channel:
            try:
                self._sync_channel.close()
            except Exception:
                pass
        
        if self._channel:
            await self._channel.close()
        
        logger.info("Disconnected")
    
    async def spawn_player(self) -> Optional[PlayerState]:
        if not self._stub:
            logger.error("Not connected")
            return None
        
        try:
            response = await self._stub.SpawnPlayer(player_pb2.SpawnRequest())
            
            self.player_id = response.id
            self.room_id = response.room_id
            
            logger.info(f"Spawned player {self.player_id} in room {self.room_id}")
            
            room_request = room_pb2.RoomInfoRequest(room_id=self.room_id)
            self.room_info = await self._stub.GetRoomInfo(room_request)
            logger.info(f"Room dimensions: {self.room_info.width}x{self.room_info.height}")
            
            return PlayerState(
                id=response.id,
                room_id=response.room_id,
                x=response.location.x,
                y=response.location.y,
                is_online=response.is_online
            )
        except Exception as e:
            logger.error(f"Failed to spawn player: {e}")
            return None
    
    async def start_streams(self, snapshot_callback: Callable[[RoomState], None]):
        if not self._stub or not self.player_id or not self.room_id:
            logger.error("Cannot start streams - missing player/room info")
            return
        
        self._running = True
        self._snapshot_callback = snapshot_callback
        
        self._snapshot_thread = threading.Thread(
            target=self._subscribe_loop_sync,
            daemon=True,
            name="subscribe_room"
        )
        self._snapshot_thread.start()
        
        self._input_task = asyncio.create_task(
            self._input_loop(),
            name="input_commands"
        )
        
        logger.info("Streams started")
    
    def _subscribe_loop_sync(self):
        try:
            self._sync_channel = grpc.insecure_channel(self.server_address, options=CHANNEL_OPTIONS)
            sync_stub = dungeon_controller_pb2_grpc.DungeonControllerStub(self._sync_channel)
            
            request = room_pb2.SubscribeRoomRequest(
                player_id=self.player_id,
                room_id=self.room_id
            )
            
            for snapshot in sync_stub.SubscribeRoom(request):
                if not self._running:
                    break
                
                players = {
                    p.id: PlayerState(
                        id=p.id,
                        room_id=p.room_id,
                        x=p.location.x,
                        y=p.location.y,
                        is_online=p.is_online
                    )
                    for p in snapshot.players
                }
                
                room_state = RoomState(
                    room_id=snapshot.room_id,
                    width=self.room_info.width if self.room_info else 40,
                    height=self.room_info.height if self.room_info else 40,
                    room_type=str(self.room_info.room_type) if self.room_info else "UNKNOWN",
                    players=players
                )
                
                self._snapshot_queue.put(room_state)
                
        except grpc.RpcError as e:
            if self._running:
                logger.error(f"Room subscription error: {e}")
        except Exception as e:
            if self._running:
                logger.error(f"Room subscription error: {e}")
    
    def process_snapshots(self):
        while True:
            try:
                snapshot = self._snapshot_queue.get_nowait()
                if self._snapshot_callback:
                    self._snapshot_callback(snapshot)
            except queue.Empty:
                break
    
    async def _input_loop(self):
        try:
            async def request_generator():
                while self._running:
                    try:
                        input_x, input_y = await asyncio.wait_for(
                            self._input_queue.get(),
                            timeout=0.016
                        )
                        self._input_sequence += 1
                        
                        yield movement_pb2.InputCommandRequest(
                            player_id=self.player_id,
                            input_x=input_x,
                            input_y=input_y,
                            sequence=self._input_sequence
                        )
                    except asyncio.TimeoutError:
                        continue
                    except asyncio.CancelledError:
                        break
            
            async for _ in self._stub.SendInputCommand(request_generator()):
                pass
                
        except grpc.aio.AioRpcError as e:
            if self._running:
                logger.error(f"Input stream error: {e}")
        except asyncio.CancelledError:
            logger.info("Input stream cancelled")
        except Exception as e:
            logger.error(f"Input stream error: {e}")
    
    def send_input(self, input_x: float, input_y: float):
        try:
            if self._input_queue.qsize() < 30:
                self._input_queue.put_nowait((input_x, input_y))
        except asyncio.QueueFull:
            pass
    
    def get_sequence(self) -> int:
        return self._input_sequence