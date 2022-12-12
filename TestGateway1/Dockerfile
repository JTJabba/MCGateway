#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["TestGateway1/TestGateway1.csproj", "TestGateway1/"]
RUN dotnet restore "TestGateway1/TestGateway1.csproj"
COPY . .
WORKDIR "/src/TestGateway1"
RUN dotnet build "TestGateway1.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestGateway1.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestGateway1.dll"]