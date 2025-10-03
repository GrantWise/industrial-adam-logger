#!/bin/bash

# Start 3 ADAM simulators directly (not in Docker)
# This script starts each simulator on different ports for local testing

echo "Starting Industrial ADAM Logger Simulators..."
echo "=============================================="

PROJECT_ROOT="/home/grant/adam-6000-counter"
SIMULATOR_PROJECT="$PROJECT_ROOT/src/Industrial.Adam.Logger.Simulator"

# Check if simulator project exists
if [ ! -d "$SIMULATOR_PROJECT" ]; then
    echo "❌ Error: Simulator project not found at $SIMULATOR_PROJECT"
    exit 1
fi

cd "$SIMULATOR_PROJECT"

# Kill any existing simulators
echo "🧹 Cleaning up any existing simulators..."
pkill -f "Industrial.Adam.Logger.Simulator" 2>/dev/null || true
sleep 2

# Function to start a simulator
start_simulator() {
    local sim_number=$1
    local modbus_port=$2
    local api_port=$3
    local base_rate=$4
    local rate_variation=$5
    local log_file="$PROJECT_ROOT/logs/simulator$sim_number.log"
    
    echo "🚀 Starting Simulator $sim_number..."
    echo "   - Modbus TCP: localhost:$modbus_port"
    echo "   - REST API: http://localhost:$api_port"
    echo "   - Base Rate: $base_rate units/min"
    echo "   - Log File: $log_file"
    
    # Create logs directory if it doesn't exist
    mkdir -p "$PROJECT_ROOT/logs"
    
    # Set environment variables and start simulator
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="http://localhost:$api_port" \
    SimulatorSettings__DeviceId="SIM-6051-0$sim_number" \
    SimulatorSettings__DeviceName="Production Line $sim_number Simulator" \
    SimulatorSettings__ModbusPort="$modbus_port" \
    SimulatorSettings__ApiPort="$api_port" \
    ProductionSettings__BaseRate="$base_rate" \
    ProductionSettings__RateVariation="$rate_variation" \
    dotnet run --configuration Release \
        > "$log_file" 2>&1 &
    
    local pid=$!
    echo "   - PID: $pid"
    echo "$pid" > "$PROJECT_ROOT/logs/simulator$sim_number.pid"
    
    # Wait a moment and check if it started successfully
    sleep 3
    if kill -0 $pid 2>/dev/null; then
        echo "   ✅ Simulator $sim_number started successfully"
    else
        echo "   ❌ Simulator $sim_number failed to start"
        echo "   📋 Check log: $log_file"
    fi
    echo
}

# Start all 3 simulators with different configurations
start_simulator 1 5502 8081 120 0.1   # Production Line 1: 120 units/min, 10% variation
start_simulator 2 5503 8082 90  0.15   # Production Line 2: 90 units/min, 15% variation  
start_simulator 3 5504 8083 60  0.2    # Packaging Line: 60 units/min, 20% variation

echo "⏳ Waiting for all simulators to fully initialize..."
sleep 5

echo "🔍 Checking simulator status..."
echo "================================"

# Check each simulator
for i in 1 2 3; do
    local port=$((8080 + i))
    echo -n "Simulator $i (port $port): "
    
    if curl -s -f "http://localhost:$port/api/simulator/health" > /dev/null 2>&1; then
        echo "✅ Healthy"
        echo "   🌐 API: http://localhost:$port/api/simulator/status"
        echo "   🔧 Modbus: localhost:$((5501 + i))"
    else
        echo "❌ Not responding"
        echo "   📋 Check log: $PROJECT_ROOT/logs/simulator$i.log"
    fi
    echo
done

echo "📋 Simulator Management Commands:"
echo "================================="
echo "View logs:     tail -f $PROJECT_ROOT/logs/simulator1.log"
echo "Stop all:      $PROJECT_ROOT/scripts/stop-simulators.sh"
echo "Test connection: $PROJECT_ROOT/scripts/test-simulators.sh"
echo
echo "📊 Simulator Web Interfaces:"
echo "=============================="
echo "Simulator 1: http://localhost:8081/api/simulator/status"
echo "Simulator 2: http://localhost:8082/api/simulator/status"  
echo "Simulator 3: http://localhost:8083/api/simulator/status"
echo
echo "🏁 All simulators startup complete!"