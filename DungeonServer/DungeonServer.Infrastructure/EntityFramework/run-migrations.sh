#!/bin/bash
# Run EF Core migrations
# Finds the solution root automatically
cd "$(dirname "$0")"
while [ ! -f "DungeonGame.sln" ] && [ "$(pwd)" != "/" ]; do
    cd ..
done
dotnet ef database update --project DungeonServer.Infrastructure --startup-project DungeonServer.Service
