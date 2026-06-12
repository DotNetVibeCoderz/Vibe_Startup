# 🎮 Simulator — FastRide

> Parallel Ride-Hailing Simulator for load testing, demos, and performance analysis.

---

## 📖 Overview

The **FastRide Simulator** is a console application built with [Spectre.Console](https://spectreconsole.net/) that simulates multiple riders creating orders and multiple drivers accepting and completing them — all in parallel, with a live interactive table display.

```
  _____                 _     ____    _       _
 |  ___|   __ _   ___  | |_  |  _ \  (_)   __| |   ___
 | |_     / _` | / __| | __| | |_) | | |  / _` |  / _ \
 |  _|   | (_| | \__ \ | |_  |  _ <  | | | (_| | |  __/
 |_|      \__,_| |___/  \__| |_| \_\ |_|  \__,_|  \___|

▶ Starting simulation...
   Riders: 10 | Drivers: 5 | Duration: 30s
```

---

## 🎯 Features

| Feature | Description |
|---------|-------------|
| 🔄 **Parallel Simulation** | Multiple riders and drivers run simultaneously |
| 📊 **Live Statistics** | Real-time counter: total orders, requested, completed |
| 📋 **Active Orders Table** | Live table showing recent 20 orders with statuses |
| 🚗 **Driver Fleet Table** | Live table showing driver status, trips, earnings |
| ⏱️ **Configurable Duration** | Adjust simulation duration |
| 🎨 **Color-Coded Statuses** | Different colors for each order & driver status |
| 📈 **Final Summary** | Complete statistics after simulation ends |

---

## 🚀 Running the Simulator

```bash
# From project root
dotnet run --project FastRide.Simulator

# Or from simulator folder
cd FastRide.Simulator
dotnet run
```

---

## ⚙️ Configuration

Edit `Program.cs` to customize simulation parameters:

```csharp
// --- Configuration ---
const int riderCount = 10;                    // Number of simulated riders
const int driverCount = 5;                    // Number of simulated drivers
const int simulationDurationSeconds = 30;     // How long the simulation runs

var random = new Random(42);                  // Seed for reproducible results
```

### Parameter Guide

| Parameter | Default | Min | Max | Description |
|-----------|---------|-----|-----|-------------|
| `riderCount` | 10 | 1 | 1000 | Number of riders creating orders |
| `driverCount` | 5 | 1 | 500 | Number of drivers accepting orders |
| `simulationDurationSeconds` | 30 | 5 | 3600 | Simulation duration |

---

## 📊 Live Display

During simulation, you'll see two live tables updating every 500ms:

### Active Orders Table

```
┌──────────┬──────────┬──────────┬───────────┬───────────┐
│ Order ID │  Rider   │  Driver  │ Fare (Rp) │  Status   │
├──────────┼──────────┼──────────┼───────────┼───────────┤
│ #a1b2c3d4 │ Rider 1  │ Driver 3 │    25,000 │ Completed │
│ #e5f6a7b8 │ Rider 4  │ Driver 1 │    45,000 │ Started   │
│ #c9d0e1f2 │ Rider 7  │    -     │    18,000 │ Requested │
└──────────┴──────────┴──────────┴───────────┴───────────┘
```

### Driver Fleet Table

```
┌──────────┬───────────────┬─────────┬───────┬────────────────┐
│  Driver  │   Vehicle     │ Status  │ Trips │ Earnings (Rp)  │
├──────────┼───────────────┼─────────┼───────┼────────────────┤
│ Driver 1 │ 🚗 Economy    │ On Trip │    12 │        350,000 │
│ Driver 2 │ 🚙 Comfort    │ Online  │     8 │        240,000 │
│ Driver 3 │ 🏍️ Bike       │ Online  │    15 │        180,000 │
└──────────┴───────────────┴─────────┴───────┴────────────────┘
```

### Stats Panel

```
┌──────────────────────────────┐
│ 📊 Simulation Stats          │
│ ⏱️ Time: 15s / 30s           │
│ 📊 Total Orders: 47          │
│ 🆕 Requested: 12              │
│ ✅ Completed: 21              │
└──────────────────────────────┘
```

---

## 📈 Final Summary

After simulation completes, you'll get a final summary:

```
✅ Simulation Complete!

┌──────────────────────┬─────────────────┐
│ Metric               │ Value           │
├──────────────────────┼─────────────────┤
│ Total Orders Created │ 127             │
│ Orders Completed     │ 89              │
│ Orders Cancelled     │ 8               │
│ Avg. Fare            │ Rp 35,420       │
│ Total Driver Earnings│ Rp 3,152,380    │
└──────────────────────┴─────────────────┘
```

---

## 🧪 Use Cases

| Use Case | Configuration |
|----------|--------------|
| **Demo / Presentation** | `riderCount=5, driverCount=3, duration=20` |
| **Light Load Test** | `riderCount=50, driverCount=20, duration=60` |
| **Heavy Load Test** | `riderCount=200, driverCount=50, duration=120` |
| **Stress Test** | `riderCount=500, driverCount=100, duration=300` |

---

## 🔧 Technical Details

### Rider Behavior

- Creates orders at random intervals (1-5 seconds)
- Moves randomly around Jakarta coordinates (-6.2, 106.8)
- Each order has random fare (Rp 10.000 - Rp 100.000)

### Driver Behavior

- Scans for available orders every 2-6 seconds
- Accepts first available order (FIFO queue)
- Completes trips in 3-10 seconds (simulated time)
- Vehicles: Economy, Comfort, Bike, Electric

### Thread Safety

All order list operations use `lock` to prevent race conditions:

```csharp
lock (orders)
{
    orders.Add(order);
}
```

---

## 🎨 Color Scheme

| Element | Color | Hex |
|---------|-------|-----|
| Title | Gold | #FFD700 |
| Success | Green | #00C853 |
| Error | Red | #FF1744 |
| Warning | Orange | #FF6B35 |
| Info | Cyan | #00BCD4 |
| Headers | Yellow bold | - |

---

## 🚧 Planned Enhancements

- [ ] GPS coordinate movement to specific Jakarta landmarks
- [ ] Surge pricing simulation during peak hours
- [ ] Cancellation scenarios (rider cancel, driver cancel)
- [ ] Multi-stop trips simulation
- [ ] Export simulation results to CSV/JSON
- [ ] Performance metrics (orders/sec, avg response time)
- [ ] Integration with real API for end-to-end testing
