#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

# Add a line here to create a directory for SSL certificates
RUN mkdir /app/crypto

# Install curl in the base image
RUN apt-get update && apt-get install -y curl

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Examples/PingPongDemo/PingPongDemo.csproj", "Examples/PingPongDemo/"]
COPY ["MCGateway/MCGateway.csproj", "MCGateway/"]
RUN dotnet restore "Examples/PingPongDemo/PingPongDemo.csproj"
COPY . .
WORKDIR "/src/Examples/PingPongDemo"
RUN dotnet build "PingPongDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PingPongDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Copy the SSL certificates from your local project into the container
COPY crypto /app/crypto

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PingPongDemo.dll"]