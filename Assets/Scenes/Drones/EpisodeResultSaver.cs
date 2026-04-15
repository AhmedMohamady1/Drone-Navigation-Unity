using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

/// <summary>
/// Writes one JSON file per successful episode to
/// &lt;ProjectRoot&gt;/DronePathfindingResults/.
///
/// Each file is named  {uuid}_{iteration:D5}.json
/// and contains the A* planned path, the drone's actual path,
/// timing, attempt count, and exploration statistics.
/// </summary>
public static class EpisodeResultSaver
{
    // ============================================== serialisable types
    [Serializable]
    public class Vec3
    {
        public float x, y, z;
        public Vec3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    }

    [Serializable]
    public class EpisodeResult
    {
        // identification
        public string uuid;
        public int    iteration;
        public string timestamp;
        public float  timestampEpoch;

        // episode stats
        public int   attemptsToTarget;
        public float totalPathLength;
        public float totalTime;
        public int   stepCount;
        public int   exploredCells;
        public float pathCompletionRatio;

        // positions
        public Vec3 startPosition;
        public Vec3 targetPosition;
        public Vec3 endPosition;

        // paths
        public List<Vec3> plannedPath;   // A* optimal path
        public List<Vec3> actualPath;    // every recorded step

        public EpisodeResult()
        {
            uuid             = Guid.NewGuid().ToString();
            plannedPath      = new List<Vec3>();
            actualPath       = new List<Vec3>();
            timestampEpoch   = (float)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds;
            timestamp        = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    // ================================================= static state
    private static string _dir;
    private static int    _iteration;

    // =========================================== public API
    public static void ResetIterationCounter() => _iteration = 0;

    public static void SaveEpisodeResult(PathTracker    tracker,
                                          List<Vector3>  plannedPath,
                                          Vector3        endPos)
    {
        EnsureDirectory();

        var r = new EpisodeResult
        {
            iteration          = ++_iteration,
            attemptsToTarget   = tracker.GetAttempts(),
            totalPathLength    = tracker.GetTotalPathLength(),
            totalTime          = tracker.GetTotalTime(),
            stepCount          = tracker.GetStepCount(),
            exploredCells      = tracker.GetExploredCellCount(),
            pathCompletionRatio = tracker.GetPathCompletion(),
            startPosition      = new Vec3(tracker.GetEpisodeStartPosition()),
            targetPosition     = new Vec3(tracker.GetTargetPosition()),
            endPosition        = new Vec3(endPos),
        };

        // A* planned path
        foreach (var p in plannedPath)
            r.plannedPath.Add(new Vec3(p));

        // Actual drone path (every recorded step)
        foreach (var step in tracker.GetRecordedPath())
            r.actualPath.Add(new Vec3(step.position));

        // Write file
        string filename = $"{r.uuid}_{r.iteration:D5}.json";
        string filepath = Path.Combine(_dir, filename);

        try
        {
            File.WriteAllText(filepath, JsonUtility.ToJson(r, prettyPrint: true));
            Debug.Log($"[EpisodeResultSaver] Saved → {filepath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EpisodeResultSaver] Write failed: {ex.Message}");
        }
    }

    public static string GetResultsDirectory()
    {
        EnsureDirectory();
        return _dir;
    }

    // =========================================== helpers
    private static void EnsureDirectory()
    {
        if (_dir != null && Directory.Exists(_dir)) return;

        // Prefer project root (one level above Assets)
        try
        {
            string projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
            _dir = Path.Combine(projectRoot, "DronePathfindingResults");
        }
        catch
        {
            _dir = Path.Combine(Application.persistentDataPath, "DronePathfindingResults");
        }

        if (!Directory.Exists(_dir))
            Directory.CreateDirectory(_dir);

        Debug.Log($"[EpisodeResultSaver] Results directory: {_dir}");
    }
}
