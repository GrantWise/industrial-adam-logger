FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 5502

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Industrial.Adam.Logger.Simulator.csproj", "."]
RUN dotnet restore "Industrial.Adam.Logger.Simulator.csproj"
COPY . .
RUN dotnet build "Industrial.Adam.Logger.Simulator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Industrial.Adam.Logger.Simulator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create data directory for SQLite
RUN mkdir -p /app/data

# Environment variables can override settings
ENV ASPNETCORE_URLS=http://+:8080
ENV SimulatorSettings__ModbusPort=5502
ENV SimulatorSettings__ApiPort=8080
ENV SimulatorSettings__DatabasePath=/app/data/simulator.db

ENTRYPOINT ["dotnet", "Industrial.Adam.Logger.Simulator.dll"]