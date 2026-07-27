using Godot;

namespace LivingRealms.Client;

public partial class RoamingDragon : Node3D
{
    private const float GroundHeight = 0.12f;
    private const float WalkSpeed = 3.2f;
    private const float RunSpeed = 7.0f;
    private const float FlySpeed = 13.0f;
    private const float ClimbSpeed = 5.0f;
    private static readonly string[] TravelModes = ["Walk", "Run", "Fly"];

    private readonly List<Vector3> _plannedPath = [];
    private readonly RandomNumberGenerator _random = new();
    private WorldPathfinder _pathfinder = null!;
    private IReadOnlyList<Vector3> _roamingAnchors = [];
    private AnimationPlayer? _animationPlayer;
    private Label3D _statusLabel = null!;
    private Vector3 _destination;
    private float _stateSeconds;
    private float _flightAltitude;
    private int _pathWaypoint;
    private int _travelModeIndex;
    private int _anchorIndex;
    private int _anchorStep = 1;
    private bool _landing;
    private bool _configured;
    private Func<Vector3>? _playerPositionProvider;
    private Func<IEnumerable<Vector3>>? _dragonPositionProvider;
    private string _displayName = "Dragon";
    private string _currentMode = "Idle";
    private float _noProgressSeconds;
    private float _bestDestinationDistance = float.MaxValue;
    private int _stallRecoveries;

    public string CurrentMode => _currentMode;

    public string DisplayName => _displayName;

    public bool RoamsWholeMap => _roamingAnchors.Count >= 9;

    public void Configure(
        WorldPathfinder pathfinder,
        Node3D model,
        string displayName,
        Color labelColor,
        IReadOnlyList<Vector3> roamingAnchors,
        int seed,
        int initialTravelMode,
        int initialAnchor,
        bool reverseRoute,
        Func<Vector3>? playerPositionProvider,
        Func<IEnumerable<Vector3>>? dragonPositionProvider)
    {
        _pathfinder = pathfinder;
        _displayName = displayName;
        _roamingAnchors = roamingAnchors;
        _travelModeIndex = Math.Clamp(initialTravelMode, 0, TravelModes.Length - 1);
        _anchorIndex = Math.Clamp(initialAnchor, 0, Math.Max(0, roamingAnchors.Count - 1));
        _anchorStep = reverseRoute ? -1 : 1;
        _playerPositionProvider = playerPositionProvider;
        _dragonPositionProvider = dragonPositionProvider;
        _random.Seed = (ulong)Math.Max(1, seed);

        model.Name = "DragonModel";
        model.Scale = Vector3.One * 0.11f;
        AddChild(model);
        _animationPlayer = FindAnimationPlayer(model);
        if (_animationPlayer is not null)
        {
            foreach (var mode in new[] { "Idle", "Walk", "Run", "Fly" })
            {
                if (_animationPlayer.HasAnimation(mode))
                {
                    _animationPlayer.GetAnimation(mode).LoopMode = Animation.LoopModeEnum.Linear;
                }
            }
        }

        _statusLabel = new Label3D
        {
            Name = "DragonStatusLabel",
            Text = $"{_displayName.ToUpperInvariant()}\nIDLE  -  ROAMING ALL NINE GRIDS",
            Position = new Vector3(0, 5.6f, 0),
            FontSize = 30,
            Modulate = labelColor,
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.92f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        AddChild(_statusLabel);

        _stateSeconds = 4.0f + initialTravelMode;
        SetMode("Idle");
        _configured = true;
    }

    public override void _Process(double delta)
    {
        if (!_configured || !IsInstanceValid(_animationPlayer))
        {
            return;
        }

        var seconds = Math.Min((float)delta, 0.1f);
        if (_currentMode.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            _stateSeconds -= seconds;
            if (_stateSeconds <= 0)
            {
                BeginNextTravel();
            }
            return;
        }

        if (_currentMode.Equals("Fly", StringComparison.OrdinalIgnoreCase))
        {
            UpdateFlight(seconds);
            UpdateProgressWatchdog(seconds);
            return;
        }

        UpdateGroundTravel(seconds);
        UpdateProgressWatchdog(seconds);
    }

    private void BeginNextTravel()
    {
        var nextMode = TravelModes[_travelModeIndex];
        _travelModeIndex = (_travelModeIndex + 1) % TravelModes.Length;
        if (nextMode.Equals("Fly", StringComparison.OrdinalIgnoreCase))
        {
            BeginFlight();
            return;
        }

        BeginGroundTravel(nextMode);
    }

    private void BeginGroundTravel(string mode)
    {
        var radius = mode.Equals("Run", StringComparison.OrdinalIgnoreCase)
            ? _random.RandfRange(28.0f, 44.0f)
            : _random.RandfRange(13.0f, 25.0f);
        var foundPath = false;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var angle = _random.RandfRange(0, Mathf.Tau);
            var candidate = GlobalPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius);
            candidate.X = Mathf.Clamp(candidate.X, -136.0f, 136.0f);
            candidate.Z = Mathf.Clamp(candidate.Z, -136.0f, 136.0f);
            candidate.Y = GroundHeight;
            _destination = _pathfinder.GetNearestWalkablePosition(candidate);
            _plannedPath.Clear();
            _plannedPath.AddRange(_pathfinder.FindPath(GlobalPosition, _destination));
            if (_plannedPath.Count > 1)
            {
                foundPath = true;
                break;
            }
        }

        if (!foundPath)
        {
            BeginFlight();
            return;
        }

        _pathWaypoint = 0;
        while (_pathWaypoint < _plannedPath.Count &&
               HorizontalDistance(GlobalPosition, _plannedPath[_pathWaypoint]) <= 1.5f)
        {
            _pathWaypoint++;
        }
        ResetProgressWatchdog();
        SetMode(mode);
    }

    private void BeginFlight()
    {
        _destination = SelectNextMapAnchor();
        _flightAltitude = _random.RandfRange(10.0f, 16.0f);
        _landing = false;
        _plannedPath.Clear();
        _pathWaypoint = 0;
        ResetProgressWatchdog();
        SetMode("Fly");
    }

    private Vector3 SelectNextMapAnchor()
    {
        if (_roamingAnchors.Count == 0)
        {
            return _pathfinder.GetNearestWalkablePosition(
                new Vector3(-GlobalPosition.X, GroundHeight, -GlobalPosition.Z));
        }

        for (var attempt = 0; attempt < _roamingAnchors.Count; attempt++)
        {
            _anchorIndex = PositiveModulo(_anchorIndex + _anchorStep, _roamingAnchors.Count);
            var anchor = _roamingAnchors[_anchorIndex];
            var jitter = new Vector3(
                _random.RandfRange(-5.0f, 5.0f),
                0,
                _random.RandfRange(-5.0f, 5.0f));
            var candidate = _pathfinder.GetNearestWalkablePosition(anchor + jitter);
            candidate.Y = GroundHeight;
            if (HorizontalDistance(GlobalPosition, candidate) >= 48.0f)
            {
                return candidate;
            }
        }

        var fallback = _pathfinder.GetNearestWalkablePosition(
            _roamingAnchors[_anchorIndex]);
        fallback.Y = GroundHeight;
        return fallback;
    }

    private void UpdateGroundTravel(float seconds)
    {
        while (_pathWaypoint < _plannedPath.Count &&
               HorizontalDistance(GlobalPosition, _plannedPath[_pathWaypoint]) <= 1.1f)
        {
            _pathWaypoint++;
        }

        if (_pathWaypoint >= _plannedPath.Count ||
            HorizontalDistance(GlobalPosition, _destination) <= 1.3f)
        {
            BeginIdle();
            return;
        }

        var waypoint = _plannedPath[_pathWaypoint];
        var direction = waypoint - GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < 0.001f)
        {
            _pathWaypoint++;
            return;
        }

        direction = ApplyLocalAvoidance(direction.Normalized(), avoidPlayer: true);
        FaceTravelDirection(direction, seconds);
        var speed = _currentMode.Equals("Run", StringComparison.OrdinalIgnoreCase)
            ? RunSpeed
            : WalkSpeed;
        var next = GlobalPosition + direction * speed * seconds;
        next.Y = GroundHeight;
        GlobalPosition = ClampToWorld(next);
    }

    private void UpdateFlight(float seconds)
    {
        var horizontalOffset = _destination - GlobalPosition;
        horizontalOffset.Y = 0;
        if (!_landing && horizontalOffset.Length() <= 3.0f)
        {
            _landing = true;
        }

        var targetHeight = _landing ? GroundHeight : _flightAltitude;
        var next = GlobalPosition;
        next.Y = Mathf.MoveToward(next.Y, targetHeight, ClimbSpeed * seconds);
        if (!_landing && horizontalOffset.LengthSquared() > 0.01f)
        {
            var direction = ApplyLocalAvoidance(
                horizontalOffset.Normalized(),
                avoidPlayer: GlobalPosition.Y < 8.0f);
            FaceTravelDirection(direction, seconds);
            var horizontalStep = Math.Min(FlySpeed * seconds, horizontalOffset.Length());
            next.X += direction.X * horizontalStep;
            next.Z += direction.Z * horizontalStep;
        }
        GlobalPosition = ClampToWorld(next);

        if (_landing && Mathf.Abs(GlobalPosition.Y - GroundHeight) <= 0.03f)
        {
            GlobalPosition = new Vector3(_destination.X, GroundHeight, _destination.Z);
            BeginIdle();
        }
    }

    private void BeginIdle()
    {
        _plannedPath.Clear();
        _pathWaypoint = 0;
        _landing = false;
        _stateSeconds = _random.RandfRange(2.5f, 5.5f);
        ResetProgressWatchdog();
        SetMode("Idle");
    }

    private void SetMode(string mode)
    {
        _currentMode = mode;
        if (_animationPlayer?.HasAnimation(mode) == true)
        {
            _animationPlayer.Play(mode, 0.35);
        }
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Text =
                $"{_displayName.ToUpperInvariant()}\n{mode.ToUpperInvariant()}  -  ROAMING ALL NINE GRIDS";
        }
    }

    private void FaceTravelDirection(Vector3 direction, float seconds)
    {
        var targetRotation = Mathf.Atan2(direction.X, direction.Z);
        Rotation = new Vector3(
            0,
            Mathf.LerpAngle(Rotation.Y, targetRotation, Math.Min(1.0f, seconds * 4.5f)),
            0);
    }

    private Vector3 ApplyLocalAvoidance(Vector3 desiredDirection, bool avoidPlayer)
    {
        var correction = Vector3.Zero;
        if (avoidPlayer && _playerPositionProvider is not null)
        {
            correction += GetSeparation(
                _playerPositionProvider(),
                clearance: 7.5f,
                strength: 1.15f);
        }

        if (_dragonPositionProvider is not null)
        {
            foreach (var peerPosition in _dragonPositionProvider())
            {
                if (peerPosition.DistanceSquaredTo(GlobalPosition) <= 0.01f)
                {
                    continue;
                }
                correction += GetSeparation(
                    peerPosition,
                    clearance: 11.0f,
                    strength: 1.8f);
            }
        }

        var adjusted = desiredDirection + correction;
        adjusted.Y = 0;
        return adjusted.LengthSquared() > 0.001f
            ? adjusted.Normalized()
            : desiredDirection;
    }

    private Vector3 GetSeparation(Vector3 otherPosition, float clearance, float strength)
    {
        var away = GlobalPosition - otherPosition;
        away.Y = 0;
        var distance = away.Length();
        if (distance >= clearance)
        {
            return Vector3.Zero;
        }
        if (distance < 0.01f)
        {
            away = Vector3.Right;
            distance = 0.01f;
        }

        return away.Normalized() * ((clearance - distance) / clearance) * strength;
    }

    private void UpdateProgressWatchdog(float seconds)
    {
        if (_currentMode.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            ResetProgressWatchdog();
            return;
        }

        var distance = HorizontalDistance(GlobalPosition, _destination);
        if (distance < _bestDestinationDistance - 0.18f)
        {
            _bestDestinationDistance = distance;
            _noProgressSeconds = 0;
            _stallRecoveries = Math.Max(0, _stallRecoveries - 1);
            return;
        }

        _noProgressSeconds += seconds;
        if (_noProgressSeconds < 2.2f)
        {
            return;
        }

        _noProgressSeconds = 0;
        _bestDestinationDistance = float.MaxValue;
        _stallRecoveries++;
        if (_currentMode.Equals("Fly", StringComparison.OrdinalIgnoreCase) || _stallRecoveries >= 2)
        {
            // A stale landing approach or avoidance loop must never hold a
            // roaming dragon indefinitely. Select a fresh, walkable map anchor
            // and approach it from altitude.
            _destination = SelectNextMapAnchor();
            _flightAltitude = _random.RandfRange(12.0f, 18.0f);
            _landing = false;
            _plannedPath.Clear();
            _pathWaypoint = 0;
            SetMode("Fly");
            return;
        }

        _plannedPath.Clear();
        _plannedPath.AddRange(_pathfinder.FindPath(GlobalPosition, _destination));
        _pathWaypoint = 0;
        while (_pathWaypoint < _plannedPath.Count &&
               HorizontalDistance(GlobalPosition, _plannedPath[_pathWaypoint]) <= 1.5f)
        {
            _pathWaypoint++;
        }
        if (_pathWaypoint >= _plannedPath.Count)
        {
            BeginFlight();
        }
    }

    private void ResetProgressWatchdog()
    {
        _noProgressSeconds = 0;
        _bestDestinationDistance = _currentMode.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            ? float.MaxValue
            : HorizontalDistance(GlobalPosition, _destination);
        if (_currentMode.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            _stallRecoveries = 0;
        }
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer animationPlayer)
        {
            return animationPlayer;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static int PositiveModulo(int value, int divisor) =>
        ((value % divisor) + divisor) % divisor;

    private static float HorizontalDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();

    private static Vector3 ClampToWorld(Vector3 position) => new(
        Mathf.Clamp(position.X, -138.0f, 138.0f),
        position.Y,
        Mathf.Clamp(position.Z, -138.0f, 138.0f));
}
