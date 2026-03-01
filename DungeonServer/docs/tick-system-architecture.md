# Tick-Based Movement System Architecture

## Overview

This document describes the server-side tick-based movement system, which is very loosely based on Valve's [Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) model

## Core Principles

1. **Server-Authoritative**: Server is the source of truth for all player positions
2. **Fixed Tick Rate**: Server runs at 64Hz (15.625ms per tick)
3. **Client-Provided Sequence**: Clients send input commands with sequence numbers for ordering
4. **In-Memory State**: Player state is kept in memory during gameplay, periodically persisted to DB

## Architecture Diagram

```mermaid
flowchart TB
    subgraph Client
        C[Client]
    end

    subgraph Server
        subgraph "gRPC Layer"
            GS[gRPC Service]
        end

        subgraph "Input Processing"
            PIM[PlayerInputManager]
            PCQ[PlayerCommandQueue<br/>per player]
        end

        subgraph "Tick System"
            TR[TickRunner<br/>64Hz loop]
            PSM[PlayerStateManager]
            RSM[RoomStateManager]
            MM[MovementManager<br/>SimulatePhysics]
        end

        subgraph "Output"
            RR[IRoomSubscriptionRegistry]
            Redis[(Redis<br/>Pub/Sub)]
        end

        subgraph "Persistence"
            PS[IPlayerStore]
            DB[(Database)]
        end
    end

    C -->|InputCommand stream| GS
    GS --> PIM
    PIM --> PCQ
    PCQ -.->|dequeue each tick| TR
    TR <--> PSM
    TR <--> RSM
    TR <--> MM
    TR --> RR
    RR --> Redis
    Redis -->|broadcast| C
    PSM -->|periodic save| PS
    PS --> DB
```

## Data Flow

### Input Path

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant PIM as PlayerInputManager
    participant TR as TickRunner

    C->>S: InputCommandRequest (input_x, input_y, sequence)
    S->>PIM: SendInputCommandAsync(command)
    PIM->>PIM: Enqueue to player's channel
    Note over PIM: Commands queued in Channel<InputCommand>

    loop Every 15ms (64Hz)
        TR->>PIM: DequeueAllForPlayer(playerId)
        TR->>TR: Run physics for each player
        TR->>TR: Update in-memory position
        TR->>S: Publish RoomPlayerUpdate via Redis
    end

    S-->>C: RoomSnapshot (positions via SubscribeRoom)
```

### Tick Loop Flow

```mermaid
flowchart TD
    A[Start Tick] --> B[Increment global tick counter]
    B --> C[For each room]
    C --> D[Get room state from cache]
    D --> E[For each player in room]
    E --> F[Dequeue commands from PlayerInputManager]
    F --> G{Commands?}
    G -->|Yes| H[Apply movement input to position]
    G -->|No| I[Skip physics]
    H --> J[Run physics - collision and room boundary check]
    J --> K[Update LastProcessedSequence]
    K --> I
    I --> L[Build RoomPlayerUpdate snapshot]
    L --> M[Publish via IRoomSubscriptionRegistry]
    M --> N[Periodic save every 64 ticks?]
    N -->|Yes| O[Save all player states to DB]
    N -->|No| P[Next room]
    O --> P
    P --> Q[Task.Delay 15ms]
    Q --> C
```

## Key Components

### 1. InputCommand

Client sends movement intent with:
- PlayerId
- Sequence (client-provided for ordering)
- ClientTimestamp (for RTT calculation)
- Input (MoveX, MoveY in range -1 to +1)

### 2. PlayerInputManager

- Manages command queues per player
- Each player has their own queue
- Bounded queue that drops oldest when full
- Thread-safe storage

### 3. PlayerStateManager

- In-memory storage for player states
- Tracks position, room, view angle, sequence, online status
- Provides getPlayersInRoom, addPlayerToRoom, removePlayer, updatePosition
- Handles periodic save to database

### 4. RoomStateManager

- Caches room states in memory
- Loads from DB on first access
- Avoids DB query every tick

### 5. TickRunner

- 64Hz async task loop
- Processes all rooms each tick
- Gets room state from cache
- Dequeues player commands
- Runs physics for each player
- Publishes snapshots to subscribers
- Periodic database save every 64 ticks

### 6. MovementManager.SimulatePhysics

- Sums all input commands for the tick
- Updates player position
- Checks room boundaries
- Handles room transitions if needed
- Updates DB for room changes


## Persistence Strategy

| Event | Action |
|-------|--------|
| Player spawns | Created in DB, added to PlayerStateManager |
| During gameplay | In-memory only (no DB) |
| Every 64 ticks (~1 sec) | Batch write all positions to DB |
| Room transition | Immediate DB update via SwapRoomsAsync |
| Server crash | Players resume from last saved position |

## API Contract

### SendInputCommand (gRPC)

Client sends movement input stream, server responds with Empty (positions come via SubscribeRoom).

### SubscribeRoom (gRPC)

Clients subscribe to room updates to receive position snapshots.