FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DungeonServer/DungeonServer.Contracts/DungeonServer.Contracts.csproj", "DungeonServer/DungeonServer.Contracts/"]
COPY ["DungeonServer/DungeonServer.Application/DungeonServer.Application.csproj", "DungeonServer/DungeonServer.Application/"]
COPY ["DungeonServer/DungeonServer.Infrastructure/DungeonServer.Infrastructure.csproj", "DungeonServer/DungeonServer.Infrastructure/"]
COPY ["DungeonServer/DungeonServer.Service/DungeonServer.Service.csproj", "DungeonServer/DungeonServer.Service/"]
RUN dotnet restore "DungeonServer/DungeonServer.Service/DungeonServer.Service.csproj"

COPY ["contracts/", "contracts/"]

COPY DungeonServer/ DungeonServer/
WORKDIR "/src/DungeonServer/DungeonServer.Service"
RUN dotnet build "DungeonServer.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DungeonServer.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DungeonServer.Service.dll"]