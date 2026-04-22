using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

/// <summary>
/// ML-Agents drone that navigates using:
///   • LiDAR point-cloud observations  (obstacle avoidance / environment sensing)
///   • A* waypoint direction           (guided exploration toward target)
///   • PathTracker history + transform (self-localisation & progress)
///
/// Multi-environment support:
///   • The drone and target must both be children of the same environment root GameObject.
///   • If 'target' is left unassigned in the Inspector, Initialize() will auto-find it
///     by searching for a child tagged "Target" under the same parent. This means
///     duplicating the environment prefab requires no manual re-wiring.
///   • All position resets and bounds checks use localPosition relative to the
///     environment root, so every instance is self-contained.
/// </summary>
public class DroneAgent : Agent
{
    // --------------------------------------------------------- references
    [Tooltip("Assign in Inspector, or leave null to auto-find the child tagged 'Target' under the same parent.")]
    public Transform target;

    [Header("Shared Physics Tuning")]
    public float physicsAcceleration = 5f;
    public float physicsMaxSpeed = 20f;
    public float physicsHorizontalDamping = 0.5f;
    public float physicsHorizontalDrag = 1f;
    public float physicsRotateSpeed = 40f;

    [Header("Shared Physics Stabilization")]
    public float physicsHoverThrust = 0f;
    public float physicsThrustPower = 8f;
    public float physicsDamping = 1f;

    [Header("Shared Physics Ground")]
    public LayerMask physicsGroundLayer;
    public float physicsGroundCheckDistance = 0.3f;

    [Header("Shared Physics Behavior")]
    public bool physicsAllowYawWhenGrounded = false;

    [Header("LiDAR")]
    public int   lidarObservationPoints = 32;
    public float lidarMaxDistance       = 15f;

    [Header("A* Pathfinding")]
    public bool  useAStarPathfinding        = true;
    public float pathRecalculationInterval  = 2f;
    public float waypointReachedDistance    = 1f;
    public float targetReachedRadius        = 7.0f;

    [Header("Rewards")]
    public float stepPenalty            = -0.001f;
    public float wallPenalty            = -2f;
    public float targetReward           = 10f;
    public float distanceRewardScale    = 0.1f;
    public float progressRewardScale    = 0.005f;
    public float headingAlignmentRewardScale = 0.001f;
    public float excessYawRateThreshold      = 3.0f;
    public float excessYawRatePenaltyScale   = 0.0003f;
    public float choppyMovementPenalty       = -0.0001f;
    public float timeoutPenalty         = -2f;
    public float outOfBoundsPenalty     = -1f;

    [Header("Episode Control")]
    [Tooltip("Used only when Agent.MaxStep is 0 (unlimited).")]
    public int fallbackMaxStep = 800;

    [Header("Target Randomization")]
    [Tooltip("Minimum absolute Y offset between drone start and randomized target in non-maze mode.")]
    public float minTargetVerticalOffset = 2f;
    [Tooltip("Maximum absolute Y offset between drone start and randomized target in non-maze mode.")]
    public float maxTargetVerticalOffset = 6f;

    [Header("Episode Recording")]
    public bool saveEpisodeResults = true;

    [Header("Maze Configuration")]
    [Tooltip("Optional. If provided, the drone will query this for start/exit rooms and regenerate it per episode.")]
    public MazeDensity mazeDensity;

    [Header("Curriculum")]
    [Tooltip("Number of episodes over which difficulty ramps from 1-hop target to full exit.")]
    public int curriculumEpisodes = 3000;

    // --------------------------------------------------------- private
    private Rigidbody       _rb;
    private SharedDronePhysics _sharedPhysics;
    private Quaternion      _initRotation;
    private Lidar3DScanner  _lidar;
    private AStarPathfinder _astar;
    private PathTracker     _tracker;

    /// <summary>
    /// The root of this environment instance. The drone and target are children of this.
    /// All localPosition operations are relative to this transform.
    /// </summary>
    private Transform       _envRoot;

    private List<Vector3>   _path            = new List<Vector3>();
    private int             _waypointIdx     = 0;
    private float           _timeSinceRepath = 0f;
    private int             _episodeCount    = 0;
    private float[]         _prevActions     = new float[4];
    private float           _bestPathCompletion = 0f;
    private float           _prevDistanceToTarget = 0f;

    /// <summary>
    /// Tracks the current curriculum graph distance used as target.
    /// </summary>
    private int             _curriculumTargetDist = 1;

    private void ApplySharedPhysicsSettings()
    {
        if (_sharedPhysics == null) return;

        _sharedPhysics.Configure(
            physicsAcceleration,
            physicsMaxSpeed,
            physicsHorizontalDamping,
            physicsHorizontalDrag,
            physicsRotateSpeed,
            physicsHoverThrust,
            physicsThrustPower,
            physicsDamping,
            physicsGroundLayer,
            physicsGroundCheckDistance,
            physicsAllowYawWhenGrounded);
    }

    private void OnValidate()
    {
        _sharedPhysics = GetComponent<SharedDronePhysics>();
        ApplySharedPhysicsSettings();

        fallbackMaxStep = Mathf.Max(0, fallbackMaxStep);
        minTargetVerticalOffset = Mathf.Max(0f, minTargetVerticalOffset);
        maxTargetVerticalOffset = Mathf.Max(minTargetVerticalOffset, maxTargetVerticalOffset);
        curriculumEpisodes = Mathf.Max(1, curriculumEpisodes);
    }

    // ================================================================ Init
    public override void Initialize()
    {
        _rb           = GetComponent<Rigidbody>();
        _sharedPhysics = GetComponent<SharedDronePhysics>() ?? gameObject.AddComponent<SharedDronePhysics>();
        ApplySharedPhysicsSettings();
        _initRotation = transform.localRotation;
        _lidar        = GetComponent<Lidar3DScanner>();

        // --- Environment root ---
        // The drone must be a direct child of the environment root GameObject.
        // e.g. hierarchy:  EnvironmentRoot -> Drone
        //                  EnvironmentRoot -> Target
        //                  EnvironmentRoot -> Walls/...
        _envRoot = transform.parent;
        if (_envRoot == null)
            Debug.LogError("[DroneAgent] Drone has no parent! It must be a child of an environment root GameObject.");

        // --- Auto-find target if not assigned ---
        // Searches siblings (children of the same parent) for a GameObject tagged "Target".
        if (target == null && _envRoot != null)
        {
            foreach (Transform sibling in _envRoot)
            {
                try
                {
                    if (sibling.CompareTag("Target"))
                    {
                        target = sibling;
                        break;
                    }
                }
                catch (UnityException)
                {
                    // Fallback if "Target" tag is literally not defined in the Project Settings
                    if (sibling.name.Contains("Target"))
                    {
                        target = sibling;
                        break;
                    }
                }
            }

            if (target == null)
            {
                if (mazeDensity != null)
                {
                    // Create a dummy target object if we are using the maze generator
                    GameObject dummyTarget = new GameObject("MazeTargetDummy");
                    dummyTarget.tag = "Target";
                    dummyTarget.transform.SetParent(_envRoot);
                    target = dummyTarget.transform;
                }
                else
                {
                    Debug.LogError("[DroneAgent] Could not find a child of the environment root tagged 'Target'. " +
                                   "Assign it manually or tag it correctly.");
                }
            }
        }

        if (_lidar == null)
            Debug.LogError("[DroneAgent] Lidar3DScanner not found!");

        if (useAStarPathfinding)
            _astar = GetComponent<AStarPathfinder>() ?? gameObject.AddComponent<AStarPathfinder>();

        _tracker = GetComponent<PathTracker>() ?? gameObject.AddComponent<PathTracker>();

        if (saveEpisodeResults)
            EpisodeResultSaver.ResetIterationCounter();
    }

    // ============================================================ Episode
    public override void OnEpisodeBegin()
    {
        if (target == null) { Debug.LogError("[DroneAgent] Target is not assigned!"); return; }

        _episodeCount++;

        if (mazeDensity != null)
        {
            mazeDensity.RebuildImmediate();

            MeshGenerator meshGen = null;
            if (_envRoot != null)
                meshGen = _envRoot.GetComponentInChildren<MeshGenerator>();
            
            if (meshGen != null)
                meshGen.RequestMeshUpdate();

            // Start Room placement
            Vector3 startPos = mazeDensity.GetStartRoomCentre();
            transform.localPosition = startPos;
            
            // ── Curriculum target placement ──────────────────────────
            // Ramp from 1-hop neighbour to full maze exit over curriculumEpisodes.
            int maxDist = mazeDensity.GetMaxGraphDistance();
            float progress = Mathf.Clamp01((float)_episodeCount / (float)curriculumEpisodes);
            _curriculumTargetDist = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Lerp(1f, maxDist, progress)),
                1, maxDist);

            Vector3 targetPos = mazeDensity.GetRoomCentreAtGraphDistance(_curriculumTargetDist);
            target.localPosition = targetPos;
        }
        else
        {
            // Reset drone to the origin of its own environment root (local space).
            transform.localPosition = new Vector3(8f, -13f, -10f);
            RandomizeTarget();
        }

        transform.localRotation = _initRotation;
        _rb.linearVelocity            = Vector3.zero;
        _rb.angularVelocity     = Vector3.zero;
        _sharedPhysics.SetControlInput(Vector2.zero, 0f, 0f);
        _sharedPhysics.ResetRuntimeState();

        _lidar?.ResetScan();

        _tracker.StartTracking(transform.position, target.position);
        _bestPathCompletion = 0f;
        _prevDistanceToTarget = Vector3.Distance(transform.position, target.position);

        if (useAStarPathfinding && _astar != null)
            RecalculatePath();

        _timeSinceRepath = 0f;
        // Debug.Log($"[DroneAgent] Episode {_episodeCount} start " +
        //           $"(env: {(_envRoot != null ? _envRoot.name : "none")}). " +
        //           $"Curriculum dist: {_curriculumTargetDist}. " +
        //           $"Path: {_path.Count} waypoints.");
    }

    // ========================================================== Observations
    // Observation budget (must match Behavior Parameters → Vector Observations):
    //   3   target direction (agent-local)
    //   1   target distance
    //   3   velocity
    //   1   yaw rate
    //  10   transform / progress (PathTracker)
    //   3   A* direction to next waypoint
    //   3   A* direction to final waypoint
    //   1   path-completion ratio
    //   1   normalised distance to next waypoint
    //   1   remaining waypoints ratio
    //  60   last-20-step path history (20 × 3)
    //  32   LiDAR distances
    // ---
    // 119  TOTAL
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Target direction in drone-local frame (3) + distance (1)
        Vector3 toTargetWorld = target.position - transform.position;
        Vector3 toTargetLocalDir = toTargetWorld.sqrMagnitude > 0.0001f
            ? transform.InverseTransformDirection(toTargetWorld.normalized)
            : Vector3.zero;
        sensor.AddObservation(toTargetLocalDir);
        sensor.AddObservation(Mathf.Clamp01(toTargetWorld.magnitude / 50f));

        // 2. Velocity (3) + yaw rate (1)
        sensor.AddObservation(_rb.linearVelocity);
        float normalizedYawRate = Mathf.Clamp(_rb.angularVelocity.y / Mathf.Max(0.01f, excessYawRateThreshold), -1f, 1f);
        sensor.AddObservation(normalizedYawRate);

        // 3. Transform / progress (10)
        float[] xfObs = _tracker.GetTransformAsObservations();
        foreach (float v in xfObs) sensor.AddObservation(v);

        // 4. A* guidance (9)
        if (useAStarPathfinding && _path.Count > 0 && _astar != null)
        {
            Vector3 toWaypointWorld = _astar.GetPathDirection(transform.position, _path, _waypointIdx);
            Vector3 toGoalWorld = _astar.GetPathDirection(transform.position, _path, _path.Count - 1);
            sensor.AddObservation(transform.InverseTransformDirection(toWaypointWorld));
            sensor.AddObservation(transform.InverseTransformDirection(toGoalWorld));
            sensor.AddObservation(_tracker.GetPathCompletion());

            float dWp = Vector3.Distance(transform.position,
                _path[Mathf.Clamp(_waypointIdx, 0, _path.Count - 1)]);
            sensor.AddObservation(Mathf.Clamp01(dWp / 10f));

            int remaining = Mathf.Max(0, _path.Count - _waypointIdx);
            sensor.AddObservation(remaining / (float)Mathf.Max(1, _path.Count));
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // 5. Path history – last 20 steps (60)
        float[] pathObs = _tracker.GetPathAsObservations(20);
        foreach (float v in pathObs) sensor.AddObservation(v);

        // 6. LiDAR (32)
        AddLidarObservations(sensor);
    }

    private void AddLidarObservations(VectorSensor sensor)
    {
        float fov      = _lidar != null ? _lidar.fieldOfView : 90f;
        int   gridSize = Mathf.CeilToInt(Mathf.Sqrt(lidarObservationPoints));
        int   cast     = 0;

        for (int x = 0; x < gridSize && cast < lidarObservationPoints; x++)
        for (int y = 0; y < gridSize && cast < lidarObservationPoints; y++)
        {
            float ax    = Mathf.Lerp(-fov * 0.5f, fov * 0.5f, (float)x / (gridSize - 1));
            float ay    = Mathf.Lerp(-fov * 0.5f, fov * 0.5f, (float)y / (gridSize - 1));
            Vector3 dir = Quaternion.Euler(ay, ax, 0f) * transform.forward;

            float norm = Physics.Raycast(transform.position, dir, out RaycastHit hit, lidarMaxDistance)
                ? hit.distance / lidarMaxDistance
                : 1f;

            sensor.AddObservation(norm);
            cast++;
        }

        while (cast < lidarObservationPoints)
        {
            sensor.AddObservation(1f);
            cast++;
        }
    }

    // ============================================================= Actions
    public override void OnActionReceived(ActionBuffers actions)
    {
        float mx = actions.ContinuousActions[0];
        float my = actions.ContinuousActions[1];
        float mz = actions.ContinuousActions[2];
        float rz = actions.ContinuousActions[3];

        if (_episodeCount > 0)
        {
            float actionDiff = 0f;
            for (int i = 0; i < 4; i++)
            {
                actionDiff += Mathf.Abs(actions.ContinuousActions[i] - _prevActions[i]);
            }
            AddReward(actionDiff * choppyMovementPenalty); // choppyMovementPenalty is negative
        }
        for (int i = 0; i < 4; i++)
        {
            _prevActions[i] = actions.ContinuousActions[i];
        }

        _sharedPhysics.SetControlInput(new Vector2(mx, mz), my, rz);
        _sharedPhysics.SimulateStep();

        _tracker.RecordStep(transform.position);
        _tracker.UpdateWaypointProgress(transform.position);

        if (_path.Count > 0 && _waypointIdx < _path.Count)
        {
            if (Vector3.Distance(transform.position, _path[_waypointIdx]) < waypointReachedDistance)
                _waypointIdx = Mathf.Min(_waypointIdx + 1, _path.Count - 1);
        }

        _timeSinceRepath += Time.fixedDeltaTime;
        if (useAStarPathfinding && _astar != null && _timeSinceRepath >= pathRecalculationInterval)
        {
            _tracker.IncrementAttemptCount();
            RecalculatePath();
            _timeSinceRepath = 0f;
        }

        // ── Per-step time penalty ──
        AddReward(stepPenalty);

        // ── Distance-to-target shaping (primary reward signal) ──
        float currentDist = Vector3.Distance(transform.position, target.position);
        float distanceDelta = _prevDistanceToTarget - currentDist; // positive = getting closer
        AddReward(distanceDelta * distanceRewardScale);
        _prevDistanceToTarget = currentDist;

        // ── A* waypoint progress (secondary signal) ──
        float completion = _tracker.GetPathCompletion();
        if (completion > _bestPathCompletion)
        {
            float completionDelta = completion - _bestPathCompletion;
            AddReward(completionDelta * progressRewardScale);
            _bestPathCompletion = completion;
        }

        // ── Heading alignment (minor auxiliary signal) ──
        Vector3 toTargetWorld = target.position - transform.position;
        if (toTargetWorld.sqrMagnitude > 0.0001f)
        {
            float headingAlignment = Vector3.Dot(transform.forward, toTargetWorld.normalized);
            AddReward(headingAlignment * headingAlignmentRewardScale);
        }

        // ── Excess yaw penalty ──
        float excessYawRate = Mathf.Abs(_rb.angularVelocity.y) - excessYawRateThreshold;
        if (excessYawRate > 0f)
            AddReward(-excessYawRate * excessYawRatePenaltyScale);

        // ── Out-of-bounds check (uses localPosition → works for every env copy) ──
        if (transform.localPosition.y > 180f || transform.localPosition.y < -180f)
        {
            AddReward(outOfBoundsPenalty);
            EndEpisode();
        }
        
        // ── Target Recognition ──
        bool targetReached = false;

        if (mazeDensity != null)
        {
            Vector3 currTargetCentre = mazeDensity.GetRoomCentreAtGraphDistance(_curriculumTargetDist);
            float currTargetSize = mazeDensity.GetRoomSizeAtGraphDistance(_curriculumTargetDist);
            float dist = Vector3.Distance(transform.localPosition, currTargetCentre);
            targetReached = dist <= currTargetSize;
        }
        else if (target != null)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            targetReached = dist <= targetReachedRadius;
        }

        if (targetReached)
        {
            transform.localRotation = _initRotation;
            _rb.angularVelocity = Vector3.zero;

            Debug.Log($"[DroneAgent] Episode {_episodeCount}: TARGET (or ROOM) reached! (curriculum dist: {_curriculumTargetDist})");
            AddReward(targetReward);

            if (saveEpisodeResults && _tracker != null)
                EpisodeResultSaver.SaveEpisodeResult(_tracker, _path, transform.position);

            _tracker.StopTracking();
            
            if (mazeDensity == null)
            {
                RandomizeTarget();
            }
            
            EndEpisode();
            return;
        }

        // No hard step timeout — the per-step penalty (stepPenalty) provides
        // a soft incentive to finish quickly. Set MaxStep = 0 in Inspector.
    }

    // =========================================================== Collisions
    private void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled) return;

        GameObject other = collision.gameObject;

        try
        {
            // The logic for hitting the Target is now handled dynamically in OnActionReceived
            // via a distance check so the drone doesn't need to physically collide with it.
            
            if (other.CompareTag("Wall"))
            {
                // Penalize wall collision but do NOT end the episode.
                // The tunnels between rooms are narrow — the drone must learn to
                // recover from grazes rather than being killed instantly.
                AddReward(wallPenalty);
            }
        }
        catch (UnityException)
        {
            // Ignore tag undefined errors during collisions
        }
    }

    // ============================================================= Helpers
    private void RecalculatePath()
    {
        _astar.UpdateGridWithLidarData(transform.position, lidarMaxDistance, lidarObservationPoints);
        _path        = _astar.FindPath(transform.position, target.position);
        _waypointIdx = 0;
        _tracker.SetPlannedPath(_path);
    }

    /// <summary>
    /// Randomizes target position in LOCAL space of the environment root.
    /// Works correctly for every duplicate because localPosition is relative
    /// to the environment's own root, not world origin.
    /// </summary>
    private void RandomizeTarget()
    {
        float x = Random.Range(-20f, 32f);
        float z = Random.Range(-37f, 14f);

        float minDelta = Mathf.Max(0f, minTargetVerticalOffset);
        float maxDelta = Mathf.Max(minDelta, maxTargetVerticalOffset);
        float signedDelta = Random.value < 0.5f
            ? -Random.Range(minDelta, maxDelta)
            : Random.Range(minDelta, maxDelta);

        float baseY = transform.localPosition.y;
        float y = Mathf.Clamp(baseY + signedDelta, -13f, -3f);

        // If clamping collapsed the offset, force a meaningful vertical gap.
        if (Mathf.Abs(y - baseY) < minDelta * 0.5f)
        {
            float forcedDelta = baseY <= -8f ? minDelta : -minDelta;
            y = Mathf.Clamp(baseY + forcedDelta, -13f, -3f);
        }

        target.localPosition = new Vector3(
            x,
            y,
            z);
    }

    // ============================================================= Heuristic
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxisRaw("Horizontal");
        ca[2] = Input.GetAxisRaw("Vertical");
        ca[1] = Input.GetKey(KeyCode.Space) ? 1f : Input.GetKey(KeyCode.LeftShift) ? -1f : 0f;
        ca[3] = Input.GetKey(KeyCode.E)     ? 1f : Input.GetKey(KeyCode.Q) ? -1f : 0f;
    }
}