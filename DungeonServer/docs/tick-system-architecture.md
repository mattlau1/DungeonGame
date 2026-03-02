# Tick-Based Movement System Architecture

## Overview

This document describes the server-side tick-based system, which is **very** loosely based on Valve's [Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) model

## Core Principles

1. **Server-Authoritative**: Server is the source of truth for all player positions
2. **Fixed Tick Rate**: Server runs at 64Hz (15.625ms per tick)
3. **Client-Provided Sequence**: Clients send input commands with sequence numbers for ordering
4. **In-Memory State**: Player state is kept in memory during gameplay, periodically persisted to DB
5. **Simulation-Based**: Rooms tick only when they have registered simulations

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

        subgraph "Simulation System"
            TR[TickRunner<br/>64Hz loop]
            SQ[SimulationQueue]
            SIM[ISimulation<br/>handlers]
        end

        subgraph "State Management"
            PSM[PlayerStateManager]
            RSM[RoomStateManager]
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
    TR <--> SQ
    SQ -.->|runs| SIM
    SIM --> PSM
    SIM --> RSM
    TR --> PSM
    TR --> RSM
    TR --> RR
    RR --> Redis
    Redis -->|broadcast| C
    PSM -->|periodic save| PS
    PS --> DB
```

## Simulation Queue Architecture

The simulation queue allows fine-grained control over which rooms tick:
- A room only ticks when it has registered simulations
- Different simulation types can run in the same room (e.g. players + enemies + projectiles)
- When all simulations are unregistered from a room, it stops ticking

## Data Flow

### Input Path

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant PIM as PlayerInputManager
    participant SQ as SimulationQueue
    participant TR as TickRunner

    C->>S: InputCommandRequest
    S->>PIM: SendInputCommandAsync
    PIM->>PIM: Enqueue to player's channel

    loop Every 15ms (64Hz)
        TR->>TR: Run each simulation for room
        TR->>S: Publish Room Updates
    end

    S-->>C: Room Snapshot (via SubscribeRoom)
```

### Tick Loop Flow

```mermaid
flowchart TD
    A[Start Tick] --> B[Increment global tick counter]
    B --> C[Get active rooms from SimulationQueue]
    C --> D{More rooms?}
    D -->|Yes| E[Get next room]
    D -->|No| P[Task.Delay 15ms]
    E --> F[Get room state]
    F --> G[Get simulation types for room]
    G --> H{More simulations?}
    H -->|Yes| I[Run Simulation]
    I --> H
    H -->|No| J[Build RoomPlayerUpdate snapshot]
    J --> K[Publish via Redis Pub/Sub]
    K --> D
    P --> C
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

### 5. SimulationQueue

- Tracks which simulation types are active per room
- Uses `Type` directly (no enum needed)
- Allows multiple simulation types per room
- Auto-discovery via DI: TickRunner finds all `ISimulation` implementations

### 6. TickRunner

- 64Hz async task loop
- Gets active rooms from SimulationQueue (not all rooms)
- Runs all registered simulation types per room
- Publishes snapshots to subscribers
- Periodic database save every 64 ticks

## Persistence Strategy

| Event | Action |
|-------|--------|
| Player spawns | Created in DB, added to PlayerStateManager, register PlayerSimulation |
| During gameplay | In-memory only (no DB) |
| Every 64 ticks (~1 sec) | Batch write all positions to DB |
| Player disconnects | Remove from PlayerStateManager, unregister simulation if empty |
| Room transition | Immediate DB update via SwapRoomsAsync |
| Server crash | Players resume from last saved position |

## API Contract

### SendInputCommand (gRPC)

Client sends movement input stream, server responds with Empty (positions come via SubscribeRoom).

### SubscribeRoom (gRPC)

Clients subscribe to room updates to receive position snapshots.
