# DungeonGame

> 2D Multiplayer MMORPG built with .NET 8 and C++ (WIP)

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![gRPC](https://img.shields.io/badge/gRPC-green.svg)](https://grpc.io/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-blue.svg)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7-red.svg)](https://redis.io/)
[![Docker](https://img.shields.io/badge/Docker-blue.svg)](https://www.docker.com/)
[![Kubernetes](https://img.shields.io/badge/Kubernetes-326CE5.svg)](https://kubernetes.io/)

---

## Backend (DungeonServer)

Real-time authoritative multiplayer game server using gRPC streaming and Redis pub/sub

### Architecture

```mermaid
flowchart TB
    subgraph GCP["Google Cloud Platform"]
        LB["HTTP(S)
    Load Balancer"]
        VPC["VPC
    dungeon-vpc"]
        GKE["GKE
    dungeon-game-v3"]
        Postgres["PostgreSQL
    dungeon-postgres"]
        Redis["Redis
    dungeon-redis"]
    end
    
    Users["Users"]
    
    Users --> LB
    LB --> GKE
    VPC --> GKE
    VPC --> Postgres
    VPC --> Redis
    
    style GCP fill:#4285f4,stroke:#1967d2,stroke-width:2px,color:#fff
    style LB fill:#9c27b0,stroke:#6a1b9a,stroke-width:2px,color:#fff
    style VPC fill:#34a853,stroke:#188038,stroke-width:2px,color:#fff
    style GKE fill:#33691e,stroke:#1b5e20,stroke-width:2px,color:#fff
    style Postgres fill:#ea4335,stroke:#c5221f,stroke-width:2px,color:#fff
    style Redis fill:#00897b,stroke:#00695c,stroke-width:2px,color:#fff
    style Users fill:#757575,stroke:#424242,stroke-width:2px,color:#fff
```

### Tech Stack

- **Language**: C#
- **Runtime**: ASP.NET 8
- **API**: gRPC with Protocol Buffers
- **Database**: PostgreSQL (EF Core)
- **Cache / Pub-Sub**: Redis
- **Infrastructure**: Docker

### Features

- Real-time player movement with room-to-room transitions
- Redis-backed distributed pub/sub for cross-instance state synchronization
- PostgreSQL persistence with write-through Redis caching
- gRPC streaming APIs for low-latency updates
- Containerized deployment with automatic health checks
- Built-in benchmark suite with real-time latency metrics (P50/P95/P99)

### Project Structure

```
DungeonServer/
├── Service/          # gRPC API layer
├── Application/      # Business logic & domain
├── Infrastructure/   # Database, cache, messaging implementations
├── Contracts/        # Protobuf service definitions
└── Benchmark/        # Load testing tools
```

### Getting Started

#### Docker Compose (Recommended for development)

```bash
docker compose up --build
```

Services:
- **Server**: gRPC on port 5142 (maps to internal port 8080)
- **Benchmark Dashboard**: port 9092
- **PostgreSQL**: port 5432
- **Redis**: port 6379
- **Adminer** (DB UI): port 9090
- **Redis Insight**: port 9091

#### Local Kubernetes

For a production-like Kubernetes environment locally using Kind + Helm, see [LOCAL_K8S_SETUP.md](LOCAL_K8S_SETUP.md).

---

## Client (DungeonClient)

C++ client using raylib for graphics and gRPC for networking

### Tech Stack

- **Language**: C++
- **Graphics**: raylib
- **Networking**: gRPC + Protocol Buffers
- **Package Manager**: vcpkg

### Status

Basic skeleton - not yet functional.

---

## Project Structure

```
DungeonGame/
├── DungeonServer/      # .NET 8 backend
│   ├── Service/        # API layer
│   ├── Application/    # Domain logic
│   ├── Infrastructure/ # DB, cache, messaging
│   ├── Contracts/      # Protobuf definitions
│   └── Benchmark/      # Load testing tools
├── DungeonClient/      # C++ client (WIP)
└── contracts/          # .proto files
```