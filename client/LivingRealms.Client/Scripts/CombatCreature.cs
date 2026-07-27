using Godot;

namespace LivingRealms.Client;

public partial class CombatCreature : CharacterBody3D
{
    public event Action<string>? ResourceWorkPulse;
    private Node3D _visualRoot = null!;
    private Label3D _statusLabel = null!;
    private Label3D _healthBarLabel = null!;
    private Label3D _targetMarker = null!;
    private MeshInstance3D _targetGroundRing = null!;
    private CollisionShape3D _collider = null!;
    private ThirdPersonPlayer _player = null!;
    private WorldPathfinder _pathfinder = null!;
    private Vector3 _spawnPosition;
    private Vector3 _raidObjective;
    private IReadOnlyList<Vector3> _raidRoute = [];
    private int _raidRouteIndex;
    private Vector3 _raidApproachOffset;
    private Vector3 _playerApproachOffset;
    private SettlementNpc? _raidDefenseTarget;
    private float _attackCooldown;
    private float _gravity = 9.8f;
    private float _stuckSeconds;
    private IReadOnlyList<Vector3> _plannedPath = [];
    private int _pathWaypoint;
    private float _pathRefreshSeconds;
    private Vector3 _lastPathTarget;
    private bool _pathReturningHome;
    private bool _engagedWithPlayer;
    private float _navigationRadius = 0.8f;
    private readonly List<Vector3> _failedWaypoints = [];
    private float _failedWaypointSeconds;
    private int _consecutiveStuckReplans;
    private Vector3 _progressGoal;
    private float _bestProgressDistance = float.MaxValue;
    private float _noProgressSeconds;
    private bool _hasProgressGoal;
    private readonly RandomNumberGenerator _wanderRandom = new();
    private Vector3 _wanderTarget;
    private float _wanderPauseSeconds;
    private float _wanderRadius;
    private bool _hasWanderTarget;
    private IReadOnlyList<Vector3> _campDutyRoute = [];
    private int _campDutyRouteIndex;
    private float _campDutyPauseSeconds;
    private string _campDuty = string.Empty;
    private Node3D? _leftArmPivot;
    private Node3D? _rightArmPivot;
    private Node3D? _leftLegPivot;
    private Node3D? _rightLegPivot;
    private Node3D? _frontLeftLegPivot;
    private Node3D? _frontRightLegPivot;
    private Node3D? _backLeftLegPivot;
    private Node3D? _backRightLegPivot;
    private Node3D? _headPivot;
    private Node3D? _tailPivot;
    private Node3D? _realisticCreatureModel;
    private Skeleton3D? _realisticGoblinSkeleton;
    private bool _usesRealisticGoblin;
    private bool _usesBlenderCreatureAsset;
    private float _realisticLocomotionBlend;
    private float _motionClock;
    private float _attackAnimationRemaining;
    private bool _showDetailedOverhead;
    private const float AttackAnimationDuration = 0.62f;

    public Guid CreatureId { get; private set; }
    public string SpeciesKey { get; private set; } = string.Empty;
    public string SpeciesName { get; private set; } = string.Empty;
    public string CreatureName { get; private set; } = string.Empty;
    public string CreatureRole { get; private set; } = string.Empty;
    public string CreatureTitle { get; private set; } = string.Empty;
    public int Level { get; private set; }
    public int Health { get; private set; }
    public int MaximumHealth { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public float MovementSpeed { get; private set; }
    public float DetectionRadius { get; private set; }
    public float AttackRange { get; private set; }
    public bool IsBoss { get; private set; }
    public bool IsRaidAttacker { get; private set; }
    public bool IsDarkwoodClanMember => SpeciesKey.StartsWith("goblin-", StringComparison.OrdinalIgnoreCase);
    public bool IsAlive { get; private set; }
    public bool IsPlayerSelected { get; private set; }
    public bool AiEnabled { get; set; } = true;
    public bool PlayerTargetable { get; set; } = true;
    public bool IsEngagedWithPlayer => IsAlive && _engagedWithPlayer;
    public bool HasSettlementDefenseTarget =>
        IsInstanceValid(_raidDefenseTarget) && _raidDefenseTarget!.IsRaidDefender;

    public event Action<Guid>? AttackPlayerRequested;
    public event Action? RaidCombatPulse;

    public void Configure(WorldCreatureData data, ThirdPersonPlayer player, WorldPathfinder pathfinder)
    {
        CreatureId = data.Id;
        SpeciesKey = data.SpeciesKey;
        SpeciesName = data.SpeciesName;
        CreatureName = data.Name;
        CreatureRole = data.Role ?? data.Title ?? string.Empty;
        CreatureTitle = data.Title ?? string.Empty;
        Level = data.Level;
        Health = data.Health;
        MaximumHealth = data.MaximumHealth;
        Attack = data.Attack;
        Defense = data.Defense;
        MovementSpeed = data.MovementSpeed;
        DetectionRadius = data.DetectionRadius;
        AttackRange = data.AttackRange;
        IsBoss = data.IsBoss;
        IsRaidAttacker = data.IsRaidAttacker;
        IsAlive = data.Status.Equals("Alive", StringComparison.OrdinalIgnoreCase);
        _player = player;
        _pathfinder = pathfinder;
        _spawnPosition = data.SpawnPosition;
        _wanderRandom.Seed = BitConverter.ToUInt64(data.Id.ToByteArray(), 0);
        _wanderPauseSeconds = _wanderRandom.RandfRange(0.5f, 2.5f);
        var raidLane = data.Id.ToByteArray()[0] % 4;
        var laneOffset = new[] { -2.4f, -0.8f, 0.8f, 2.4f }[raidLane];
        _raidObjective = new Vector3(laneOffset, 0.08f, -4.0f);
        if (IsRaidAttacker)
        {
            _raidRoute =
            [
                new Vector3(-98.0f + laneOffset * 0.45f, 0.08f, -98.0f),
                new Vector3(-96.0f + laneOffset * 0.45f, 0.08f, 10.0f),
                new Vector3(-42.0f, 0.08f, 12.0f + laneOffset * 0.55f),
                new Vector3(-12.0f, 0.08f, 11.0f + laneOffset * 0.45f)
            ];
        }
        var idBytes = data.Id.ToByteArray();
        var approachSlot = BitConverter.ToUInt16(idBytes, 0) % 32;
        var approachRing = idBytes[2] % 3;
        var approachAngle = approachSlot * Mathf.Tau / 32.0f;
        var approachRadius = Mathf.Max(1.35f, AttackRange * 0.78f) + approachRing * 0.65f;
        _raidApproachOffset = new Vector3(
            Mathf.Cos(approachAngle) * approachRadius,
            0,
            Mathf.Sin(approachAngle) * approachRadius);
        var playerAngle = (approachSlot + 11) % 32 * Mathf.Tau / 32.0f;
        var playerRadius = Mathf.Min(
            Mathf.Max(1.2f, AttackRange * 0.72f) + approachRing * 0.45f,
            AttackRange + 0.35f);
        _playerApproachOffset = new Vector3(
            Mathf.Cos(playerAngle) * playerRadius,
            0,
            Mathf.Sin(playerAngle) * playerRadius);
        Position = pathfinder.GetNearestWalkablePosition(data.Position);
        _navigationRadius = SpeciesKey switch
        {
            "forest-rat" => 0.42f,
            "prairie-wolf" => 0.72f,
            "goblin-raider" => 1.0f,
            "goblin-chief" => 1.35f,
            _ => 0.8f
        };
        _wanderRadius = SpeciesKey switch
        {
            "forest-rat" => 5.5f,
            "prairie-wolf" => 7.0f,
            "goblin-raider" => 5.0f,
            "goblin-chief" => 4.0f,
            _ => 5.0f
        };
        ConfigureDarkwoodCampDuty();
    }

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2 | 4 | 8;
        FloorSnapLength = 0.25f;
        FloorMaxAngle = Mathf.DegToRad(48.0f);
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        BuildModel();
        ApplyAliveState();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateOverheadVisibility();
        if (!IsAlive || !AiEnabled || !IsInstanceValid(_player))
        {
            Velocity = Vector3.Zero;
            return;
        }

        var seconds = (float)delta;
        if (GlobalPosition.Y < -1.5f)
        {
            var recoveryPosition = GlobalPosition.IsFinite()
                ? new Vector3(
                    Mathf.Clamp(GlobalPosition.X, -139.0f, 139.0f),
                    0.08f,
                    Mathf.Clamp(GlobalPosition.Z, -139.0f, 139.0f))
                : _spawnPosition;
            GlobalPosition = _pathfinder.GetNearestWalkablePosition(recoveryPosition);
            Velocity = Vector3.Zero;
            _stuckSeconds = 0;
            _consecutiveStuckReplans = 0;
            ResetProgressWatchdog();
            InvalidatePath();
            return;
        }
        if (_failedWaypointSeconds > 0)
        {
            _failedWaypointSeconds = Mathf.Max(0, _failedWaypointSeconds - seconds);
            if (_failedWaypointSeconds <= 0)
            {
                _failedWaypoints.Clear();
            }
        }
        _attackCooldown = Mathf.Max(0, _attackCooldown - seconds);
        var velocity = Velocity;
        velocity.Y = IsOnFloor() ? -0.1f : velocity.Y - _gravity * seconds;

        var playerOffset = _player.GlobalPosition - GlobalPosition;
        playerOffset.Y = 0;
        var playerDistance = playerOffset.Length();
        var homeOffset = _spawnPosition - GlobalPosition;
        homeOffset.Y = 0;
        if (!PlayerTargetable)
        {
            _engagedWithPlayer = false;
        }
        else if (!_engagedWithPlayer && playerDistance <= (IsRaidAttacker ? AttackRange + 1.0f : DetectionRadius))
        {
            _engagedWithPlayer = true;
        }
        else if (_engagedWithPlayer && playerDistance > DetectionRadius + 3.0f)
        {
            _engagedWithPlayer = false;
        }

        var hasDefender = HasSettlementDefenseTarget;
        if (hasDefender)
        {
            // A Stonehaven guard has intercepted this creature. The defender
            // becomes its immediate threat so a boss cannot keep striking the
            // player while guards merely stand beside the fight.
            _engagedWithPlayer = false;
        }

        Vector3 movement;
        if (hasDefender)
        {
            var defenderPosition = _raidDefenseTarget!.GlobalPosition;
            var approachPosition = defenderPosition + _raidApproachOffset;
            var defenderOffset = defenderPosition - GlobalPosition;
            defenderOffset.Y = 0;
            var defenderDistance = defenderOffset.Length();
            var approachDistance = HorizontalDistance(GlobalPosition, approachPosition);
            var hasLineOfSight = HasClearWorldPath(defenderPosition);
            movement = approachDistance > 0.55f || !hasLineOfSight
                ? GetPathDirection(approachPosition, seconds, returningHome: false)
                : Vector3.Zero;
            if (hasLineOfSight && defenderDistance <= AttackRange + 0.9f && _attackCooldown <= 0)
            {
                _attackCooldown = 1.45f;
                PulseAttack();
                _raidDefenseTarget.ReceiveRaidAttackPulse();
            }
        }
        else if (_engagedWithPlayer)
        {
            var hasLineOfSight = HasClearWorldPath(_player.GlobalPosition);
            var playerApproach = _pathfinder.GetNearestWalkablePosition(
                _player.GlobalPosition + _playerApproachOffset);
            var approachDistance = HorizontalDistance(GlobalPosition, playerApproach);
            movement = approachDistance > 0.55f || !hasLineOfSight
                ? GetPathDirection(playerApproach, seconds, returningHome: false)
                : Vector3.Zero;
            if (hasLineOfSight && playerDistance <= AttackRange + 0.6f && _attackCooldown <= 0)
            {
                _attackCooldown = 1.45f;
                AttackPlayerRequested?.Invoke(CreatureId);
                PulseAttack();
            }
        }
        else
        {
            if (IsRaidAttacker)
            {
                movement = GetRaidMarchMovement(seconds);
            }
            else if (_campDutyRoute.Count > 0)
            {
                movement = GetCampDutyMovement(seconds);
            }
            else
            {
                movement = GetIdleMovement(homeOffset, seconds);
            }
        }

        velocity.X = Mathf.MoveToward(velocity.X, movement.X * MovementSpeed, 12.0f * seconds);
        velocity.Z = Mathf.MoveToward(velocity.Z, movement.Z * MovementSpeed, 12.0f * seconds);
        if (IsOnFloor() && _stuckSeconds >= 0.18f && CanHopForward(movement))
        {
            velocity.Y = SpeciesKey == "forest-rat" ? 4.2f : 4.8f;
            _stuckSeconds = 0;
        }
        var facingDirection = movement;
        if (_engagedWithPlayer)
        {
            facingDirection = playerOffset;
        }
        else if (hasDefender)
        {
            facingDirection = _raidDefenseTarget!.GlobalPosition - GlobalPosition;
            facingDirection.Y = 0;
        }
        if (facingDirection.LengthSquared() > 0.001f)
        {
            // Godot's visual forward axis is -Z. Face that axis toward travel so
            // beasts and goblins do not appear to run backward along their path.
            var targetRotation = _usesBlenderCreatureAsset
                ? Mathf.Atan2(facingDirection.X, facingDirection.Z)
                : Mathf.Atan2(-facingDirection.X, -facingDirection.Z);
            _visualRoot.Rotation = new Vector3(
                0,
                Mathf.LerpAngle(_visualRoot.Rotation.Y, targetRotation,
                    (_engagedWithPlayer || hasDefender || IsRaidAttacker ? 12.0f : 8.0f) * seconds),
                0);
        }

        Velocity = velocity;
        var positionBeforeMove = GlobalPosition;
        MoveAndSlide();
        UpdateStuckRecovery(movement, positionBeforeMove, seconds);
        UpdateMotion(seconds);
    }

    private Vector3 GetIdleMovement(Vector3 homeOffset, float seconds)
    {
        if (homeOffset.Length() > _wanderRadius * 1.6f)
        {
            _hasWanderTarget = false;
            return GetPathDirection(_spawnPosition, seconds, returningHome: true);
        }

        _wanderPauseSeconds = Mathf.Max(0, _wanderPauseSeconds - seconds);
        if (!_hasWanderTarget)
        {
            if (_wanderPauseSeconds > 0)
            {
                return Vector3.Zero;
            }

            var angle = _wanderRandom.RandfRange(0, Mathf.Tau);
            var distance = _wanderRandom.RandfRange(_wanderRadius * 0.35f, _wanderRadius);
            var candidate = _spawnPosition + new Vector3(
                Mathf.Cos(angle) * distance,
                0,
                Mathf.Sin(angle) * distance);
            _wanderTarget = _pathfinder.GetNearestWalkablePosition(candidate);
            _hasWanderTarget = true;
            InvalidatePath();
        }

        if (HorizontalDistance(GlobalPosition, _wanderTarget) <= 0.85f)
        {
            _hasWanderTarget = false;
            _wanderPauseSeconds = _wanderRandom.RandfRange(1.5f, 4.5f);
            InvalidatePath();
            return Vector3.Zero;
        }

        return GetPathDirection(_wanderTarget, seconds, returningHome: true);
    }

    private Vector3 GetRaidMarchMovement(float seconds)
    {
        while (_raidRouteIndex < _raidRoute.Count &&
               HorizontalDistance(GlobalPosition, _raidRoute[_raidRouteIndex]) <= 1.65f)
        {
            _raidRouteIndex++;
            InvalidatePath();
        }

        var destination = _raidRouteIndex < _raidRoute.Count
            ? _raidRoute[_raidRouteIndex]
            : _raidObjective;
        return HorizontalDistance(GlobalPosition, destination) > 1.25f
            ? GetPathDirection(destination, seconds, returningHome: false)
            : Vector3.Zero;
    }

    private void ConfigureDarkwoodCampDuty()
    {
        if (IsRaidAttacker ||
            SpeciesKey is not ("goblin-raider" or "goblin-chief") ||
            _spawnPosition.X > -80.0f || _spawnPosition.Z > -70.0f)
        {
            return;
        }

        if (CreatureName.Equals("Skrit", StringComparison.OrdinalIgnoreCase))
        {
            _campDuty = "CUTTING DARKWOOD TIMBER";
            _campDutyRoute =
            [
                new Vector3(-131.5f, 0.08f, -91.0f),
                new Vector3(-125.0f, 0.08f, -106.0f)
            ];
        }
        else if (CreatureName.Equals("Vrak", StringComparison.OrdinalIgnoreCase))
        {
            _campDuty = "MINING DARKWOOD STONE";
            _campDutyRoute =
            [
                new Vector3(-129.5f, 0.08f, -126.0f),
                new Vector3(-108.0f, 0.08f, -109.0f)
            ];
        }
        else if (IsBoss)
        {
            _campDuty = "COMMANDING THE CAMP";
            _campDutyRoute =
            [
                new Vector3(-116.0f, 0.08f, -112.0f),
                new Vector3(-116.0f, 0.08f, -108.0f),
                new Vector3(-120.0f, 0.08f, -101.0f),
                new Vector3(-111.0f, 0.08f, -101.0f)
            ];
        }
        else
        {
            var lane = CreatureId.ToByteArray()[2] % 3 - 1;
            switch (CreatureRole)
            {
                case "Woodcutter":
                    _campDuty = "HAULING DARKWOOD TIMBER";
                    _campDutyRoute =
                    [
                        new Vector3(-132.0f, 0.08f, -96.0f + lane * 2.0f),
                        new Vector3(-124.0f, 0.08f, -106.0f + lane)
                    ];
                    break;
                case "Stone Gatherer":
                    _campDuty = "HAULING QUARRIED STONE";
                    _campDutyRoute =
                    [
                        new Vector3(-129.0f + lane, 0.08f, -123.0f),
                        new Vector3(-108.0f, 0.08f, -109.0f + lane)
                    ];
                    break;
                case "Clan Hunter":
                    _campDuty = "CHECKING HUNTING TRAPS";
                    _campDutyRoute =
                    [
                        new Vector3(-130.0f, 0.08f, -94.0f + lane * 2.0f),
                        new Vector3(-104.0f, 0.08f, -101.0f + lane * 2.0f)
                    ];
                    break;
                case "Scout":
                    _campDuty = "WATCHING THE STONEHAVEN ROAD";
                    _campDutyRoute =
                    [
                        new Vector3(-101.0f, 0.08f, -94.0f + lane * 2.0f),
                        new Vector3(-111.0f, 0.08f, -91.0f + lane)
                    ];
                    break;
                case "Camp Guard":
                    _campDuty = "PATROLLING THE PALISADE";
                    _campDutyRoute =
                    [
                        new Vector3(-130.0f, 0.08f, -104.0f + lane * 2.0f),
                        new Vector3(-116.0f + lane * 2.0f, 0.08f, -119.0f),
                        new Vector3(-102.0f, 0.08f, -104.0f - lane * 2.0f),
                        new Vector3(-116.0f - lane * 2.0f, 0.08f, -90.0f)
                    ];
                    break;
                default:
                    _campDuty = "TRAINING FOR THE NEXT RAID";
                    _campDutyRoute =
                    [
                        new Vector3(-121.0f, 0.08f, -100.0f + lane * 1.5f),
                        new Vector3(-111.0f, 0.08f, -100.0f + lane * 1.5f)
                    ];
                    break;
            }
        }
    }

    private Vector3 GetCampDutyMovement(float seconds)
    {
        _campDutyPauseSeconds = Mathf.Max(0, _campDutyPauseSeconds - seconds);
        if (_campDutyPauseSeconds > 0)
        {
            return Vector3.Zero;
        }

        var destination = _campDutyRoute[_campDutyRouteIndex];
        if (HorizontalDistance(GlobalPosition, destination) <= 0.9f)
        {
            if (_campDutyRouteIndex == 0 &&
                (CreatureName.Equals("Skrit", StringComparison.OrdinalIgnoreCase) ||
                 CreatureName.Equals("Vrak", StringComparison.OrdinalIgnoreCase)))
            {
                ResourceWorkPulse?.Invoke(CreatureName.ToLowerInvariant());
            }
            _campDutyRouteIndex = (_campDutyRouteIndex + 1) % _campDutyRoute.Count;
            _campDutyPauseSeconds = _wanderRandom.RandfRange(1.4f, 3.8f);
            InvalidatePath();
            return Vector3.Zero;
        }

        return GetPathDirection(destination, seconds, returningHome: true);
    }

    private bool CanHopForward(Vector3 movement)
    {
        if (movement.LengthSquared() < 0.01f || !IsInsideTree())
        {
            return false;
        }

        var direction = movement.Normalized();
        var distance = _navigationRadius + 0.75f;
        var lowStart = GlobalPosition + Vector3.Up * 0.22f;
        var highStart = GlobalPosition + Vector3.Up * 1.2f;
        return RayHitsWorld(lowStart, lowStart + direction * distance) &&
               !RayHitsWorld(highStart, highStart + direction * distance);
    }

    private Vector3 GetPathDirection(Vector3 target, float seconds, bool returningHome)
    {
        var direct = target - GlobalPosition;
        direct.Y = 0;
        if (direct.LengthSquared() < 0.001f)
        {
            return Vector3.Zero;
        }

        _pathRefreshSeconds = Mathf.Max(0, _pathRefreshSeconds - seconds);
        if (HasClearWorldPath(target))
        {
            var wasFollowingRoute = _plannedPath.Count > 0 || _stuckSeconds > 0;
            InvalidatePath();
            _stuckSeconds = 0;
            _consecutiveStuckReplans = 0;
            _failedWaypoints.Clear();
            _failedWaypointSeconds = 0;
            SetProgressGoal(target);
            if (wasFollowingRoute)
            {
                var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
                Velocity = new Vector3(
                    direct.Normalized().X * horizontalSpeed,
                    Velocity.Y,
                    direct.Normalized().Z * horizontalSpeed);
            }
            return direct.Normalized();
        }

        if (_plannedPath.Count > 0 && _pathReturningHome != returningHome)
        {
            InvalidatePath();
            _failedWaypoints.Clear();
            _failedWaypointSeconds = 0;
        }

        var targetMovedMaterially = HorizontalDistance(target, _lastPathTarget) > (IsBoss ? 5.0f : 4.0f);
        if (_plannedPath.Count == 0 || (_pathRefreshSeconds <= 0 && targetMovedMaterially))
        {
            _plannedPath = _pathfinder.FindPath(
                GlobalPosition,
                target,
                _failedWaypoints,
                _navigationRadius + 0.9f);
            _pathWaypoint = Math.Min(1, Math.Max(0, _plannedPath.Count - 1));
            _lastPathTarget = target;
            _pathReturningHome = returningHome;
            _pathRefreshSeconds = IsBoss ? 0.75f : 0.9f;
        }

        while (_pathWaypoint < _plannedPath.Count - 1 &&
               HorizontalDistance(GlobalPosition, _plannedPath[_pathWaypoint]) < 0.7f)
        {
            _pathWaypoint++;
            _consecutiveStuckReplans = 0;
            ResetProgressWatchdog();
        }
        while (_pathWaypoint < _plannedPath.Count - 1 &&
               HasClearWorldPath(_plannedPath[_pathWaypoint + 1]))
        {
            _pathWaypoint++;
        }

        if (_plannedPath.Count > 0 && _pathWaypoint < _plannedPath.Count)
        {
            if (_pathWaypoint == _plannedPath.Count - 1 &&
                HorizontalDistance(GlobalPosition, _plannedPath[_pathWaypoint]) < 0.7f)
            {
                InvalidatePath();
                return Vector3.Zero;
            }
            var waypointDirection = _plannedPath[_pathWaypoint] - GlobalPosition;
            waypointDirection.Y = 0;
            if (waypointDirection.LengthSquared() > 0.001f)
            {
                SetProgressGoal(_plannedPath[_pathWaypoint]);
                return waypointDirection.Normalized();
            }
        }

        InvalidatePath();
        ResetProgressWatchdog();
        return Vector3.Zero;
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private void InvalidatePath()
    {
        _plannedPath = [];
        _pathWaypoint = 0;
        _pathRefreshSeconds = 0;
    }

    private void SetProgressGoal(Vector3 goal)
    {
        if (_hasProgressGoal && HorizontalDistance(_progressGoal, goal) <= 0.75f)
        {
            return;
        }

        _progressGoal = goal;
        _bestProgressDistance = HorizontalDistance(GlobalPosition, goal);
        _noProgressSeconds = 0;
        _hasProgressGoal = true;
    }

    private void ResetProgressWatchdog()
    {
        _hasProgressGoal = false;
        _bestProgressDistance = float.MaxValue;
        _noProgressSeconds = 0;
    }

    private bool HasClearWorldPath(Vector3 target)
    {
        if (!IsInsideTree())
        {
            return true;
        }

        var from = GlobalPosition + Vector3.Up * 1.0f;
        var to = new Vector3(target.X, from.Y, target.Z);
        var direction = to - from;
        direction.Y = 0;
        if (direction.LengthSquared() < 0.001f)
        {
            return true;
        }

        var perpendicular = new Vector3(-direction.Z, 0, direction.X).Normalized();
        var clearance = _navigationRadius + 0.12f;
        return !RayHitsWorld(from, to) &&
               !RayHitsWorld(from + perpendicular * clearance, to + perpendicular * clearance) &&
               !RayHitsWorld(from - perpendicular * clearance, to - perpendicular * clearance);
    }

    private bool RayHitsWorld(Vector3 from, Vector3 to)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to, 1);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    private void UpdateStuckRecovery(Vector3 requestedMovement, Vector3 positionBeforeMove, float seconds)
    {
        if (requestedMovement.LengthSquared() < 0.01f)
        {
            _stuckSeconds = 0;
            ResetProgressWatchdog();
            return;
        }

        var distanceMoved = new Vector2(
            GlobalPosition.X - positionBeforeMove.X,
            GlobalPosition.Z - positionBeforeMove.Z).Length();
        _stuckSeconds = distanceMoved < Math.Max(0.004f, MovementSpeed * seconds * 0.08f)
            ? _stuckSeconds + seconds
            : Mathf.Max(0, _stuckSeconds - seconds * 2.0f);

        if (_hasProgressGoal)
        {
            var distanceToGoal = HorizontalDistance(GlobalPosition, _progressGoal);
            if (distanceToGoal < _bestProgressDistance - 0.08f)
            {
                _bestProgressDistance = distanceToGoal;
                _noProgressSeconds = 0;
            }
            else
            {
                _noProgressSeconds += seconds;
            }
        }

        if (_stuckSeconds < 0.6f && _noProgressSeconds < 0.85f)
        {
            return;
        }

        if (_plannedPath.Count > 0 && _pathWaypoint < _plannedPath.Count)
        {
            var failed = _plannedPath[_pathWaypoint];
            if (_failedWaypoints.All(existing => HorizontalDistance(existing, failed) > 1.0f))
            {
                _failedWaypoints.Add(failed);
                if (_failedWaypoints.Count > 4)
                {
                    _failedWaypoints.RemoveAt(0);
                }
            }
            _failedWaypointSeconds = 6.0f;
        }
        _consecutiveStuckReplans++;
        if (_consecutiveStuckReplans >= 3)
        {
            GlobalPosition = _pathfinder.GetNearestWalkablePosition(GlobalPosition);
            Velocity = Vector3.Zero;
            _consecutiveStuckReplans = 0;
        }
        InvalidatePath();
        _stuckSeconds = 0;
        ResetProgressWatchdog();
    }

    public void ApplyServerState(WorldCreatureData data, bool synchronizePosition = false)
    {
        var wasAlive = IsAlive;
        Health = data.Health;
        MaximumHealth = data.MaximumHealth;
        Level = data.Level;
        Attack = data.Attack;
        Defense = data.Defense;
        _spawnPosition = data.SpawnPosition;
        IsAlive = data.Status.Equals("Alive", StringComparison.OrdinalIgnoreCase);
        if (IsAlive && !IsInstanceValid(_visualRoot))
        {
            return;
        }

        if (IsAlive && (synchronizePosition || !wasAlive || !_visualRoot.Visible))
        {
            GlobalPosition = _pathfinder.GetNearestWalkablePosition(data.Position);
            Velocity = Vector3.Zero;
            _stuckSeconds = 0;
            _consecutiveStuckReplans = 0;
            _engagedWithPlayer = false;
            _failedWaypoints.Clear();
            _failedWaypointSeconds = 0;
            ResetProgressWatchdog();
            InvalidatePath();
        }
        ApplyAliveState();
    }

    public void FlashDamage()
    {
        if (!IsAlive || !IsInstanceValid(_visualRoot))
        {
            return;
        }

        if (IsRaidAttacker && PlayerTargetable)
        {
            _engagedWithPlayer = true;
            InvalidatePath();
        }

        PlayDamagePulse();
    }

    public void ReceiveDefenderAttackPulse()
    {
        if (!IsAlive || !IsInstanceValid(_visualRoot))
        {
            return;
        }

        PlayDamagePulse();
        RaidCombatPulse?.Invoke();
    }

    public void SetRaidDefenseTarget(SettlementNpc? target)
    {
        if (ReferenceEquals(_raidDefenseTarget, target))
        {
            return;
        }

        _raidDefenseTarget = target;
        InvalidatePath();
    }

    public void SetPlayerSelected(bool selected)
    {
        IsPlayerSelected = selected;
        UpdateOverheadVisibility();
    }

    public void SetOverheadDetail(bool showDetailed)
    {
        _showDetailedOverhead = showDetailed;
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Text = BuildStatusText();
            _statusLabel.FontSize = showDetailed ? (IsBoss ? 38 : 30) : (IsBoss ? 34 : 28);
        }
        UpdateOverheadVisibility();
    }

    private void PlayDamagePulse()
    {
        var baseScale = GetVisualBaseScale();
        var tween = CreateTween();
        tween.TweenProperty(_visualRoot, "scale", baseScale * 1.18f, 0.08);
        tween.TweenProperty(_visualRoot, "scale", baseScale, 0.12);
    }

    private void ApplyAliveState()
    {
        if (!IsInstanceValid(_visualRoot))
        {
            return;
        }

        _visualRoot.Visible = IsAlive;
        _collider.Disabled = !IsAlive;
        SetPhysicsProcess(IsAlive);
        if (IsAlive)
        {
            _statusLabel.Text = BuildStatusText();
            UpdateHealthBar();
        }
        else
        {
            Velocity = Vector3.Zero;
        }
        UpdateOverheadVisibility();
    }

    private string BuildStatusText()
    {
        var boss = IsBoss ? "★ " : string.Empty;
        var identityParts = new[] { CreatureRole, CreatureTitle }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var role = identityParts.Length == 0 ? SpeciesName : string.Join("  •  ", identityParts);
        if (!_showDetailedOverhead)
        {
            return $"{boss}{CreatureName}  •  {role}";
        }

        var activity = string.IsNullOrEmpty(_campDuty) ? string.Empty : $"\n{_campDuty}";
        var detailedRole = string.IsNullOrWhiteSpace(CreatureRole)
            ? SpeciesName.ToUpperInvariant()
            : CreatureRole.ToUpperInvariant();
        return IsBoss
            ? $"{boss}{CreatureName}  •  Lv {Level}\n{detailedRole}  •  HP {Health}/{MaximumHealth}  •  ATK {Attack}  •  DEF {Defense}{activity}"
            : $"{boss}{CreatureName}  •  Lv {Level}\n{detailedRole}  •  HP {Health}/{MaximumHealth}{activity}";
    }

    private void UpdateOverheadVisibility()
    {
        if (!IsInsideTree() ||
            !IsInstanceValid(_statusLabel) ||
            !IsInstanceValid(_player) ||
            !_player.IsInsideTree())
        {
            return;
        }

        var nearby = HorizontalDistance(GlobalPosition, _player.GlobalPosition) <= 28.0f;
        var visible = IsAlive && (nearby || IsPlayerSelected || _engagedWithPlayer);
        _statusLabel.Visible = visible;
        if (IsInstanceValid(_healthBarLabel))
        {
            _healthBarLabel.Visible = visible &&
                                      _showDetailedOverhead &&
                                      (nearby || IsPlayerSelected || Health < MaximumHealth);
        }
        if (IsInstanceValid(_targetMarker))
        {
            _targetMarker.Visible = IsAlive && IsPlayerSelected;
        }
        if (IsInstanceValid(_targetGroundRing))
        {
            _targetGroundRing.Visible = IsAlive && IsPlayerSelected;
        }
    }

    private void UpdateHealthBar()
    {
        if (!IsInstanceValid(_healthBarLabel))
        {
            return;
        }

        const int segments = 14;
        var ratio = Mathf.Clamp((float)Health / Math.Max(1, MaximumHealth), 0, 1);
        var filled = Math.Clamp((int)MathF.Ceiling(ratio * segments), 0, segments);
        _healthBarLabel.Text = $"[{new string('█', filled)}{new string('░', segments - filled)}]";
        _healthBarLabel.Modulate = ratio > 0.55f
            ? new Color("e4b94f")
            : ratio > 0.25f
                ? new Color("d87932")
                : new Color("e84d3d");
    }

    private void PulseAttack()
    {
        _attackAnimationRemaining = AttackAnimationDuration;
    }

    private void UpdateMotion(float seconds)
    {
        if (!IsInstanceValid(_visualRoot))
        {
            return;
        }

        if (_usesRealisticGoblin)
        {
            UpdateRealisticGoblinMotion(seconds);
            return;
        }

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var moving = horizontalSpeed > 0.16f;
        _motionClock += seconds * (moving ? (SpeciesKey == "forest-rat" ? 11.5f : 8.2f) : 1.8f);
        _attackAnimationRemaining = Mathf.Max(0, _attackAnimationRemaining - seconds);
        var stride = moving ? Mathf.Sin(_motionClock) : 0;
        var idle = Mathf.Sin(_motionClock * 0.68f);
        var lunge = 0.0f;

        if (SpeciesKey is "goblin-raider" or "goblin-chief")
        {
            if (!IsInstanceValid(_leftArmPivot) || !IsInstanceValid(_rightArmPivot) ||
                !IsInstanceValid(_leftLegPivot) || !IsInstanceValid(_rightLegPivot))
            {
                return;
            }

            var legSwing = stride * 0.58f;
            var armSwing = stride * 0.38f;
            var leftArm = new Vector3(-armSwing + idle * 0.018f, 0, -0.08f);
            var rightArm = new Vector3(armSwing - idle * 0.018f, 0, 0.08f);
            if (_attackAnimationRemaining > 0)
            {
                var progress = 1.0f - _attackAnimationRemaining / AttackAnimationDuration;
                var axeArc = progress < 0.28f
                    ? Mathf.Lerp(0.08f, 1.04f, progress / 0.28f)
                    : progress < 0.68f
                        ? Mathf.Lerp(1.04f, -1.46f, (progress - 0.28f) / 0.40f)
                        : Mathf.Lerp(-1.46f, 0.0f, (progress - 0.68f) / 0.32f);
                rightArm = new Vector3(axeArc, -0.20f, 0.50f);
                leftArm = new Vector3(-0.24f, 0.20f, -0.34f);
                var impact = Mathf.Clamp(1.0f - Mathf.Abs(progress - 0.62f) / 0.30f, 0, 1);
                lunge = -impact * (IsBoss ? 0.42f : 0.30f);
            }

            _leftArmPivot!.Rotation = leftArm;
            _rightArmPivot!.Rotation = rightArm;
            _leftLegPivot!.Rotation = new Vector3(legSwing, 0, 0);
            _rightLegPivot!.Rotation = new Vector3(-legSwing, 0, 0);
        }
        else
        {
            if (!IsInstanceValid(_frontLeftLegPivot) || !IsInstanceValid(_frontRightLegPivot) ||
                !IsInstanceValid(_backLeftLegPivot) || !IsInstanceValid(_backRightLegPivot))
            {
                return;
            }

            var legSwing = stride * (SpeciesKey == "forest-rat" ? 0.66f : 0.48f);
            _frontLeftLegPivot!.Rotation = new Vector3(legSwing, 0, 0);
            _backRightLegPivot!.Rotation = new Vector3(legSwing, 0, 0);
            _frontRightLegPivot!.Rotation = new Vector3(-legSwing, 0, 0);
            _backLeftLegPivot!.Rotation = new Vector3(-legSwing, 0, 0);
            if (IsInstanceValid(_tailPivot))
            {
                _tailPivot!.Rotation = new Vector3(0, Mathf.Sin(_motionClock * 0.75f) * 0.22f, 0);
            }
            if (IsInstanceValid(_headPivot))
            {
                var headPitch = idle * 0.035f;
                if (_attackAnimationRemaining > 0)
                {
                    var progress = 1.0f - _attackAnimationRemaining / AttackAnimationDuration;
                    headPitch = progress < 0.30f
                        ? Mathf.Lerp(-0.10f, -0.38f, progress / 0.30f)
                        : progress < 0.68f
                            ? Mathf.Lerp(-0.38f, 0.48f, (progress - 0.30f) / 0.38f)
                            : Mathf.Lerp(0.48f, 0.0f, (progress - 0.68f) / 0.32f);
                    var impact = Mathf.Clamp(1.0f - Mathf.Abs(progress - 0.62f) / 0.30f, 0, 1);
                    lunge = (_usesBlenderCreatureAsset ? 1.0f : -1.0f) * impact *
                             (SpeciesKey == "forest-rat" ? 0.25f : 0.42f);
                }
                _headPivot!.Rotation = new Vector3(headPitch, 0, 0);
            }
        }

        var bob = moving ? Mathf.Abs(stride) * (SpeciesKey == "forest-rat" ? 0.025f : 0.045f) : idle * 0.012f;
        _visualRoot.Position = new Vector3(0, bob, lunge);
    }

    private void BuildModel()
    {
        _visualRoot = new Node3D { Name = "CreatureModel" };
        AddChild(_visualRoot);

        var profile = SpeciesKey switch
        {
            "forest-rat" => new CreatureProfile(0.22f, 0.22f, new Color("52565b")),
            "prairie-wolf" => new CreatureProfile(0.48f, 0.48f, new Color("454b52")),
            "goblin-raider" => new CreatureProfile(1.0f, 0.38f, new Color("52652f")),
            "goblin-chief" => new CreatureProfile(1.35f, 0.52f, new Color("6a7131")),
            _ => new CreatureProfile(0.8f, 0.4f, new Color("555555"))
        };
        _collider = new CollisionShape3D
        {
            Name = "CreatureCollider",
            Position = new Vector3(0, profile.Height * 0.9f, 0),
            Shape = new CapsuleShape3D
            {
                Radius = profile.Radius,
                Height = profile.Height * 1.8f
            }
        };
        AddChild(_collider);

        _usesBlenderCreatureAsset = TryLoadRealisticCreatureModel();
        if (!_usesBlenderCreatureAsset && SpeciesKey is "forest-rat" or "prairie-wolf")
        {
            BuildBeast(profile);
        }
        else if (!_usesBlenderCreatureAsset)
        {
            BuildGoblin(profile);
        }
        BuildMotionRig(profile);

        var labelBaseHeight = IsBoss ? 5.0f : profile.Height * 2.5f + 0.7f;

        _statusLabel = new Label3D
        {
            Name = "CreatureStatus",
            Position = new Vector3(0, labelBaseHeight, 0),
            FontSize = IsBoss ? 38 : 30,
            Modulate = IsBoss ? new Color("efbd4e") : new Color("f2e0b8"),
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.95f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        AddChild(_statusLabel);

        _healthBarLabel = new Label3D
        {
            Name = "CreatureHealthBar",
            Position = new Vector3(0, labelBaseHeight + 0.42f, 0),
            FontSize = IsBoss ? 34 : 27,
            Modulate = new Color("e4b94f"),
            OutlineSize = 7,
            OutlineModulate = new Color(0, 0, 0, 0.98f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        AddChild(_healthBarLabel);

        _targetMarker = new Label3D
        {
            Name = "PlayerTargetMarker",
            Position = new Vector3(0, labelBaseHeight + 1.0f, 0),
            Text = "▼  TARGET  ▼",
            FontSize = IsBoss ? 34 : 28,
            Modulate = new Color("f0bd43"),
            OutlineSize = 8,
            OutlineModulate = new Color("681914"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false
        };
        AddChild(_targetMarker);
        _targetGroundRing = new MeshInstance3D
        {
            Name = "PlayerTargetGroundRing",
            Position = new Vector3(0, 0.055f, 0),
            Mesh = new TorusMesh
            {
                InnerRadius = SpeciesKey == "forest-rat" ? 0.42f : IsBoss ? 1.25f : 0.72f,
                OuterRadius = SpeciesKey == "forest-rat" ? 0.52f : IsBoss ? 1.42f : 0.86f,
                Rings = 32,
                RingSegments = 12
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("f0bd43"),
                EmissionEnabled = true,
                Emission = new Color("b86d18"),
                EmissionEnergyMultiplier = 1.65f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        AddChild(_targetGroundRing);
        _visualRoot.Scale = GetVisualBaseScale();
    }

    private Vector3 GetVisualBaseScale() => SpeciesKey switch
    {
        "forest-rat" => Vector3.One * 0.42f,
        "prairie-wolf" => Vector3.One * 0.78f,
        _ when IsBoss => Vector3.One * 1.35f,
        _ => Vector3.One
    };

    private bool TryLoadRealisticCreatureModel()
    {
        var path = SpeciesKey switch
        {
            "forest-rat" => "res://Assets/Creatures3D/forest-rat.glb",
            "prairie-wolf" => "res://Assets/Creatures3D/prairie-wolf.glb",
            "goblin-raider" or "goblin-chief" => "res://Assets/Creatures3D/goblin.glb",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            return false;
        }

        var packed = GD.Load<PackedScene>(path);
        if (packed is null)
        {
            return false;
        }
        var model = packed.Instantiate<Node3D>();
        model.Name = $"{SpeciesKey.Replace("-", string.Empty)}RealisticModel";
        _visualRoot.AddChild(model);
        _realisticCreatureModel = model;
        _usesRealisticGoblin = SpeciesKey is "goblin-raider" or "goblin-chief";
        if (_usesRealisticGoblin)
        {
            _realisticGoblinSkeleton = FindDescendant<Skeleton3D>(model);
            if (_realisticGoblinSkeleton is null)
            {
                model.QueueFree();
                _realisticCreatureModel = null;
                _usesRealisticGoblin = false;
                return false;
            }
        }
        return true;
    }

    private void BuildRealisticGoblinRig()
    {
        if (!IsInstanceValid(_realisticGoblinSkeleton))
        {
            return;
        }

        var axeAttachment = new BoneAttachment3D { Name = "GoblinAxeHandAttachment", BoneName = new StringName("hand_r") };
        _realisticGoblinSkeleton!.AddChild(axeAttachment);
        var axe = new Node3D { Name = "GoblinHeldAxe", Position = new Vector3(0, 0.08f, 0) };
        axeAttachment.AddChild(axe);
        AddAttachmentPart(axe, "GoblinAxeHandle",
            new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = IsBoss ? 1.05f : 0.88f, RadialSegments = 14 },
            new Vector3(IsBoss ? 0.27f : 0.20f, 0, 0), new Color("452918"),
            new Vector3(0, 0, -Mathf.Pi / 2), roughness: 0.54f);
        AddAttachmentPart(axe, "GoblinAxeHead",
            new BoxMesh { Size = IsBoss ? new Vector3(0.32f, 0.50f, 0.14f) : new Vector3(0.25f, 0.40f, 0.12f) },
            new Vector3(IsBoss ? 0.82f : 0.68f, 0.08f, 0), new Color(IsBoss ? "a7792b" : "747b7c"),
            metallic: 0.82f, roughness: 0.28f);
        AddAttachmentPart(axe, "GoblinAxeBladeEdge",
            new CylinderMesh { TopRadius = 0, BottomRadius = IsBoss ? 0.23f : 0.18f, Height = IsBoss ? 0.34f : 0.27f, RadialSegments = 3 },
            new Vector3(IsBoss ? 0.98f : 0.81f, 0.08f, 0), new Color("aeb5b6"),
            new Vector3(0, 0, -Mathf.Pi / 2), metallic: 0.90f, roughness: 0.22f);

        var shieldAttachment = new BoneAttachment3D { Name = "GoblinShieldHandAttachment", BoneName = new StringName("hand_l") };
        _realisticGoblinSkeleton.AddChild(shieldAttachment);
        var shield = new Node3D { Name = "GoblinHeldShield", Position = new Vector3(0.08f, 0, 0.08f) };
        shieldAttachment.AddChild(shield);
        AddAttachmentPart(shield, "GoblinShieldFace",
            new CylinderMesh { TopRadius = IsBoss ? 0.36f : 0.29f, BottomRadius = IsBoss ? 0.36f : 0.29f, Height = 0.075f, RadialSegments = 24 },
            Vector3.Zero, new Color(IsBoss ? "651d16" : "4b241d"), new Vector3(Mathf.Pi / 2, 0, 0), metallic: 0.18f, roughness: 0.58f);
        AddAttachmentPart(shield, "GoblinShieldBoss",
            new SphereMesh { Radius = 0.09f, Height = 0.14f, RadialSegments = 18, Rings = 10 },
            new Vector3(0, 0, -0.06f), new Color(IsBoss ? "a7792b" : "737a7b"),
            scale: new Vector3(1, 1, 0.55f), metallic: 0.72f, roughness: 0.30f);
    }

    private void UpdateRealisticGoblinMotion(float seconds)
    {
        if (!IsInstanceValid(_realisticCreatureModel) || !IsInstanceValid(_realisticGoblinSkeleton))
        {
            return;
        }

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var moving = horizontalSpeed > 0.16f;
        var targetBlend = moving ? Mathf.Clamp(horizontalSpeed / Mathf.Max(0.1f, MovementSpeed), 0, 1.25f) : 0;
        _realisticLocomotionBlend = Mathf.MoveToward(_realisticLocomotionBlend, targetBlend, seconds * 5.5f);
        _motionClock += seconds * (moving ? 8.4f : 1.8f);
        _attackAnimationRemaining = Mathf.Max(0, _attackAnimationRemaining - seconds);
        var stride = Mathf.Sin(_motionClock);
        var idle = Mathf.Sin(_motionClock * 0.68f);
        var strideAmount = stride * 0.42f * _realisticLocomotionBlend;

        SetGoblinBoneDirection("upperarm_l", new Vector3(0.25f, -0.92f, strideAmount + idle * 0.025f));
        SetGoblinBoneDirection("upperarm_r", new Vector3(-0.25f, -0.92f, -strideAmount - idle * 0.025f));
        SetGoblinBoneDirection("thigh_l", new Vector3(0.10f, -1.0f, -stride * 0.36f * _realisticLocomotionBlend));
        SetGoblinBoneDirection("thigh_r", new Vector3(-0.10f, -1.0f, stride * 0.36f * _realisticLocomotionBlend));
        SetGoblinBoneRotation("calf_l", new Quaternion(Vector3.Right, -Mathf.Max(0, stride) * 0.58f * Mathf.Min(_realisticLocomotionBlend, 1)));
        SetGoblinBoneRotation("calf_r", new Quaternion(Vector3.Right, -Mathf.Max(0, -stride) * 0.58f * Mathf.Min(_realisticLocomotionBlend, 1)));

        var lunge = 0.0f;
        if (_attackAnimationRemaining > 0)
        {
            var progress = 1.0f - _attackAnimationRemaining / AttackAnimationDuration;
            var ready = new Vector3(-0.24f, -0.76f, 0.52f);
            var windup = new Vector3(-0.48f, 0.40f, -0.82f);
            var strike = new Vector3(-0.12f, -0.26f, 1.0f);
            var axeArm = progress < 0.34f
                ? ready.Lerp(windup, Mathf.SmoothStep(0, 1, progress / 0.34f))
                : windup.Lerp(strike, Mathf.SmoothStep(0, 1, (progress - 0.34f) / 0.66f));
            SetGoblinBoneDirection("upperarm_r", axeArm);
            SetGoblinBoneDirection("upperarm_l", new Vector3(0.48f, -0.34f, 0.76f));
            lunge = Mathf.Clamp(1.0f - Mathf.Abs(progress - 0.72f) / 0.28f, 0, 1) * (IsBoss ? 0.30f : 0.22f);
        }

        var bob = moving ? Mathf.Abs(stride) * 0.04f * Mathf.Min(_realisticLocomotionBlend, 1) : idle * 0.007f;
        _realisticCreatureModel!.Position = new Vector3(0, bob, lunge);
    }

    private void SetGoblinBoneDirection(string boneName, Vector3 desiredDirection)
    {
        var boneIndex = _realisticGoblinSkeleton!.FindBone(boneName);
        if (boneIndex < 0) return;
        var globalRest = _realisticGoblinSkeleton.GetBoneGlobalRest(boneIndex);
        var delta = new Quaternion(globalRest.Basis.Y.Normalized(), desiredDirection.Normalized());
        _realisticGoblinSkeleton.SetBoneGlobalPose(boneIndex,
            new Transform3D(new Basis(delta) * globalRest.Basis, globalRest.Origin));
    }

    private void SetGoblinBoneRotation(string boneName, Quaternion rotation)
    {
        var boneIndex = _realisticGoblinSkeleton!.FindBone(boneName);
        if (boneIndex >= 0) _realisticGoblinSkeleton.SetBonePoseRotation(boneIndex, rotation.Normalized());
    }

    private static void AddAttachmentPart(
        Node3D parent,
        string name,
        Mesh mesh,
        Vector3 position,
        Color color,
        Vector3? rotation = null,
        Vector3? scale = null,
        float metallic = 0,
        float roughness = 0.82f)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Rotation = rotation ?? Vector3.Zero,
            Scale = scale ?? Vector3.One,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Metallic = metallic,
                Roughness = roughness
            }
        });
    }

    private static T? FindDescendant<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match) return match;
            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private void BuildMotionRig(CreatureProfile profile)
    {
        if (_usesRealisticGoblin)
        {
            BuildRealisticGoblinRig();
            return;
        }

        if (SpeciesKey is "goblin-raider" or "goblin-chief")
        {
            _leftArmPivot = CreateMotionPivot("GoblinLeftArmRig", new Vector3(-profile.Radius * 0.90f, profile.Height * 1.68f, 0),
                "GoblinLeftUpperArm", "GoblinLeftForearm", "GoblinLeftHand", "GoblinLeftPauldron",
                "GoblinShield", "GoblinShieldBoss", "ChiefShoulderSpikeLeft");
            _rightArmPivot = CreateMotionPivot("GoblinRightArmRig", new Vector3(profile.Radius * 0.90f, profile.Height * 1.68f, 0),
                "GoblinRightUpperArm", "GoblinRightForearm", "GoblinRightHand", "GoblinRightPauldron",
                "GoblinAxeHandle", "GoblinAxeHead", "ChiefShoulderSpikeRight");
            _leftLegPivot = CreateMotionPivot("GoblinLeftLegRig", new Vector3(-profile.Radius * 0.42f, profile.Height * 0.83f, 0),
                "GoblinLeftThigh", "GoblinLeftShin", "GoblinLeftBoot");
            _rightLegPivot = CreateMotionPivot("GoblinRightLegRig", new Vector3(profile.Radius * 0.42f, profile.Height * 0.83f, 0),
                "GoblinRightThigh", "GoblinRightShin", "GoblinRightBoot");
            return;
        }

        var rat = SpeciesKey == "forest-rat";
        var prefix = rat ? "Rat" : "Wolf";
        var frontX = rat ? 0.18f : 0.29f;
        var backX = rat ? 0.20f : 0.31f;
        var frontY = rat ? 0.31f : 0.69f;
        var backY = rat ? 0.31f : 0.68f;
        var visualForward = _usesBlenderCreatureAsset ? 1.0f : -1.0f;
        var frontZ = visualForward * (rat ? 0.33f : 0.50f);
        var backZ = -visualForward * (rat ? 0.30f : 0.50f);
        var upper = rat ? "Leg" : "Upper";
        _frontLeftLegPivot = CreateMotionPivot($"{prefix}FrontLeftLegRig", new Vector3(-frontX, frontY, frontZ),
            $"{prefix}FrontLeft{upper}", $"{prefix}FrontLeftLower", $"{prefix}FrontLeftPaw");
        _frontRightLegPivot = CreateMotionPivot($"{prefix}FrontRightLegRig", new Vector3(frontX, frontY, frontZ),
            $"{prefix}FrontRight{upper}", $"{prefix}FrontRightLower", $"{prefix}FrontRightPaw");
        _backLeftLegPivot = CreateMotionPivot($"{prefix}BackLeftLegRig", new Vector3(-backX, backY, backZ),
            $"{prefix}BackLeft{upper}", $"{prefix}BackLeftLower", $"{prefix}BackLeftPaw");
        _backRightLegPivot = CreateMotionPivot($"{prefix}BackRightLegRig", new Vector3(backX, backY, backZ),
            $"{prefix}BackRight{upper}", $"{prefix}BackRightLower", $"{prefix}BackRightPaw");

        _headPivot = CreateMotionPivotMatching($"{prefix}HeadRig",
            rat ? new Vector3(0, 0.48f, visualForward * 0.42f) : new Vector3(0, 1.05f, visualForward * 0.70f),
            name => name.StartsWith(prefix, StringComparison.Ordinal) &&
                    (name.Contains("Head", StringComparison.Ordinal) ||
                     name.Contains("Muzzle", StringComparison.Ordinal) ||
                     name.Contains("Nose", StringComparison.Ordinal) ||
                     name.Contains("Ear", StringComparison.Ordinal) ||
                     name.Contains("Eye", StringComparison.Ordinal) ||
                     name.Contains("Pupil", StringComparison.Ordinal) ||
                     name.Contains("Whisker", StringComparison.Ordinal) ||
                     name.Contains("Fang", StringComparison.Ordinal)));
        _tailPivot = CreateMotionPivotMatching($"{prefix}TailRig",
            rat ? new Vector3(0, 0.35f, -visualForward * 0.42f) : new Vector3(0, 0.88f, -visualForward * 0.78f),
            name => name.StartsWith($"{prefix}Tail", StringComparison.Ordinal));
    }

    private Node3D CreateMotionPivot(string name, Vector3 position, params string[] partNames)
    {
        var pivot = new Node3D { Name = name, Position = position };
        _visualRoot.AddChild(pivot);
        foreach (var partName in partNames)
        {
            if (_visualRoot.FindChild(partName, true, false) is Node3D part)
            {
                part.Reparent(pivot, true);
            }
        }
        return pivot;
    }

    private Node3D CreateMotionPivotMatching(string name, Vector3 position, Func<string, bool> matches)
    {
        var pivot = new Node3D { Name = name, Position = position };
        _visualRoot.AddChild(pivot);
        var matchesFound = new List<Node3D>();
        CollectMatchingParts(_visualRoot, pivot, matches, matchesFound);
        foreach (var part in matchesFound)
        {
            part.Reparent(pivot, true);
        }
        return pivot;
    }

    private static void CollectMatchingParts(Node node, Node excluded, Func<string, bool> matches, List<Node3D> result)
    {
        foreach (var child in node.GetChildren())
        {
            if (ReferenceEquals(child, excluded))
            {
                continue;
            }
            if (child is Node3D part && matches(part.Name.ToString()))
            {
                result.Add(part);
                continue;
            }
            CollectMatchingParts(child, excluded, matches, result);
        }
    }

    private void BuildBeast(CreatureProfile profile)
    {
        if (SpeciesKey == "prairie-wolf")
        {
            BuildWolf(profile);
            return;
        }

        BuildRat(profile);
    }

    private void BuildRat(CreatureProfile profile)
    {
        var fur = profile.Color;
        var furLight = fur.Lightened(0.12f);
        var furDark = fur.Darkened(0.22f);
        AddPart("RatBody", new SphereMesh { Radius = 0.34f, Height = 0.62f, RadialSegments = 22, Rings = 12 },
            new Vector3(0, 0.36f, 0.06f), Vector3.Zero, fur, new Vector3(0.9f, 0.82f, 1.48f), roughness: 0.92f);
        AddPart("RatChest", new SphereMesh { Radius = 0.25f, Height = 0.46f, RadialSegments = 20, Rings = 11 },
            new Vector3(0, 0.41f, -0.28f), Vector3.Zero, furLight, new Vector3(0.95f, 1, 1.15f), roughness: 0.92f);
        AddPart("RatHead", new SphereMesh { Radius = 0.23f, Height = 0.42f, RadialSegments = 20, Rings = 11 },
            new Vector3(0, 0.50f, -0.52f), Vector3.Zero, furLight, new Vector3(0.95f, 1, 1.12f), roughness: 0.92f);
        AddPart("RatMuzzle", new SphereMesh { Radius = 0.14f, Height = 0.24f, RadialSegments = 18, Rings = 10 },
            new Vector3(0, 0.44f, -0.72f), Vector3.Zero, new Color("8b7169"), new Vector3(0.9f, 0.72f, 1.2f), roughness: 0.76f);
        AddPart("RatNose", new SphereMesh { Radius = 0.062f, Height = 0.11f, RadialSegments = 14, Rings = 8 },
            new Vector3(0, 0.46f, -0.84f), Vector3.Zero, new Color("21191a"), new Vector3(1, 0.8f, 0.72f), roughness: 0.5f);

        foreach (var (side, x) in new[] { ("Left", -0.15f), ("Right", 0.15f) })
        {
            AddPart($"Rat{side}EarOuter", new SphereMesh { Radius = 0.12f, Height = 0.20f, RadialSegments = 18, Rings = 10 },
                new Vector3(x, 0.68f, -0.48f), Vector3.Zero, furDark, new Vector3(0.9f, 1, 0.52f), roughness: 0.9f);
            AddPart($"Rat{side}EarInner", new SphereMesh { Radius = 0.075f, Height = 0.13f, RadialSegments = 16, Rings = 9 },
                new Vector3(x, 0.68f, -0.535f), Vector3.Zero, new Color("9a6864"), new Vector3(0.9f, 1, 0.45f), roughness: 0.8f);
            AddPart($"Rat{side}Eye", new SphereMesh { Radius = 0.043f, Height = 0.08f, RadialSegments = 14, Rings = 8 },
                new Vector3(x * 0.56f, 0.565f, -0.70f), Vector3.Zero, new Color("d6aa3a"), new Vector3(1, 1, 0.55f), roughness: 0.2f);
            AddPart($"Rat{side}Pupil", new SphereMesh { Radius = 0.018f, Height = 0.035f, RadialSegments = 12, Rings = 7 },
                new Vector3(x * 0.56f, 0.565f, -0.736f), Vector3.Zero, new Color("090909"), new Vector3(1, 1, 0.5f), roughness: 0.16f);
        }

        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 3; index++)
            {
                AddPart($"RatWhisker{side}_{index}", new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.004f, Height = 0.38f, RadialSegments = 8 },
                    new Vector3(side * (0.18f + index * 0.02f), 0.43f + index * 0.035f, -0.77f),
                    new Vector3(0, 0, side * (Mathf.Pi / 2 - 0.12f + index * 0.11f)), new Color("c8b89c"), roughness: 0.7f);
            }
        }

        foreach (var (name, x, z) in new[]
        {
            ("FrontLeft", -0.18f, -0.33f), ("FrontRight", 0.18f, -0.33f),
            ("BackLeft", -0.20f, 0.30f), ("BackRight", 0.20f, 0.30f)
        })
        {
            AddPart($"Rat{name}Leg", new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.07f, Height = 0.24f, RadialSegments = 12 },
                new Vector3(x, 0.17f, z), Vector3.Zero, furDark, roughness: 0.9f);
            AddPart($"Rat{name}Paw", new SphereMesh { Radius = 0.075f, Height = 0.11f, RadialSegments = 14, Rings = 8 },
                new Vector3(x, 0.055f, z - 0.045f), Vector3.Zero, new Color("6d5650"), new Vector3(0.9f, 0.55f, 1.25f), roughness: 0.76f);
        }
        AddPart("RatTailBase", new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.075f, Height = 0.65f, RadialSegments = 12 },
            new Vector3(0, 0.33f, 0.69f), new Vector3(Mathf.DegToRad(68), 0, 0), new Color("88645c"), roughness: 0.72f);
        AddPart("RatTailTip", new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.045f, Height = 0.62f, RadialSegments = 12 },
            new Vector3(0, 0.13f, 1.22f), new Vector3(Mathf.DegToRad(82), 0, 0), new Color("765750"), roughness: 0.72f);
    }

    private void BuildWolf(CreatureProfile profile)
    {
        var fur = profile.Color;
        var furLight = fur.Lightened(0.14f);
        var furDark = fur.Darkened(0.24f);
        AddPart("WolfBody", new SphereMesh { Radius = 0.56f, Height = 0.95f, RadialSegments = 24, Rings = 14 },
            new Vector3(0, 0.75f, 0.05f), Vector3.Zero, fur, new Vector3(0.82f, 0.86f, 1.45f), roughness: 0.94f);
        AddPart("WolfChest", new SphereMesh { Radius = 0.46f, Height = 0.85f, RadialSegments = 22, Rings = 13 },
            new Vector3(0, 0.83f, -0.43f), Vector3.Zero, furLight, new Vector3(0.9f, 1.04f, 0.9f), roughness: 0.94f);
        AddPart("WolfHaunches", new SphereMesh { Radius = 0.48f, Height = 0.82f, RadialSegments = 22, Rings = 13 },
            new Vector3(0, 0.76f, 0.55f), Vector3.Zero, furDark, new Vector3(0.92f, 0.92f, 1.05f), roughness: 0.94f);
        AddPart("WolfMane", new SphereMesh { Radius = 0.48f, Height = 0.78f, RadialSegments = 22, Rings = 13 },
            new Vector3(0, 1.03f, -0.55f), Vector3.Zero, furDark, new Vector3(1, 1, 0.88f), roughness: 0.96f);
        AddPart("WolfHead", new SphereMesh { Radius = 0.38f, Height = 0.68f, RadialSegments = 22, Rings = 13 },
            new Vector3(0, 1.15f, -0.85f), Vector3.Zero, furLight, new Vector3(0.92f, 1, 1.05f), roughness: 0.94f);
        AddPart("WolfMuzzle", new SphereMesh { Radius = 0.25f, Height = 0.42f, RadialSegments = 20, Rings = 11 },
            new Vector3(0, 1.05f, -1.17f), Vector3.Zero, new Color("6d6b66"), new Vector3(0.86f, 0.72f, 1.25f), roughness: 0.82f);
        AddPart("WolfNose", new SphereMesh { Radius = 0.105f, Height = 0.18f, RadialSegments = 16, Rings = 9 },
            new Vector3(0, 1.08f, -1.39f), Vector3.Zero, new Color("161718"), new Vector3(1, 0.78f, 0.72f), roughness: 0.44f);
        foreach (var (side, x, tilt) in new[] { ("Left", -0.24f, -0.10f), ("Right", 0.24f, 0.10f) })
        {
            AddPart($"Wolf{side}Ear", new CylinderMesh { TopRadius = 0, BottomRadius = 0.16f, Height = 0.42f, RadialSegments = 12 },
                new Vector3(x, 1.53f, -0.78f), new Vector3(tilt, 0, tilt), furDark, new Vector3(0.75f, 1, 0.62f), roughness: 0.94f);
            AddPart($"Wolf{side}Eye", new SphereMesh { Radius = 0.065f, Height = 0.12f, RadialSegments = 14, Rings = 8 },
                new Vector3(x * 0.58f, 1.25f, -1.15f), Vector3.Zero, new Color("d7a72f"), new Vector3(1, 1, 0.48f), roughness: 0.2f);
            AddPart($"Wolf{side}Pupil", new SphereMesh { Radius = 0.026f, Height = 0.05f, RadialSegments = 12, Rings = 7 },
                new Vector3(x * 0.58f, 1.25f, -1.204f), Vector3.Zero, new Color("090a09"), new Vector3(0.7f, 1.1f, 0.42f), roughness: 0.16f);
        }
        foreach (var (side, x) in new[] { ("Left", -0.10f), ("Right", 0.10f) })
        {
            AddPart($"WolfFang{side}", new CylinderMesh { TopRadius = 0, BottomRadius = 0.035f, Height = 0.15f, RadialSegments = 10 },
                new Vector3(x, 0.92f, -1.30f), new Vector3(Mathf.Pi, 0, 0), new Color("e4dcc5"), roughness: 0.36f);
        }
        foreach (var (name, x, z) in new[]
        {
            ("FrontLeft", -0.29f, -0.50f), ("FrontRight", 0.29f, -0.50f),
            ("BackLeft", -0.31f, 0.50f), ("BackRight", 0.31f, 0.50f)
        })
        {
            AddPart($"Wolf{name}Upper", new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.15f, Height = 0.45f, RadialSegments = 14 },
                new Vector3(x, 0.47f, z), Vector3.Zero, furDark, roughness: 0.94f);
            AddPart($"Wolf{name}Lower", new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.11f, Height = 0.34f, RadialSegments = 14 },
                new Vector3(x, 0.20f, z - 0.04f), Vector3.Zero, furLight, roughness: 0.94f);
            AddPart($"Wolf{name}Paw", new SphereMesh { Radius = 0.14f, Height = 0.20f, RadialSegments = 16, Rings = 9 },
                new Vector3(x, 0.07f, z - 0.11f), Vector3.Zero, furDark, new Vector3(0.92f, 0.58f, 1.26f), roughness: 0.94f);
        }
        AddPart("WolfTailBase", new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.22f, Height = 0.78f, RadialSegments = 14 },
            new Vector3(0, 0.88f, 1.03f), new Vector3(Mathf.DegToRad(58), 0, 0), furDark, roughness: 0.94f);
        AddPart("WolfTailTip", new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.15f, Height = 0.64f, RadialSegments = 14 },
            new Vector3(0, 0.72f, 1.68f), new Vector3(Mathf.DegToRad(76), 0, 0), fur, roughness: 0.94f);
    }

    private void BuildGoblin(CreatureProfile profile)
    {
        var isCaptain = CreatureName.Contains("Captain", StringComparison.OrdinalIgnoreCase);
        var armor = IsBoss ? new Color("651d16") : IsRaidAttacker ? new Color("4b241d") : new Color("30251d");
        var leather = IsBoss ? new Color("46231b") : new Color("3b2a20");
        var metal = IsBoss ? new Color("a7792b") : isCaptain ? new Color("8e6b34") : new Color("6d7476");
        var skin = profile.Color;

        foreach (var (side, x) in new[] { ("Left", -profile.Radius * 0.42f), ("Right", profile.Radius * 0.42f) })
        {
            AddPart($"Goblin{side}Thigh", new CylinderMesh { TopRadius = profile.Radius * 0.22f, BottomRadius = profile.Radius * 0.25f, Height = profile.Height * 0.62f, RadialSegments = 14 },
                new Vector3(x, profile.Height * 0.52f, 0), Vector3.Zero, new Color("24201c"), roughness: 0.88f);
            AddPart($"Goblin{side}Shin", new CylinderMesh { TopRadius = profile.Radius * 0.17f, BottomRadius = profile.Radius * 0.20f, Height = profile.Height * 0.48f, RadialSegments = 14 },
                new Vector3(x, profile.Height * 0.20f, 0), Vector3.Zero, leather, roughness: 0.66f);
            AddPart($"Goblin{side}Boot", new SphereMesh { Radius = profile.Radius * 0.28f, Height = profile.Radius * 0.42f, RadialSegments = 16, Rings = 9 },
                new Vector3(x, profile.Height * 0.04f, -profile.Radius * 0.16f), Vector3.Zero, new Color("211b17"), new Vector3(0.9f, 0.58f, 1.24f), roughness: 0.64f);
        }

        AddPart("GoblinTunic", new CylinderMesh { TopRadius = profile.Radius * 0.72f, BottomRadius = profile.Radius, Height = profile.Height * 1.05f, RadialSegments = 18 },
            new Vector3(0, profile.Height * 1.20f, 0), Vector3.Zero, armor, roughness: 0.76f);
        AddPart("GoblinChestArmor", new BoxMesh { Size = new Vector3(profile.Radius * 1.38f, 0.12f, profile.Height * 0.72f) },
            new Vector3(0, profile.Height * 1.32f, -profile.Radius * 0.70f), Vector3.Zero, leather, metallic: 0.08f, roughness: 0.58f);
        AddPart("GoblinBelt", new CylinderMesh { TopRadius = profile.Radius * 0.98f, BottomRadius = profile.Radius * 0.98f, Height = profile.Height * 0.11f, RadialSegments = 18 },
            new Vector3(0, profile.Height * 0.78f, 0), Vector3.Zero, new Color("2c2019"), roughness: 0.58f);
        AddPart("GoblinBuckle", new BoxMesh { Size = new Vector3(profile.Radius * 0.38f, 0.10f, profile.Height * 0.22f) },
            new Vector3(0, profile.Height * 0.80f, -profile.Radius * 0.83f), Vector3.Zero, metal, metallic: 0.66f, roughness: 0.34f);

        foreach (var (side, sign) in new[] { ("Left", -1f), ("Right", 1f) })
        {
            var x = sign * profile.Radius * 1.04f;
            AddPart($"Goblin{side}UpperArm", new CylinderMesh { TopRadius = profile.Radius * 0.18f, BottomRadius = profile.Radius * 0.22f, Height = profile.Height * 0.52f, RadialSegments = 14 },
                new Vector3(x, profile.Height * 1.42f, 0), new Vector3(0, 0, sign * -0.10f), skin, roughness: 0.86f);
            AddPart($"Goblin{side}Forearm", new CylinderMesh { TopRadius = profile.Radius * 0.15f, BottomRadius = profile.Radius * 0.19f, Height = profile.Height * 0.46f, RadialSegments = 14 },
                new Vector3(x * 1.06f, profile.Height * 1.00f, -0.02f), Vector3.Zero, skin.Darkened(0.05f), roughness: 0.86f);
            AddPart($"Goblin{side}Hand", new SphereMesh { Radius = profile.Radius * 0.22f, Height = profile.Radius * 0.38f, RadialSegments = 16, Rings = 9 },
                new Vector3(x * 1.06f, profile.Height * 0.70f, -0.04f), Vector3.Zero, skin, new Vector3(0.84f, 1, 0.78f), roughness: 0.86f);
            AddPart($"Goblin{side}Pauldron", new SphereMesh { Radius = profile.Radius * 0.34f, Height = profile.Radius * 0.42f, RadialSegments = 18, Rings = 10 },
                new Vector3(sign * profile.Radius * 0.90f, profile.Height * 1.66f, 0), Vector3.Zero, metal.Darkened(0.12f), new Vector3(1.15f, 0.62f, 1), metallic: 0.64f, roughness: 0.36f);
        }

        AddPart("GoblinHead", new SphereMesh { Radius = profile.Radius * 0.62f, Height = profile.Height * 0.72f, RadialSegments = 24, Rings = 14 },
            new Vector3(0, profile.Height * 2.04f, 0), Vector3.Zero, skin, new Vector3(1, 0.94f, 0.94f), roughness: 0.86f);
        AddPart("GoblinJaw", new SphereMesh { Radius = profile.Radius * 0.45f, Height = profile.Height * 0.38f, RadialSegments = 20, Rings = 11 },
            new Vector3(0, profile.Height * 1.88f, -profile.Radius * 0.32f), Vector3.Zero, skin.Darkened(0.06f), new Vector3(1.05f, 0.8f, 0.85f), roughness: 0.86f);
        AddPart("GoblinNose", new SphereMesh { Radius = profile.Radius * 0.22f, Height = profile.Height * 0.25f, RadialSegments = 16, Rings = 9 },
            new Vector3(0, profile.Height * 2.02f, -profile.Radius * 0.64f), Vector3.Zero, skin.Lightened(0.04f), new Vector3(0.82f, 1, 1.05f), roughness: 0.82f);
        foreach (var (side, sign) in new[] { ("Left", -1f), ("Right", 1f) })
        {
            AddPart($"Goblin{side}Ear", new CylinderMesh { TopRadius = 0, BottomRadius = profile.Radius * 0.22f, Height = profile.Radius * 0.86f, RadialSegments = 12 },
                new Vector3(sign * profile.Radius * 0.77f, profile.Height * 2.08f, 0), new Vector3(0, 0, sign * -Mathf.Pi / 2), skin, new Vector3(1, 1, 0.62f), roughness: 0.86f);
            AddPart($"Goblin{side}Eye", new SphereMesh { Radius = profile.Radius * 0.14f, Height = profile.Radius * 0.24f, RadialSegments = 14, Rings = 8 },
                new Vector3(sign * profile.Radius * 0.24f, profile.Height * 2.10f, -profile.Radius * 0.54f), Vector3.Zero, new Color("e7b42e"), new Vector3(1, 1, 0.52f), roughness: 0.2f);
            AddPart($"Goblin{side}Pupil", new SphereMesh { Radius = profile.Radius * 0.055f, Height = profile.Radius * 0.10f, RadialSegments = 12, Rings = 7 },
                new Vector3(sign * profile.Radius * 0.24f, profile.Height * 2.10f, -profile.Radius * 0.64f), Vector3.Zero, new Color("080909"), new Vector3(0.75f, 1.1f, 0.45f), roughness: 0.15f);
            AddPart($"Goblin{side}Brow", new BoxMesh { Size = new Vector3(profile.Radius * 0.40f, 0.06f, profile.Radius * 0.08f) },
                new Vector3(sign * profile.Radius * 0.22f, profile.Height * 2.24f, -profile.Radius * 0.52f), new Vector3(0, sign * 0.12f, sign * -0.14f), new Color("2b241c"), roughness: 0.8f);
            AddPart($"Goblin{side}Tusk", new CylinderMesh { TopRadius = 0, BottomRadius = profile.Radius * 0.055f, Height = profile.Height * 0.18f, RadialSegments = 10 },
                new Vector3(sign * profile.Radius * 0.22f, profile.Height * 1.78f, -profile.Radius * 0.55f), new Vector3(Mathf.Pi, 0, sign * 0.08f), new Color("dfd3b5"), roughness: 0.4f);
        }

        AddPart("GoblinAxeHandle", new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.072f, Height = profile.Height * 1.75f, RadialSegments = 12 },
            new Vector3(profile.Radius * 1.35f, profile.Height * 1.20f, -0.05f), new Vector3(0, 0, Mathf.DegToRad(-12)), new Color("442b19"), roughness: 0.5f);
        AddPart("GoblinAxeHead", new BoxMesh { Size = new Vector3(profile.Radius * 1.05f, profile.Radius * 0.68f, 0.18f) },
            new Vector3(profile.Radius * 1.52f, profile.Height * 1.98f, -0.05f), new Vector3(0, 0, Mathf.DegToRad(-12)), metal, metallic: 0.78f, roughness: 0.30f);
        AddPart("GoblinShield", new CylinderMesh { TopRadius = profile.Radius * 0.68f, BottomRadius = profile.Radius * 0.68f, Height = 0.10f, RadialSegments = 14 },
            new Vector3(-profile.Radius * 1.18f, profile.Height * 1.12f, -0.12f), new Vector3(Mathf.Pi / 2, 0, 0), armor.Darkened(0.10f), new Vector3(0.86f, 1, 1.08f), metallic: 0.16f, roughness: 0.58f);
        AddPart("GoblinShieldBoss", new SphereMesh { Radius = profile.Radius * 0.20f, Height = profile.Radius * 0.30f, RadialSegments = 14, Rings = 8 },
            new Vector3(-profile.Radius * 1.18f, profile.Height * 1.12f, -0.20f), Vector3.Zero, metal, new Vector3(1, 1, 0.5f), metallic: 0.72f, roughness: 0.3f);

        if (IsBoss)
        {
            AddPart("ChiefCape", new BoxMesh { Size = new Vector3(profile.Radius * 1.75f, 0.10f, profile.Height * 1.85f) },
                new Vector3(0, profile.Height * 1.20f, profile.Radius * 0.60f), new Vector3(0.05f, 0, 0), new Color("4f1715"), roughness: 0.82f);
            AddPart("ChiefHelm", new CylinderMesh { TopRadius = profile.Radius * 0.44f, BottomRadius = profile.Radius * 0.68f, Height = profile.Height * 0.38f, RadialSegments = 10 },
                new Vector3(0, profile.Height * 2.48f, 0), Vector3.Zero, metal, metallic: 0.78f, roughness: 0.28f);
            foreach (var (side, sign) in new[] { ("Left", -1f), ("Right", 1f) })
            {
                AddPart($"ChiefHorn{side}", new CylinderMesh { TopRadius = 0, BottomRadius = profile.Radius * 0.15f, Height = profile.Height * 0.55f, RadialSegments = 12 },
                    new Vector3(sign * profile.Radius * 0.48f, profile.Height * 2.72f, 0), new Vector3(0, 0, sign * -0.35f), new Color("d4c49b"), roughness: 0.44f);
                AddPart($"ChiefShoulderSpike{side}", new CylinderMesh { TopRadius = 0, BottomRadius = profile.Radius * 0.13f, Height = profile.Height * 0.40f, RadialSegments = 10 },
                    new Vector3(sign * profile.Radius * 1.02f, profile.Height * 1.92f, 0), new Vector3(0, 0, sign * -0.30f), metal, metallic: 0.72f, roughness: 0.30f);
            }
        }
        else if (isCaptain)
        {
            AddPart("RaiderCaptainHelm", new CylinderMesh { TopRadius = profile.Radius * 0.38f, BottomRadius = profile.Radius * 0.60f, Height = profile.Height * 0.28f, RadialSegments = 10 },
                new Vector3(0, profile.Height * 2.42f, 0), Vector3.Zero, metal, metallic: 0.68f, roughness: 0.34f);
        }
    }

    private void AddPart(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        Vector3? scale = null,
        float metallic = 0.0f,
        float roughness = 0.82f)
    {
        var part = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            Scale = scale ?? Vector3.One,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = roughness,
                Metallic = metallic
            }
        };
        _visualRoot.AddChild(part);
    }

    private sealed record CreatureProfile(float Height, float Radius, Color Color);
}
