#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PingPongDemo/PingPongDemo.csproj", "PingPongDemo/"]
COPY ["MCGateway/MCGateway.csproj", "MCGateway/"]
RUN dotnet restore "PingPongDemo/PingPongDemo.csproj"
COPY . .
WORKDIR "/src/PingPongDemo"
RUN dotnet build "PingPongDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PingPongDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PingPongDemo.dll"]