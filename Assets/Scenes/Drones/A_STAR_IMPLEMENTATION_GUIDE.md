# A* Pathfinding Implementation for Drone Agent

## Implementation Summary

A complete A* pathfinding system has been integrated into your ML-Agents drone project. The system uses LiDAR data to create optimal paths and provides rich observations to the neural network model.

## Files Created

### 1. **AStarPathfinder.cs**
Implements the A* pathfinding algorithm with LiDAR integration:
- **Grid-based pathfinding**: Divides the environment into a 3D grid
- **LiDAR-aware obstacles**: Uses raycasts to update walkability based on sensor data
- **Heuristic pathfinding**: Employs Euclidean distance heuristic for efficiency
- **Dynamic updates**: Grid updates with LiDAR data each frame
- **Waypoint generation**: Creates a series of waypoints to reach the target

**Key Methods:**
- `UpdateGridWithLidarData()`: Updates obstacle information based on LiDAR scans
- `FindPath()`: Finds optimal path from start to goal using A*
- `GetPathDirection()`: Gets the normalized direction to the next waypoint

### 2. **PathTracker.cs**
Records and tracks the drone's journey:
- **Step recording**: Records position, distance, and time for each action
- **Path planning**: Stores both optimal (A*) and actual (traveled) paths
- **Progress tracking**: Monitors waypoint progression toward target
- **Observation generation**: Converts path data into normalized neural network inputs

**Key Methods:**
- `StartTracking()`: Begin tracking a new episode
- `RecordStep()`: Record drone position at each timestep
- `GetPathAsObservations()`: Returns last 20 steps as neural network observations (normalized)
- `GetTransformAsObservations()`: Returns 10 normalized transform features

### 3. **EpisodeResultSaver.cs**
Saves episode results to JSON files:
- **Location**: Saves to `ProjectRoot/DronePathfindingResults/` (not in Assets folder)
- **Filename format**: `{UUID}_{iteration:D5}.json`
- **Data stored**:
  - Optimal A* path (all waypoints)
  - Actual path taken (all step positions)
  - Number of attempts/recalculations
  - Total path length and time
  - Start and target positions
  - Timestamp (both human-readable and epoch)
  - Path completion ratio

### 4. **DroneAgent.cs (Modified)**
Enhanced with A* integration:
- Initializes A* and PathTracker components
- Generates observations from 6 sources
- Implements periodic path recalculation
- Waypoint detection and progression
- Episode result saving on target reach

## Observation Space (Total ~135+ observations)

The neural network now receives:

1. **Target relative position** (3): Direction and distance to target
2. **Drone velocity** (3): Current speed vector
3. **Transform data** (10):
   - Normalized position (3)
   - Distance to target (1)
   - Path completion ratio (1)
   - Normalized step count (1)
   - Normalized time (1)
   - Total path length (1)
   - Normalized attempt count (1)
   - Waypoint progress (1)

4. **A* Direction & Path** (6):
   - Direction to next waypoint (3)
   - Path completion percentage (1)
   - Distance to next waypoint (normalized) (1)
   - Remaining waypoints ratio (1)

5. **Recorded path history** (60): Last 20 steps as (x,y,z) coordinates (normalized)

6. **LiDAR data** (64): Distance to obstacles in 8x8 grid pattern

## Configuration (Inspector Settings)

In the DroneAgent component, you can adjust:

```
Movement Settings:
- moveSpeed: 7.0
- rotationSpeed: 120.0

LiDAR Settings:
- lidarObservationPoints: 64
- lidarMaxDistance: 50.0

A* Pathfinding Settings:
- useAStarPathfinding: true (enable/disable A*)
- pathRecalculationInterval: 2.0 (seconds between path updates)
- waypointReachedDistance: 1.0 (distance threshold for waypoint completion)

Episode Recording:
- saveEpisodeResults: true (save JSON results)
```

## How It Works

### Episode Flow

1. **Episode Begin**:
   - Reset drone to starting position
   - Initialize path tracking
   - Update grid with initial LiDAR scan
   - Calculate initial A* path to target

2. **Each Step**:
   - Agent takes action (movement + rotation)
   - LiDAR data collected
   - Path tracker records position
   - Observations generated (includes A* direction)
   - Neural network makes next decision

3. **Path Recalculation** (every 2 seconds by default):
   - Update grid with current LiDAR obstacles
   - Recalculate A* path from current position
   - Update waypoint tracking
   - Attempt counter increments

4. **Target Reached**:
   - Reward +10
   - **JSON file saved** with episode data
   - Path tracking stops
   - Episode ends

### A* Algorithm Details

The A* implementation:
- **Grid resolution**: Adjustable via `gridCellSize` (default: 0.5 units)
- **Grid size**: 20x10x20 units centered at `gridCenter`
- **Heuristic**: Euclidean distance to goal
- **Diagonal movement**: Available (cost = 1.414)
- **Obstacle detection**: 3x3x3 buffer zone around LiDAR hits

The algorithm prefers smooth, direct paths while avoiding obstacles detected by LiDAR.

## Rewards

- **Per step**: -0.001 (encourages speed)
- **Path progress**: +0.01 × completion ratio (rewards forward progress)
- **Target reached**: +10
- **Wall collision**: -1
- **Out of bounds**: -1

This encourages the model to:
1. Follow the A* suggested path
2. Reach target quickly
3. Avoid walls
4. Stay within the training area

## JSON Output Format

**File Location**: `ProjectRoot/DronePathfindingResults/`

**Example filename**: `a1b2c3d4-e5f6-7890-abcd-ef1234567890_00001.json`

**File contents**:
```json
{
  "uuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "iteration": 1,
  "attemptsToTarget": 3,
  "totalPathLength": 45.2,
  "totalTime": 12.5,
  "stepCount": 250,
  "startPosition": { "x": 0, "y": 1.5, "z": 0 },
  "targetPosition": { "x": 5, "y": 3, "z": -2 },
  "endPosition": { "x": 5.1, "y": 3.0, "z": -1.9 },
  "optimalPath": [
    { "x": 0, "y": 1.5, "z": 0 },
    { "x": 1, "y": 2, "z": -0.5 },
    ...
    { "x": 5, "y": 3, "z": -2 }
  ],
  "actualPath": [
    { "x": 0, "y": 1.5, "z": 0 },
    { "x": 0.05, "y": 1.51, "z": -0.02 },
    ...
    { "x": 5.1, "y": 3.0, "z": -1.9 }
  ],
  "pathCompletionRatio": 0.95,
  "timestampEpoch": 1704067200.0,
  "timestamp": "2024-01-01 12:00:00"
}
```

## Setup Instructions

1. **Add components to DroneAgent GameObject** (if not auto-added):
   - AStarPathfinder component
   - PathTracker component
   - Any existing components remain unchanged

2. **Verify Inspector settings**:
   - Ensure `useAStarPathfinding = true`
   - Ensure `saveEpisodeResults = true`

3. **Check results directory**:
   - After first episode, check `ProjectRoot/DronePathfindingResults/`
   - JSON files appear as episodes complete

4. **Monitor training**:
   - Check Console for debug logs showing path calculations
   - Each iteration number in JSON shows progression

## Key Benefits

1. **Faster Learning**: A* provides guided direction, reducing exploration time
2. **Path Optimization**: Model learns to follow optimal paths while adapting to obstacles
3. **Data Recording**: Complete episode history for analysis and debugging
4. **Obstacle Avoidance**: LiDAR integration ensures paths avoid detected obstacles
5. **Dynamic Replanning**: Path recalculates as obstacles are discovered

## Debugging

**Monitor these in the Console**:
```
Episode 1: Initial path calculated with 15 waypoints
Episode 1: Target hit!
Episode result saved: ProjectRoot/DronePathfindingResults/{UUID}.json
```

**Check these if issues occur**:
- Is AStarPathfinder component attached? (auto-added by Initialize)
- Is PathTracker component attached? (auto-added by Initialize)
- Is target assigned in Inspector?
- Are Wall and Target tags applied to objects?
- Is persistent data path accessible?

## Performance Considerations

- **Path recalculation**: 2-second interval reduces computation overhead
- **Grid size**: Adjust `gridCellSize` for speed/accuracy trade-off
  - Larger cells: Faster but less precise
  - Smaller cells: More precise but slower
- **Ray count**: 64 LiDAR points is balanced for training

## Next Steps

1. **Train the model** using this enhanced agent
2. **Analyze saved JSON files** to understand learning progression
3. **Adjust reward weights** if needed for your training goals
4. **Tune A* parameters** (grid size, recalculation interval) for your environment
5. **Export trained model** for deployment

The system is now ready for training with A* guidance alongside LiDAR obstacle awareness!
