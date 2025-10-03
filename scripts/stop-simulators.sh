#!/bin/bash

# Stop all ADAM simulators

echo "🛑 Stopping Industrial ADAM Logger Simulators..."
echo "=================================================="

PROJECT_ROOT="/home/grant/adam-6000-counter"

# Function to stop a simulator by PID file
stop_simulator() {
    local sim_number=$1
    local pid_file="$PROJECT_ROOT/logs/simulator$sim_number.pid"
    
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file")
        echo -n "Stopping Simulator $sim_number (PID: $pid): "
        
        if kill -0 $pid 2>/dev/null; then
            kill $pid
            sleep 2
            
            # Force kill if still running
            if kill -0 $pid 2>/dev/null; then
                kill -9 $pid 2>/dev/null
                echo "💥 Force stopped"
            else
                echo "✅ Stopped gracefully"
            fi
        else
            echo "⚠️  Process not running"
        fi
        
        rm -f "$pid_file"
    else
        echo "⚠️  Simulator $sim_number PID file not found"
    fi
}

# Stop all simulators by PID files
for i in 1 2 3; do
    stop_simulator $i
done

# Cleanup any remaining processes
echo
echo "🧹 Cleaning up any remaining simulator processes..."
pkill -f "Industrial.Adam.Logger.Simulator" 2>/dev/null && echo "✅ Cleaned up remaining processes" || echo "ℹ️  No additional processes found"

# Check if any are still running
echo
echo "🔍 Final status check..."
remaining=$(pgrep -f "Industrial.Adam.Logger.Simulator" | wc -l)
if [ $remaining -eq 0 ]; then
    echo "✅ All simulators stopped successfully"
else
    echo "⚠️  $remaining simulator processes still running"
    echo "Running processes:"
    pgrep -f "Industrial.Adam.Logger.Simulator" -l
fi

echo
echo "🏁 Simulator shutdown complete!"