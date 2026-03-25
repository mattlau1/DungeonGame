FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY DungeonServer/DungeonServer.Contracts/DungeonServer.Contracts.csproj DungeonServer/DungeonServer.Contracts/
COPY DungeonServer/DungeonServer.Benchmark/DungeonServer.Benchmark.csproj DungeonServer/DungeonServer.Benchmark/
RUN dotnet restore "DungeonServer/DungeonServer.Benchmark/DungeonServer.Benchmark.csproj"

COPY contracts/ contracts/

COPY DungeonServer/ DungeonServer/
WORKDIR "/src/DungeonServer/DungeonServer.Benchmark"
RUN dotnet publish "DungeonServer.Benchmark.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 9092
COPY --from=build /app/publish .
ENTRYPOINT ["/app/DungeonServer.Benchmark"]