using Godot;

namespace LivingRealms.Client;

public partial class SettlementNpc : CharacterBody3D
{
    private ThirdPersonPlayer _player = null!;
    private WorldPathfinder _pathfinder = null!;
    private Node3D _visualRoot = null!;
    private Label3D _statusLabel = null!;
    private Label3D _healthBarLabel = null!;
    private Label3D _raidMarkerLabel = null!;
    private Vector3 _targetPosition;
    private Vector3 _scheduledTargetPosition;
    private CombatCreature? _raidCombatTarget;
    private float _raidAttackCooldown;
    private IReadOnlyList<Vector3> _path = [];
    private int _waypoint;
    private float _pathRefreshSeconds;
    private float _gravity = 9.8f;
    private Node3D? _leftArmPivot;
    private Node3D? _rightArmPivot;
    private Node3D? _leftLegPivot;
    private Node3D? _rightLegPivot;
    private Node3D? _heldSword;
    private Node3D? _stowedSwordPommel;
    private Node3D? _realisticCharacterModel;
    private Skeleton3D? _realisticSkeleton;
    private bool _usesRealisticCharacter;
    private float _locomotionBlend;
    private float _motionClock;
    private float _attackAnimationRemaining;
    private readonly RandomNumberGenerator _ambientRandom = new();
    private Vector3 _ambientTargetPosition;
    private float _ambientPauseSeconds;
    private float _resourceWorkCooldown;
    private bool _showDetailedOverhead;
    private bool _hasAmbientTarget;
    private int _guardPatrolWaypoint;
    private const float AttackAnimationDuration = 0.58f;
    private static readonly Vector3[] StonehavenGuardPatrol =
    [
        new(-7.5f, 0.08f, 0.5f),
        new(7.5f, 0.08f, 0.5f),
        new(10.0f, 0.08f, -9.0f),
        new(7.0f, 0.08f, -22.0f),
        new(0.0f, 0.08f, -29.0f),
        new(-8.0f, 0.08f, -22.0f),
        new(-10.0f, 0.08f, -9.0f),
        new(0.0f, 0.08f, -5.0f)
    ];

    public Guid ResidentId { get; private set; }
    public string ResidentName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string Activity { get; private set; } = string.Empty;
    public string Dialogue { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int Health { get; private set; }
    public int MaximumHealth { get; private set; }
    public bool CanFight { get; private set; }
    public IReadOnlyCollection<string> Skills { get; private set; } = [];
    public string PrimarySkill { get; private set; } = string.Empty;
    public int SkillLevel { get; private set; }
    public string Trait { get; private set; } = string.Empty;
    public long Experience { get; private set; }
    public bool IsMajor { get; private set; }
    public string MemorySummary { get; private set; } = string.Empty;
    public bool AiEnabled { get; set; } = true;
    public bool IsAvailable => !Status.Equals("Dead", StringComparison.OrdinalIgnoreCase) &&
                               !Status.Equals("Missing", StringComparison.OrdinalIgnoreCase);
    public bool IsRaidDefender => IsAvailable && CanFight;
    public bool IsCounterattackSoldier => IsRaidDefender &&
        (Activity.StartsWith("Assembling for the Darkwood", StringComparison.OrdinalIgnoreCase) ||
         Activity.StartsWith("Marching on Darkwood", StringComparison.OrdinalIgnoreCase) ||
         Activity.StartsWith("Fighting Darkwood", StringComparison.OrdinalIgnoreCase) ||
         Activity.StartsWith("Destroying the Darkwood", StringComparison.OrdinalIgnoreCase));

    public event Action? RaidCombatPulse;
    public event Action<string>? ResourceWorkPulse;
    public event Action<Guid, Guid, Vector3, Vector3>? SettlementDefenseAttackRequested;

    public void Configure(WorldResidentData data, ThirdPersonPlayer player, WorldPathfinder pathfinder)
    {
        _player = player;
        _pathfinder = pathfinder;
        ResidentId = data.Id;
        _ambientRandom.Seed = BitConverter.ToUInt64(data.Id.ToByteArray(), 0);
        _guardPatrolWaypoint = data.Id.ToByteArray()[0] % StonehavenGuardPatrol.Length;
        _ambientPauseSeconds = _ambientRandom.RandfRange(0.4f, 2.0f);
        ApplyData(data, synchronizePosition: true);
    }

    public override void _Ready()
    {
        CollisionLayer = 8;
        CollisionMask = 1 | 2 | 4 | 8;
        FloorSnapLength = 0.3f;
        FloorMaxAngle = Mathf.DegToRad(48.0f);
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        BuildModel();
        ApplyVisibleState();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateLabelVisibility();
        if (!IsAvailable || !AiEnabled)
        {
            Velocity = Vector3.Zero;
            return;
        }

        if (GlobalPosition.Y < -1.0f ||
            Mathf.Abs(GlobalPosition.X) > 140.0f ||
            Mathf.Abs(GlobalPosition.Z) > 140.0f)
        {
            GlobalPosition = _pathfinder.GetNearestWalkablePosition(
                new Vector3(_scheduledTargetPosition.X, 0.08f, _scheduledTargetPosition.Z));
            Velocity = Vector3.Zero;
            _path = [];
            _waypoint = 0;
        }

        var seconds = (float)delta;
        _raidAttackCooldown = Mathf.Max(0, _raidAttackCooldown - seconds);
        _resourceWorkCooldown = Mathf.Max(0, _resourceWorkCooldown - seconds);
        var hasRaidTarget = IsRaidDefender && IsInstanceValid(_raidCombatTarget) && _raidCombatTarget!.IsAlive;
        if (!hasRaidTarget)
        {
            UpdateAmbientDestination(seconds);
        }
        var desiredTarget = hasRaidTarget
            ? _pathfinder.GetNearestWalkablePosition(_raidCombatTarget!.GlobalPosition)
            : _hasAmbientTarget ? _ambientTargetPosition : _scheduledTargetPosition;
        if (HorizontalDistance(_targetPosition, desiredTarget) > 0.8f)
        {
            _targetPosition = desiredTarget;
            _pathRefreshSeconds = 0;
        }

        var velocity = Velocity;
        velocity.Y = IsOnFloor() ? -0.1f : velocity.Y - _gravity * seconds;
        var direction = GetMovementDirection(seconds, hasRaidTarget ? 1.75f : 0.55f);
        var walkingSpeed = IsGuard ? 2.55f : 2.1f;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * walkingSpeed, 9.0f * seconds);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * walkingSpeed, 9.0f * seconds);
        var facingDirection = direction;
        if (hasRaidTarget)
        {
            facingDirection = _raidCombatTarget!.GlobalPosition - GlobalPosition;
            facingDirection.Y = 0;
        }
        if (facingDirection.LengthSquared() > 0.001f)
        {
            var targetRotation = _usesRealisticCharacter
                ? Mathf.Atan2(facingDirection.X, facingDirection.Z)
                : Mathf.Atan2(-facingDirection.X, -facingDirection.Z);
            _visualRoot.Rotation = new Vector3(
                0,
                Mathf.LerpAngle(_visualRoot.Rotation.Y, targetRotation, (hasRaidTarget ? 12.0f : 7.0f) * seconds),
                0);
        }

        Velocity = velocity;
        MoveAndSlide();
        UpdateMotion(seconds);

        if (!hasRaidTarget && IsResourceWorker &&
            HorizontalDistance(GlobalPosition, _scheduledTargetPosition) <= 2.1f &&
            _resourceWorkCooldown <= 0)
        {
            _resourceWorkCooldown = 7.5f;
            ResourceWorkPulse?.Invoke(ResidentName.ToLowerInvariant());
        }

        if (hasRaidTarget &&
            HorizontalDistance(GlobalPosition, _raidCombatTarget!.GlobalPosition) <= 2.25f &&
            _raidAttackCooldown <= 0)
        {
            _raidAttackCooldown = 1.35f;
            PulseCombatAttack();
            _raidCombatTarget.ReceiveDefenderAttackPulse();
            SettlementDefenseAttackRequested?.Invoke(
                ResidentId,
                _raidCombatTarget.CreatureId,
                GlobalPosition,
                _raidCombatTarget.GlobalPosition);
        }
        else if (!hasRaidTarget &&
                 Activity.StartsWith("Destroying the Darkwood camp", StringComparison.OrdinalIgnoreCase) &&
                 HorizontalDistance(GlobalPosition, _scheduledTargetPosition) <= 2.4f &&
                 _raidAttackCooldown <= 0)
        {
            _raidAttackCooldown = 1.35f;
            PulseCombatAttack();
            RaidCombatPulse?.Invoke();
        }
    }

    public void ApplyData(WorldResidentData data, bool synchronizePosition = false)
    {
        var wasAvailable = IsAvailable;
        var previousScheduledTarget = _scheduledTargetPosition;
        ResidentName = data.Name;
        Role = data.Role;
        Health = data.Health;
        MaximumHealth = data.MaximumHealth;
        Status = data.Status;
        CanFight = data.CanFight;
        Skills = data.Skills;
        PrimarySkill = data.PrimarySkill;
        SkillLevel = data.SkillLevel;
        Trait = data.Trait;
        Experience = data.Experience;
        IsMajor = data.IsMajor;
        MemorySummary = data.MemorySummary;
        Activity = data.Activity;
        Dialogue = data.Dialogue;
        _scheduledTargetPosition = _pathfinder.GetNearestWalkablePosition(data.Position);
        if (HorizontalDistance(previousScheduledTarget, _scheduledTargetPosition) > 0.9f)
        {
            _hasAmbientTarget = false;
            _ambientPauseSeconds = _ambientRandom.RandfRange(0.35f, 1.4f);
        }
        if (!IsInstanceValid(_raidCombatTarget) || !IsRaidDefender)
        {
            _targetPosition = _hasAmbientTarget ? _ambientTargetPosition : _scheduledTargetPosition;
        }
        _path = [];
        _waypoint = 0;
        _pathRefreshSeconds = 0;
        if (synchronizePosition || (!wasAvailable && IsAvailable))
        {
            if (IsInsideTree())
            {
                GlobalPosition = _targetPosition;
            }
            else
            {
                Position = _targetPosition;
            }
        }
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Text = BuildStatusText();
            UpdateHealthBar();
            ApplyVisibleState();
        }
    }

    public float DistanceToPlayer()
    {
        if (!IsInsideTree() || !IsInstanceValid(_player) || !_player.IsInsideTree())
        {
            return float.MaxValue;
        }
        var offset = _player.GlobalPosition - GlobalPosition;
        offset.Y = 0;
        return offset.Length();
    }

    public string Interact()
    {
        if (!IsAvailable)
        {
            return $"{ResidentName} is {Status.ToLowerInvariant()}.";
        }

        if (IsInstanceValid(_visualRoot))
        {
            var tween = CreateTween();
            tween.TweenProperty(_visualRoot, "scale", Vector3.One * 1.07f, 0.09);
            tween.TweenProperty(_visualRoot, "scale", Vector3.One, 0.12);
        }
        var importance = IsMajor ? "Major resident" : "Resident";
        return $"{ResidentName}, {Role}: \"{Dialogue}\"  •  {Activity}  •  " +
               $"Status: {Status}  •  {importance}  •  {PrimarySkill} level {SkillLevel}  •  " +
               $"Trait: {Trait}  •  Memory: {MemorySummary}";
    }

    public void SetOverheadDetail(bool showDetailed)
    {
        _showDetailedOverhead = showDetailed;
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Text = BuildStatusText();
            _statusLabel.FontSize = showDetailed ? 27 : 30;
        }
        UpdateLabelVisibility();
    }

    public void SetRaidCombatTarget(CombatCreature? target)
    {
        if (ReferenceEquals(_raidCombatTarget, target))
        {
            return;
        }

        _raidCombatTarget = target;
        _hasAmbientTarget = false;
        _ambientPauseSeconds = target is null ? 0.5f : 1.0f;
        if (target is null)
        {
            _targetPosition = _scheduledTargetPosition;
        }
        _path = [];
        _waypoint = 0;
        _pathRefreshSeconds = 0;
        ApplyVisibleState();
        UpdateCombatWeaponVisibility();
    }

    public void ReceiveRaidAttackPulse()
    {
        if (!IsAvailable || !IsInstanceValid(_visualRoot))
        {
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(_visualRoot, "scale", Vector3.One * 1.14f, 0.08);
        tween.TweenProperty(_visualRoot, "scale", Vector3.One, 0.12);
        RaidCombatPulse?.Invoke();
    }

    private void PulseCombatAttack()
    {
        _attackAnimationRemaining = AttackAnimationDuration;
        UpdateCombatWeaponVisibility();
    }

    private void UpdateMotion(float seconds)
    {
        if (_usesRealisticCharacter)
        {
            UpdateRealisticMotion(seconds);
            return;
        }

        if (!IsInstanceValid(_visualRoot) ||
            !IsInstanceValid(_leftArmPivot) ||
            !IsInstanceValid(_rightArmPivot) ||
            !IsInstanceValid(_leftLegPivot) ||
            !IsInstanceValid(_rightLegPivot))
        {
            return;
        }

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var moving = horizontalSpeed > 0.16f;
        _motionClock += seconds * (moving ? 8.4f : 1.7f);
        _attackAnimationRemaining = Mathf.Max(0, _attackAnimationRemaining - seconds);

        var stride = moving ? Mathf.Sin(_motionClock) : 0;
        var idleBreath = Mathf.Sin(_motionClock * 0.72f);
        var legSwing = stride * 0.54f;
        var armSwing = stride * 0.34f;
        var leftArm = new Vector3(-armSwing + idleBreath * 0.018f, 0, -0.03f);
        var rightArm = new Vector3(armSwing - idleBreath * 0.018f, 0, 0.03f);
        var leftLeg = new Vector3(legSwing, 0, 0);
        var rightLeg = new Vector3(-legSwing, 0, 0);
        var lunge = 0.0f;

        if (!moving && _attackAnimationRemaining <= 0)
        {
            switch (Role)
            {
                case "Blacksmith":
                    rightArm = new Vector3(-0.34f + idleBreath * 0.28f, 0, 0.16f);
                    leftArm = new Vector3(0.08f - idleBreath * 0.06f, 0, -0.08f);
                    break;
                case "Innkeeper":
                    rightArm = new Vector3(-0.18f + idleBreath * 0.08f, 0, 0.10f);
                    break;
                case "Healer":
                    rightArm = new Vector3(0.06f, idleBreath * 0.035f, 0.08f);
                    leftArm = new Vector3(-0.08f + idleBreath * 0.05f, 0, -0.04f);
                    break;
                case "Storekeeper":
                    leftArm = new Vector3(-0.28f + idleBreath * 0.10f, 0, -0.12f);
                    break;
                case "Guard Captain":
                case "Stonehaven Guard":
                    leftArm = new Vector3(idleBreath * 0.035f, 0, -0.11f);
                    rightArm = new Vector3(-idleBreath * 0.035f, 0, 0.11f);
                    break;
                default:
                    rightArm = new Vector3(idleBreath * 0.07f, 0, 0.06f);
                    leftArm = new Vector3(-idleBreath * 0.07f, 0, -0.06f);
                    break;
            }
        }

        if (_attackAnimationRemaining > 0)
        {
            var progress = 1.0f - _attackAnimationRemaining / AttackAnimationDuration;
            var swordArc = progress < 0.28f
                ? Mathf.Lerp(0.06f, 0.92f, progress / 0.28f)
                : progress < 0.68f
                    ? Mathf.Lerp(0.92f, -1.30f, (progress - 0.28f) / 0.40f)
                    : Mathf.Lerp(-1.30f, 0.0f, (progress - 0.68f) / 0.32f);
            rightArm = new Vector3(swordArc, -0.16f, 0.48f);
            leftArm = new Vector3(-0.20f, 0.18f, -0.26f);
            var impact = Mathf.Clamp(1.0f - Mathf.Abs(progress - 0.62f) / 0.30f, 0, 1);
            lunge = -impact * 0.28f;
        }

        _leftArmPivot!.Rotation = leftArm;
        _rightArmPivot!.Rotation = rightArm;
        _leftLegPivot!.Rotation = leftLeg;
        _rightLegPivot!.Rotation = rightLeg;
        var bob = moving ? Mathf.Abs(stride) * 0.035f : idleBreath * 0.012f;
        _visualRoot.Position = new Vector3(0, bob, lunge);
        UpdateCombatWeaponVisibility();
    }

    private void UpdateAmbientDestination(float seconds)
    {
        if (IsGuard)
        {
            UpdateGuardPatrolDestination(seconds);
            return;
        }

        var patrolRadius = CanFight ? 4.2f : Role == "Villager" ? 3.0f : 2.4f;
        if (_hasAmbientTarget)
        {
            if (HorizontalDistance(GlobalPosition, _ambientTargetPosition) <= 0.68f ||
                HorizontalDistance(_ambientTargetPosition, _scheduledTargetPosition) > patrolRadius + 1.0f)
            {
                _hasAmbientTarget = false;
                _ambientPauseSeconds = _ambientRandom.RandfRange(CanFight ? 0.7f : 1.2f, CanFight ? 2.2f : 3.8f);
            }
            return;
        }

        _ambientPauseSeconds = Mathf.Max(0, _ambientPauseSeconds - seconds);
        if (_ambientPauseSeconds > 0)
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var angle = _ambientRandom.RandfRange(0, Mathf.Tau);
            var distance = _ambientRandom.RandfRange(patrolRadius * 0.42f, patrolRadius);
            var candidate = _scheduledTargetPosition + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
            candidate = _pathfinder.GetNearestWalkablePosition(candidate);
            if (HorizontalDistance(candidate, _scheduledTargetPosition) <= patrolRadius + 0.75f &&
                HorizontalDistance(candidate, GlobalPosition) > 0.9f)
            {
                _ambientTargetPosition = candidate;
                _hasAmbientTarget = true;
                return;
            }
        }

        _ambientPauseSeconds = 1.0f;
    }

    private bool IsGuard => Role.Contains("Guard", StringComparison.OrdinalIgnoreCase);
    private bool IsResourceWorker =>
        Role.Equals("Lumberjack", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("Quarry Worker", StringComparison.OrdinalIgnoreCase);

    private void UpdateGuardPatrolDestination(float seconds)
    {
        if (_hasAmbientTarget)
        {
            if (HorizontalDistance(GlobalPosition, _ambientTargetPosition) <= 0.75f)
            {
                _hasAmbientTarget = false;
                _ambientPauseSeconds = _ambientRandom.RandfRange(0.35f, 1.0f);
            }
            return;
        }

        _ambientPauseSeconds = Mathf.Max(0, _ambientPauseSeconds - seconds);
        if (_ambientPauseSeconds > 0)
        {
            return;
        }

        for (var attempt = 0; attempt < StonehavenGuardPatrol.Length; attempt++)
        {
            _guardPatrolWaypoint = (_guardPatrolWaypoint + 1) % StonehavenGuardPatrol.Length;
            var laneOffset = (ResidentId.ToByteArray()[1] % 3 - 1) * 0.7f;
            var waypoint = StonehavenGuardPatrol[_guardPatrolWaypoint];
            var candidate = _pathfinder.GetNearestWalkablePosition(
                waypoint + new Vector3(laneOffset, 0, -laneOffset * 0.35f));
            if (HorizontalDistance(candidate, GlobalPosition) > 1.2f)
            {
                _ambientTargetPosition = candidate;
                _hasAmbientTarget = true;
                return;
            }
        }

        _ambientPauseSeconds = 0.5f;
    }

    private Vector3 GetMovementDirection(float seconds, float stoppingDistance)
    {
        var targetOffset = _targetPosition - GlobalPosition;
        targetOffset.Y = 0;
        if (targetOffset.Length() <= stoppingDistance)
        {
            _path = [];
            _waypoint = 0;
            return Vector3.Zero;
        }

        _pathRefreshSeconds = Mathf.Max(0, _pathRefreshSeconds - seconds);
        if (_path.Count == 0 || _pathRefreshSeconds <= 0)
        {
            _path = _pathfinder.FindPath(GlobalPosition, _targetPosition);
            _waypoint = Math.Min(1, Math.Max(0, _path.Count - 1));
            _pathRefreshSeconds = 1.5f;
        }

        while (_waypoint < _path.Count - 1 && HorizontalDistance(GlobalPosition, _path[_waypoint]) < 0.6f)
        {
            _waypoint++;
        }
        if (_path.Count == 0 || _waypoint >= _path.Count)
        {
            return targetOffset.Normalized();
        }

        var waypointOffset = _path[_waypoint] - GlobalPosition;
        waypointOffset.Y = 0;
        return waypointOffset.LengthSquared() > 0.001f ? waypointOffset.Normalized() : Vector3.Zero;
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private void ApplyVisibleState()
    {
        var dead = Status.Equals("Dead", StringComparison.OrdinalIgnoreCase);
        var visible = !Status.Equals("Missing", StringComparison.OrdinalIgnoreCase);
        Visible = visible;
        if (IsInstanceValid(_visualRoot))
        {
            _visualRoot.Visible = visible;
            _visualRoot.Rotation = new Vector3(0, _visualRoot.Rotation.Y, dead ? Mathf.Pi / 2.0f : 0);
        }
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Visible = visible;
        }
        if (IsInstanceValid(_healthBarLabel))
        {
            _healthBarLabel.Visible = visible;
        }
        if (IsInstanceValid(_raidMarkerLabel))
        {
            _raidMarkerLabel.Visible = visible &&
                                       _showDetailedOverhead &&
                                       IsRaidDefender &&
                                       IsInstanceValid(_raidCombatTarget);
        }
        CollisionLayer = IsAvailable ? 8u : 0u;
        CollisionMask = IsAvailable ? 15u : 0u;
        SetPhysicsProcess(IsAvailable);
        UpdateCombatWeaponVisibility();
        UpdateLabelVisibility();
    }

    private void UpdateLabelVisibility()
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }
        var present = !Status.Equals("Missing", StringComparison.OrdinalIgnoreCase);
        var nearby = DistanceToPlayer() <= 20.0f;
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Visible = present && nearby;
        }
        if (IsInstanceValid(_healthBarLabel))
        {
            _healthBarLabel.Visible = present &&
                                      _showDetailedOverhead &&
                                      (nearby || Health < MaximumHealth || IsInstanceValid(_raidCombatTarget));
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

    private string BuildStatusText()
    {
        if (!_showDetailedOverhead)
        {
            return $"{ResidentName}  •  {Role}";
        }

        return $"{ResidentName}  •  {Role}\n{Activity.ToUpperInvariant()}  •  {Status.ToUpperInvariant()}  •  HP {Health}/{MaximumHealth}\n" +
               $"SKILLS  {string.Join(" • ", Skills.Take(3))}";
    }

    private void BuildModel()
    {
        _visualRoot = new Node3D { Name = "ResidentModel" };
        AddChild(_visualRoot);

        var profile = ResolveVisualProfile();
        _usesRealisticCharacter = TryLoadRealisticCharacter(profile);
        if (!_usesRealisticCharacter)
        {
            BuildResidentBody(profile);
            BuildResidentFace(profile);
            BuildRoleEquipment(profile);
        }
        BuildMotionRig(profile);

        var collider = new CollisionShape3D
        {
            Name = "ResidentCollider",
            Position = new Vector3(0, 1.0f, 0),
            Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.8f }
        };
        AddChild(collider);

        _statusLabel = new Label3D
        {
            Name = "ResidentStatus",
            Position = new Vector3(0, 2.65f, 0),
            Text = BuildStatusText(),
            FontSize = 27,
            Modulate = new Color("efc963"),
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.95f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        AddChild(_statusLabel);

        _healthBarLabel = new Label3D
        {
            Name = "ResidentHealthBar",
            Position = new Vector3(0, 3.15f, 0),
            FontSize = 27,
            Modulate = new Color("e4b94f"),
            OutlineSize = 7,
            OutlineModulate = new Color(0, 0, 0, 0.98f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        AddChild(_healthBarLabel);

        _raidMarkerLabel = new Label3D
        {
            Name = "RaidDefenderMarker",
            Position = new Vector3(0, 3.62f, 0),
            Text = "STONEHAVEN DEFENDER",
            FontSize = 28,
            Modulate = new Color("f0c75e"),
            OutlineSize = 9,
            OutlineModulate = new Color("203a58"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false
        };
        AddChild(_raidMarkerLabel);
        UpdateHealthBar();
    }

    private bool TryLoadRealisticCharacter(ResidentVisualProfile profile)
    {
        var modelPath = ResidentName.Equals("Elowen", StringComparison.OrdinalIgnoreCase)
            ? "res://Assets/Characters3D/elowen-herbalist.glb"
            : profile.Feminine
                ? "res://Assets/Characters3D/elara.glb"
                : "res://Assets/Characters3D/alden.glb";
        if (!ResourceLoader.Exists(modelPath))
        {
            return false;
        }

        var packed = GD.Load<PackedScene>(modelPath);
        if (packed is null)
        {
            return false;
        }

        var model = packed.Instantiate<Node3D>();
        model.Name = $"{ResidentName.Replace(" ", string.Empty)}RealisticModel";
        model.Scale = Vector3.One * profile.BodyScale;
        _visualRoot.AddChild(model);
        var skeleton = FindDescendant<Skeleton3D>(model);
        if (skeleton is null)
        {
            model.QueueFree();
            return false;
        }

        _realisticCharacterModel = model;
        _realisticSkeleton = skeleton;
        if (!ResidentName.Equals("Elowen", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureRealisticCharacterAppearance(model, profile);
        }
        return true;
    }

    private void ConfigureRealisticCharacterAppearance(Node root, ResidentVisualProfile profile)
    {
        foreach (var child in root.GetChildren())
        {
            var name = child.Name.ToString();
            var equipment = name.Contains("Sword", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Scabbard", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Bow", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Quiver", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Arrow", StringComparison.OrdinalIgnoreCase);
            var heavyArmor = name.Contains("Pauldron", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Breastplate", StringComparison.OrdinalIgnoreCase);
            if (child is Node3D visual && (equipment || (!CanFight && heavyArmor)))
            {
                visual.Visible = false;
            }

            if (child is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
            {
                for (var surface = 0; surface < meshInstance.Mesh.GetSurfaceCount(); surface++)
                {
                    if (meshInstance.GetActiveMaterial(surface) is not StandardMaterial3D source)
                    {
                        continue;
                    }
                    var material = source.Duplicate(true) as StandardMaterial3D;
                    if (material is null)
                    {
                        continue;
                    }
                    var materialName = source.ResourceName.ToString();
                    var lower = $"{name} {materialName}".ToLowerInvariant();
                    var color = ResolveRealisticMaterialColor(lower, profile);
                    if (color is not null)
                    {
                        material.AlbedoColor = new Color(color.Value.R, color.Value.G, color.Value.B, source.AlbedoColor.A);
                    }
                    meshInstance.SetSurfaceOverrideMaterial(surface, material);
                }
            }
            ConfigureRealisticCharacterAppearance(child, profile);
        }
    }

    private Color? ResolveRealisticMaterialColor(string name, ResidentVisualProfile profile)
    {
        if (name.Contains("skin")) return profile.Skin;
        if (name.Contains("hair") || name.Contains("beard") || name.Contains("moustache")) return profile.Hair;
        if (name.Contains("eye") || name.Contains("iris") || name.Contains("pupil")) return null;
        if (name.Contains("gold") || name.Contains("buckle") || name.Contains("emblem")) return profile.Accent;
        if (name.Contains("steel") || name.Contains("metal"))
        {
            return CanFight ? new Color("778187") : new Color("6d665d");
        }
        if (name.Contains("leather") || name.Contains("boot") || name.Contains("glove") || name.Contains("belt"))
        {
            return profile.Secondary.Darkened(0.28f);
        }
        if (name.Contains("trouser") || name.Contains("shadow")) return profile.Primary.Darkened(0.30f);
        if (name.Contains("cloth") || name.Contains("wool") || name.Contains("blue") ||
            name.Contains("green") || name.Contains("cloak") || name.Contains("tunic") ||
            name.Contains("tabard") || name.Contains("bodice") || name.Contains("mantle"))
        {
            return profile.Primary;
        }
        return null;
    }

    private void BuildRealisticMotionRig(ResidentVisualProfile profile)
    {
        if (!IsInstanceValid(_realisticSkeleton))
        {
            return;
        }
        if (CanFight)
        {
            BuildRealisticGuardSword();
            BuildRealisticGuardShield(profile);
        }
        else
        {
            BuildRealisticRoleProp(profile);
        }
        UpdateCombatWeaponVisibility();
    }

    private void BuildRealisticGuardSword()
    {
        var attachment = new BoneAttachment3D
        {
            Name = "GuardSwordHandAttachment",
            BoneName = new StringName(ResolveRealisticBoneName("hand_r"))
        };
        _realisticSkeleton!.AddChild(attachment);
        _heldSword = new Node3D { Name = "GuardHeldSword", Position = new Vector3(0, 0.09f, 0), Visible = false };
        attachment.AddChild(_heldSword);
        AddRigPart(_heldSword, "HeldSwordBlade", new BoxMesh { Size = new Vector3(0.82f, 0.068f, 0.024f) },
            new Vector3(0.50f, 0, 0), new Color("aeb7bb"), metallic: 0.92f, roughness: 0.22f);
        AddRigPart(_heldSword, "HeldSwordPoint", new CylinderMesh { TopRadius = 0, BottomRadius = 0.052f, Height = 0.15f, RadialSegments = 4 },
            new Vector3(0.98f, 0, 0), new Color("c8d0d2"), rotation: new Vector3(0, 0, -Mathf.Pi / 2), metallic: 0.94f, roughness: 0.20f);
        AddRigPart(_heldSword, "HeldSwordGuard", new BoxMesh { Size = new Vector3(0.045f, 0.075f, 0.34f) },
            new Vector3(0.05f, 0, 0), new Color("c7a242"), metallic: 0.78f, roughness: 0.28f);
        AddRigPart(_heldSword, "HeldSwordGrip", new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.24f, RadialSegments = 10 },
            new Vector3(-0.13f, 0, 0), new Color("2b1710"), rotation: new Vector3(0, 0, -Mathf.Pi / 2), roughness: 0.66f);
        AddRigPart(_heldSword, "HeldSwordPommel", new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 12, Rings = 7 },
            new Vector3(-0.28f, 0, 0), new Color("c7a242"), metallic: 0.76f, roughness: 0.30f);
    }

    private void BuildRealisticGuardShield(ResidentVisualProfile profile)
    {
        var attachment = new BoneAttachment3D
        {
            Name = "GuardShieldHandAttachment",
            BoneName = new StringName(ResolveRealisticBoneName("hand_l"))
        };
        _realisticSkeleton!.AddChild(attachment);
        var shield = new Node3D { Name = "GuardShield", Position = new Vector3(0.08f, 0, 0.08f) };
        attachment.AddChild(shield);
        AddRigPart(shield, "GuardShieldFace", new CylinderMesh { TopRadius = 0.31f, BottomRadius = 0.31f, Height = 0.07f, RadialSegments = 28 },
            Vector3.Zero, profile.Primary.Darkened(0.12f), rotation: new Vector3(Mathf.Pi / 2, 0, 0), metallic: 0.18f, roughness: 0.52f);
        AddRigPart(shield, "GuardShieldBoss", new SphereMesh { Radius = 0.09f, Height = 0.14f, RadialSegments = 18, Rings = 10 },
            new Vector3(0, 0, -0.06f), profile.Accent, scale: new Vector3(1, 1, 0.55f), metallic: 0.72f, roughness: 0.28f);
    }

    private void BuildRealisticRoleProp(ResidentVisualProfile profile)
    {
        var attachment = new BoneAttachment3D
        {
            Name = "ResidentRolePropAttachment",
            BoneName = new StringName(ResolveRealisticBoneName("hand_r"))
        };
        _realisticSkeleton!.AddChild(attachment);
        var prop = new Node3D { Name = $"{Role.Replace(" ", string.Empty)}Prop", Position = new Vector3(0, 0.07f, 0) };
        attachment.AddChild(prop);
        switch (Role)
        {
            case "Blacksmith":
                AddRigPart(prop, "SmithHammerHandle", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.72f, RadialSegments = 12 },
                    new Vector3(0.20f, 0, 0), new Color("5b371f"), rotation: new Vector3(0, 0, -Mathf.Pi / 2), roughness: 0.54f);
                AddRigPart(prop, "SmithHammerHead", new BoxMesh { Size = new Vector3(0.26f, 0.16f, 0.16f) },
                    new Vector3(0.57f, 0, 0), new Color("666b6d"), metallic: 0.84f, roughness: 0.28f);
                break;
            case "Healer":
                AddRigPart(prop, "HealerStaff", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.04f, Height = 1.45f, RadialSegments = 14 },
                    new Vector3(0.54f, 0, 0), new Color("654225"), rotation: new Vector3(0, 0, -Mathf.Pi / 2), roughness: 0.58f);
                AddRigPart(prop, "HealerHerb", new SphereMesh { Radius = 0.10f, Height = 0.16f, RadialSegments = 18, Rings = 10 },
                    new Vector3(1.27f, 0, 0), new Color("6f8d4f"), roughness: 0.90f);
                break;
            case "Innkeeper":
                AddRigPart(prop, "Tankard", new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.11f, Height = 0.24f, RadialSegments = 18 },
                    new Vector3(0.10f, 0, 0), new Color("a08252"), metallic: 0.18f, roughness: 0.48f);
                break;
            case "Storekeeper":
                AddRigPart(prop, "Ledger", new BoxMesh { Size = new Vector3(0.28f, 0.05f, 0.34f) },
                    new Vector3(0.10f, 0, 0), profile.Secondary, roughness: 0.66f);
                break;
        }
    }

    private void UpdateRealisticMotion(float seconds)
    {
        if (!IsInstanceValid(_realisticCharacterModel) || !IsInstanceValid(_realisticSkeleton))
        {
            return;
        }

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var moving = horizontalSpeed > 0.16f;
        var targetBlend = moving ? Mathf.Clamp(horizontalSpeed / 2.1f, 0, 1.2f) : 0;
        _locomotionBlend = Mathf.MoveToward(_locomotionBlend, targetBlend, seconds * 5.5f);
        _motionClock += seconds * (moving ? 8.4f : 1.7f);
        _attackAnimationRemaining = Mathf.Max(0, _attackAnimationRemaining - seconds);
        var stride = Mathf.Sin(_motionClock);
        var idle = Mathf.Sin(_motionClock * 0.72f);
        var strideAmount = stride * 0.40f * _locomotionBlend;

        SetRealisticBoneDirection("upperarm_l", new Vector3(0.18f, -1.0f, strideAmount + idle * 0.018f));
        SetRealisticBoneDirection("upperarm_r", new Vector3(-0.18f, -1.0f, -strideAmount - idle * 0.018f));
        SetRealisticBoneDirection("thigh_l", new Vector3(0.07f, -1.0f, -stride * 0.34f * _locomotionBlend));
        SetRealisticBoneDirection("thigh_r", new Vector3(-0.07f, -1.0f, stride * 0.34f * _locomotionBlend));
        SetRealisticBoneRotation("calf_l", new Quaternion(Vector3.Right, -Mathf.Max(0, stride) * 0.55f * Mathf.Min(_locomotionBlend, 1)));
        SetRealisticBoneRotation("calf_r", new Quaternion(Vector3.Right, -Mathf.Max(0, -stride) * 0.55f * Mathf.Min(_locomotionBlend, 1)));

        var lunge = 0.0f;
        if (_attackAnimationRemaining > 0)
        {
            var progress = 1.0f - _attackAnimationRemaining / AttackAnimationDuration;
            var ready = new Vector3(-0.18f, -0.78f, 0.48f);
            var windup = new Vector3(-0.40f, 0.32f, -0.84f);
            var strike = new Vector3(-0.10f, -0.28f, 1.0f);
            var swordArm = progress < 0.34f
                ? ready.Lerp(windup, Mathf.SmoothStep(0, 1, progress / 0.34f))
                : windup.Lerp(strike, Mathf.SmoothStep(0, 1, (progress - 0.34f) / 0.66f));
            SetRealisticBoneDirection("upperarm_r", swordArm);
            SetRealisticBoneDirection("upperarm_l", new Vector3(0.42f, -0.38f, 0.78f));
            lunge = Mathf.Clamp(1.0f - Mathf.Abs(progress - 0.72f) / 0.28f, 0, 1) * 0.22f;
        }
        else if (!moving)
        {
            if (Role == "Blacksmith") SetRealisticBoneDirection("upperarm_r", new Vector3(-0.20f, -0.68f, 0.54f + idle * 0.28f));
            else if (Role == "Healer") SetRealisticBoneDirection("upperarm_r", new Vector3(-0.16f, -0.88f, 0.35f));
            else if (Role == "Storekeeper") SetRealisticBoneDirection("upperarm_r", new Vector3(-0.28f, -0.70f, 0.52f));
        }

        var bob = moving ? Mathf.Abs(stride) * 0.035f * Mathf.Min(_locomotionBlend, 1) : idle * 0.006f;
        _realisticCharacterModel!.Position = new Vector3(0, bob, lunge);
        UpdateCombatWeaponVisibility();
    }

    private void SetRealisticBoneDirection(string boneName, Vector3 desiredDirection)
    {
        var boneIndex = _realisticSkeleton!.FindBone(ResolveRealisticBoneName(boneName));
        if (boneIndex < 0) return;
        var globalRest = _realisticSkeleton.GetBoneGlobalRest(boneIndex);
        var delta = new Quaternion(globalRest.Basis.Y.Normalized(), desiredDirection.Normalized());
        _realisticSkeleton.SetBoneGlobalPose(boneIndex,
            new Transform3D(new Basis(delta) * globalRest.Basis, globalRest.Origin));
    }

    private void SetRealisticBoneRotation(string boneName, Quaternion rotation)
    {
        var boneIndex = _realisticSkeleton!.FindBone(ResolveRealisticBoneName(boneName));
        if (boneIndex >= 0) _realisticSkeleton.SetBonePoseRotation(boneIndex, rotation.Normalized());
    }

    private string ResolveRealisticBoneName(string canonicalName)
    {
        if (!IsInstanceValid(_realisticSkeleton) || _realisticSkeleton!.FindBone(canonicalName) >= 0)
        {
            return canonicalName;
        }

        var rigifyName = canonicalName switch
        {
            "hand_l" => "DEF-hand.L",
            "hand_r" => "DEF-hand.R",
            "upperarm_l" => "DEF-upper_arm.L",
            "upperarm_r" => "DEF-upper_arm.R",
            "thigh_l" => "DEF-thigh.L",
            "thigh_r" => "DEF-thigh.R",
            "calf_l" => "DEF-shin.L",
            "calf_r" => "DEF-shin.R",
            _ => canonicalName
        };
        return _realisticSkeleton.FindBone(rigifyName) >= 0 ? rigifyName : canonicalName;
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

    private void BuildMotionRig(ResidentVisualProfile profile)
    {
        if (_usesRealisticCharacter)
        {
            BuildRealisticMotionRig(profile);
            return;
        }

        var leftShoulder = new Vector3(-0.42f * profile.BodyScale, 1.57f, 0);
        var rightShoulder = new Vector3(0.42f * profile.BodyScale, 1.57f, 0);
        _leftArmPivot = CreateMotionPivot("LeftArmRig", leftShoulder,
            "LeftUpperArm", "LeftForearm", "LeftHand", "LeftPauldron", "LeftBracer",
            "CaptainShield", "CaptainShieldBoss", "SmithGloveL", "Ledger", "LedgerClasp");
        _rightArmPivot = CreateMotionPivot("RightArmRig", rightShoulder,
            "RightUpperArm", "RightForearm", "RightHand", "RightPauldron", "RightBracer",
            "SmithHammerHandle", "SmithHammerHead", "SmithGloveR", "Tankard", "TankardRim",
            "HealerStaff", "StaffHerb", "GatheringBasket", "BasketCloth");
        _leftLegPivot = CreateMotionPivot("LeftLegRig", new Vector3(-0.18f * profile.BodyScale, 0.80f, 0),
            "LeftThigh", "LeftBoot", "LeftBootFoot", "LeftBootCuff");
        _rightLegPivot = CreateMotionPivot("RightLegRig", new Vector3(0.18f * profile.BodyScale, 0.80f, 0),
            "RightThigh", "RightBoot", "RightBootFoot", "RightBootCuff");

        _stowedSwordPommel = _visualRoot.FindChild("SwordPommel", true, false) as Node3D;
        if (CanFight && IsInstanceValid(_rightArmPivot))
        {
            BuildGuardHeldSword();
        }
        UpdateCombatWeaponVisibility();
    }

    private Node3D CreateMotionPivot(string name, Vector3 position, params string[] partNames)
    {
        var pivot = new Node3D { Name = name, Position = position };
        _visualRoot.AddChild(pivot);
        foreach (var partName in partNames)
        {
            if (_visualRoot.FindChild(partName, true, false) is Node3D part &&
                ReferenceEquals(part.GetParent(), _visualRoot))
            {
                part.Reparent(pivot, true);
            }
        }
        return pivot;
    }

    private void BuildGuardHeldSword()
    {
        _heldSword = new Node3D
        {
            Name = "GuardHeldSword",
            Position = new Vector3(0.02f, -0.89f, -0.06f),
            Rotation = new Vector3(-0.18f, 0, 0.18f),
            Visible = false
        };
        _rightArmPivot!.AddChild(_heldSword);
        AddRigPart(_heldSword, "HeldSwordBlade", new BoxMesh { Size = new Vector3(0.075f, 0.83f, 0.035f) },
            new Vector3(0, 0.52f, 0), new Color("aeb7bb"), metallic: 0.92f, roughness: 0.22f);
        AddRigPart(_heldSword, "HeldSwordPoint", new CylinderMesh { TopRadius = 0, BottomRadius = 0.055f, Height = 0.16f, RadialSegments = 4 },
            new Vector3(0, 1.01f, 0), new Color("c8d0d2"), metallic: 0.94f, roughness: 0.20f);
        AddRigPart(_heldSword, "HeldSwordGuard", new BoxMesh { Size = new Vector3(0.34f, 0.045f, 0.075f) },
            new Vector3(0, 0.08f, 0), new Color("c7a242"), metallic: 0.78f, roughness: 0.28f);
        AddRigPart(_heldSword, "HeldSwordGrip", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 0.24f, RadialSegments = 10 },
            new Vector3(0, -0.07f, 0), new Color("2b1710"), roughness: 0.66f);
        AddRigPart(_heldSword, "HeldSwordPommel", new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 12, Rings = 7 },
            new Vector3(0, -0.23f, 0), new Color("c7a242"), metallic: 0.76f, roughness: 0.30f);
    }

    private static void AddRigPart(
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

    private void UpdateCombatWeaponVisibility()
    {
        var drawn = CanFight && IsAvailable &&
                    ((IsInstanceValid(_raidCombatTarget) && _raidCombatTarget!.IsAlive) ||
                     _attackAnimationRemaining > 0);
        if (IsInstanceValid(_heldSword))
        {
            _heldSword!.Visible = drawn;
        }
        if (IsInstanceValid(_stowedSwordPommel))
        {
            _stowedSwordPommel!.Visible = !drawn;
        }
    }

    private ResidentVisualProfile ResolveVisualProfile() => ResidentName switch
    {
        "Reeve Aldric Vale" => new(false, 1.04f, new Color("ad795b"), new Color("302117"), new Color("23364d"), new Color("651f1c"), new Color("d4a447")),
        "Mira" => new(true, 0.98f, new Color("a97458"), new Color("3b251e"), new Color("30465e"), new Color("56302b"), new Color("b5bec4")),
        "Tomas" => new(false, 1.0f, new Color("9b684d"), new Color("241b17"), new Color("34485c"), new Color("3b2921"), new Color("aeb8bf")),
        "Brann" => new(false, 1.08f, new Color("8e5b43"), new Color("2a1a14"), new Color("49342a"), new Color("6f4327"), new Color("b27745")),
        "Mara Venn" => new(true, 0.98f, new Color("ae7658"), new Color("4a271b"), new Color("6c2420"), new Color("d2bd96"), new Color("d8a94b")),
        "Elowen" => new(true, 0.96f, new Color("a86f55"), new Color("68462b"), new Color("355c43"), new Color("d7cfad"), new Color("c4a85c")),
        "Oren" => new(false, 1.0f, new Color("9a674b"), new Color("37251c"), new Color("744025"), new Color("403129"), new Color("d2a85d")),
        "Nessa" => new(true, 0.94f, new Color("a97559"), new Color("2d211c"), new Color("55415f"), new Color("80704e"), new Color("c1a578")),
        _ => new(false, 1.0f, new Color("a97458"), new Color("33231b"), new Color("55415f"), new Color("47362d"), new Color("c1a578"))
    };

    private void BuildResidentBody(ResidentVisualProfile profile)
    {
        var width = profile.BodyScale;
        var clothDark = profile.Primary.Darkened(0.18f);
        var leather = new Color("35251c");
        var boot = new Color("28211d");

        AddPart("Undertunic", new CylinderMesh
        {
            TopRadius = 0.31f * width,
            BottomRadius = 0.38f * width,
            Height = 0.96f,
            RadialSegments = 18
        }, new Vector3(0, 1.19f, 0), profile.Primary);
        AddPart("ChestLayer", new SphereMesh
        {
            Radius = 0.34f * width,
            Height = 0.62f,
            RadialSegments = 20,
            Rings = 12
        }, new Vector3(0, 1.35f, -0.035f), profile.Primary.Lightened(0.04f), scale: new Vector3(1, 0.70f, 1));
        AddPart("WaistLayer", new CylinderMesh
        {
            TopRadius = 0.35f * width,
            BottomRadius = 0.40f * width,
            Height = 0.25f,
            RadialSegments = 18
        }, new Vector3(0, 0.82f, 0), clothDark);
        AddPart("Belt", new CylinderMesh
        {
            TopRadius = 0.405f * width,
            BottomRadius = 0.405f * width,
            Height = 0.115f,
            RadialSegments = 20
        }, new Vector3(0, 0.91f, 0), leather, metallic: 0.05f, roughness: 0.58f);
        AddPart("BeltBuckle", new BoxMesh { Size = new Vector3(0.13f, 0.08f, 0.11f) },
            new Vector3(0, 0.91f, -0.405f * width), profile.Accent, metallic: 0.58f, roughness: 0.34f);

        foreach (var (side, x) in new[] { ("Left", -0.42f * width), ("Right", 0.42f * width) })
        {
            AddPart($"{side}UpperArm", new CylinderMesh
            {
                TopRadius = 0.105f,
                BottomRadius = 0.12f,
                Height = 0.48f,
                RadialSegments = 14
            }, new Vector3(x, 1.34f, 0), profile.Primary.Darkened(0.06f),
                rotation: new Vector3(0, 0, x < 0 ? -0.08f : 0.08f));
            AddPart($"{side}Forearm", new CylinderMesh
            {
                TopRadius = 0.085f,
                BottomRadius = 0.105f,
                Height = 0.42f,
                RadialSegments = 14
            }, new Vector3(x * 1.06f, 0.91f, -0.015f), profile.Secondary);
            AddPart($"{side}Hand", new SphereMesh
            {
                Radius = 0.105f,
                Height = 0.22f,
                RadialSegments = 16,
                Rings = 9
            }, new Vector3(x * 1.06f, 0.64f, -0.02f), profile.Skin, scale: new Vector3(0.78f, 1.05f, 0.72f));
        }

        foreach (var (side, x) in new[] { ("Left", -0.18f * width), ("Right", 0.18f * width) })
        {
            AddPart($"{side}Thigh", new CylinderMesh
            {
                TopRadius = 0.13f,
                BottomRadius = 0.115f,
                Height = 0.45f,
                RadialSegments = 14
            }, new Vector3(x, 0.58f, 0), new Color("242524"));
            AddPart($"{side}Boot", new CylinderMesh
            {
                TopRadius = 0.105f,
                BottomRadius = 0.13f,
                Height = 0.42f,
                RadialSegments = 14
            }, new Vector3(x, 0.245f, 0.015f), boot, roughness: 0.62f);
            AddPart($"{side}BootFoot", new SphereMesh
            {
                Radius = 0.15f,
                Height = 0.25f,
                RadialSegments = 16,
                Rings = 9
            }, new Vector3(x, 0.08f, -0.105f), boot, scale: new Vector3(0.82f, 0.58f, 1.2f), roughness: 0.62f);
            AddPart($"{side}BootCuff", new CylinderMesh
            {
                TopRadius = 0.135f,
                BottomRadius = 0.125f,
                Height = 0.08f,
                RadialSegments = 14
            }, new Vector3(x, 0.44f, 0), profile.Secondary.Lightened(0.08f));
        }

        AddPart("FrontTabard", new BoxMesh { Size = new Vector3(0.48f * width, 0.055f, 0.58f) },
            new Vector3(0, 0.62f, -0.32f * width), profile.Primary.Lightened(0.06f),
            rotation: new Vector3(-0.04f, 0, 0));
        AddPart("TabardHem", new BoxMesh { Size = new Vector3(0.50f * width, 0.065f, 0.055f) },
            new Vector3(0, 0.34f, -0.315f * width), profile.Accent, metallic: 0.12f, roughness: 0.6f);
    }

    private void BuildResidentFace(ResidentVisualProfile profile)
    {
        AddPart("Neck", new CylinderMesh
        {
            TopRadius = 0.105f,
            BottomRadius = 0.115f,
            Height = 0.22f,
            RadialSegments = 16
        }, new Vector3(0, 1.68f, 0), profile.Skin);
        AddPart("Head", new SphereMesh
        {
            Radius = 0.255f,
            Height = 0.52f,
            RadialSegments = 24,
            Rings = 14
        }, new Vector3(0, 1.92f, 0), profile.Skin, scale: new Vector3(0.88f, 1, 0.88f));
        AddPart("Jaw", new SphereMesh
        {
            Radius = 0.19f,
            Height = 0.34f,
            RadialSegments = 20,
            Rings = 12
        }, new Vector3(0, 1.81f, -0.08f), profile.Skin, scale: new Vector3(0.9f, 0.82f, 0.7f));
        AddPart("Nose", new SphereMesh
        {
            Radius = 0.055f,
            Height = 0.12f,
            RadialSegments = 14,
            Rings = 8
        }, new Vector3(0, 1.91f, -0.245f), profile.Skin, scale: new Vector3(0.75f, 1.05f, 1.05f));
        foreach (var (side, x) in new[] { ("Left", -0.078f), ("Right", 0.078f) })
        {
            AddPart($"{side}EyeWhite", new SphereMesh { Radius = 0.043f, Height = 0.082f, RadialSegments = 14, Rings = 8 },
                new Vector3(x, 1.965f, -0.228f), new Color("ded7cb"), scale: new Vector3(1.1f, 0.9f, 0.45f), roughness: 0.24f);
            AddPart($"{side}Iris", new SphereMesh { Radius = 0.019f, Height = 0.038f, RadialSegments = 12, Rings = 7 },
                new Vector3(x, 1.965f, -0.265f), new Color("556c61"), scale: new Vector3(1, 1, 0.38f), roughness: 0.2f);
            AddPart($"{side}Brow", new BoxMesh { Size = new Vector3(0.09f, 0.018f, 0.018f) },
                new Vector3(x, 2.035f, -0.235f), profile.Hair,
                rotation: new Vector3(0, side == "Left" ? -0.12f : 0.12f, side == "Left" ? 0.08f : -0.08f));
        }
        AddPart("Mouth", new BoxMesh { Size = new Vector3(0.10f, 0.018f, 0.016f) },
            new Vector3(0, 1.80f, -0.235f), new Color("75443d"), roughness: 0.48f);

        AddPart("HairCap", new SphereMesh
        {
            Radius = 0.27f,
            Height = 0.46f,
            RadialSegments = 24,
            Rings = 14
        }, new Vector3(0, 2.07f, 0.025f), profile.Hair, scale: new Vector3(0.94f, 0.72f, 0.96f), roughness: 0.5f);
        if (profile.Feminine)
        {
            AddPart("HairBun", new SphereMesh { Radius = 0.13f, Height = 0.25f, RadialSegments = 18, Rings = 10 },
                new Vector3(0, 2.25f, 0.09f), profile.Hair, roughness: 0.5f);
            AddPart("LeftHairLock", new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.035f, Height = 0.34f, RadialSegments = 10 },
                new Vector3(-0.19f, 1.9f, -0.055f), profile.Hair, rotation: new Vector3(0, 0, -0.10f));
            AddPart("RightHairLock", new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.035f, Height = 0.34f, RadialSegments = 10 },
                new Vector3(0.19f, 1.9f, -0.055f), profile.Hair, rotation: new Vector3(0, 0, 0.10f));
        }
        else
        {
            AddPart("LeftSideHair", new SphereMesh { Radius = 0.10f, Height = 0.28f, RadialSegments = 16, Rings = 9 },
                new Vector3(-0.18f, 1.98f, 0.015f), profile.Hair, scale: new Vector3(0.65f, 1, 0.8f), roughness: 0.5f);
            AddPart("RightSideHair", new SphereMesh { Radius = 0.10f, Height = 0.28f, RadialSegments = 16, Rings = 9 },
                new Vector3(0.18f, 1.98f, 0.015f), profile.Hair, scale: new Vector3(0.65f, 1, 0.8f), roughness: 0.5f);
        }
    }

    private void BuildRoleEquipment(ResidentVisualProfile profile)
    {
        switch (Role)
        {
            case "Guard Captain":
            case "Stonehaven Guard":
                BuildGuardEquipment(profile, Role == "Guard Captain");
                break;
            case "Blacksmith":
                BuildBlacksmithEquipment(profile);
                break;
            case "Innkeeper":
                BuildInnkeeperEquipment(profile);
                break;
            case "Healer":
                BuildHealerEquipment(profile);
                break;
            case "Storekeeper":
                BuildStorekeeperEquipment(profile);
                break;
            default:
                BuildVillagerEquipment(profile);
                break;
        }
    }

    private void BuildGuardEquipment(ResidentVisualProfile profile, bool captain)
    {
        var steel = captain ? new Color("8c9396") : new Color("687276");
        foreach (var (side, x) in new[] { ("Left", -0.40f), ("Right", 0.40f) })
        {
            AddPart($"{side}Pauldron", new SphereMesh { Radius = 0.17f, Height = 0.20f, RadialSegments = 18, Rings = 10 },
                new Vector3(x, 1.55f, 0), steel, scale: new Vector3(1.18f, 0.62f, 1), metallic: 0.72f, roughness: 0.34f);
            AddPart($"{side}Bracer", new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.12f, Height = 0.30f, RadialSegments = 14 },
                new Vector3(x * 1.05f, 0.96f, -0.01f), steel.Darkened(0.12f), metallic: 0.7f, roughness: 0.36f);
        }
        AddPart("GuardChestPlate", new BoxMesh { Size = new Vector3(0.52f, 0.085f, 0.56f) },
            new Vector3(0, 1.32f, -0.31f), profile.Primary.Darkened(0.12f), roughness: 0.62f);
        AddPart("GuardTreeMark", new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.035f, RadialSegments = 8 },
            new Vector3(0, 1.38f, -0.37f), profile.Accent,
            rotation: new Vector3(Mathf.Pi / 2, 0, 0), scale: new Vector3(0.8f, 1.3f, 1), metallic: 0.55f, roughness: 0.35f);
        AddPart("SwordScabbard", new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.82f) },
            new Vector3(0.37f, 0.72f, 0.08f), new Color("2b2019"), rotation: new Vector3(0.12f, 0, -0.18f), roughness: 0.6f);
        AddPart("SwordPommel", new SphereMesh { Radius = 0.065f, Height = 0.12f, RadialSegments = 12, Rings = 7 },
            new Vector3(0.29f, 1.12f, 0.02f), profile.Accent, metallic: 0.65f, roughness: 0.3f);
        if (captain)
        {
            AddPart("CaptainCape", new BoxMesh { Size = new Vector3(0.62f, 0.055f, 1.05f) },
                new Vector3(0, 1.05f, 0.29f), profile.Secondary, rotation: new Vector3(0.05f, 0, 0));
            AddPart("CaptainShield", new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.10f, RadialSegments = 16 },
                new Vector3(-0.50f, 1.05f, -0.12f), profile.Primary,
                rotation: new Vector3(Mathf.Pi / 2, 0, 0), scale: new Vector3(0.82f, 1, 1.12f), metallic: 0.2f, roughness: 0.52f);
            AddPart("CaptainShieldBoss", new SphereMesh { Radius = 0.10f, Height = 0.15f, RadialSegments = 14, Rings = 8 },
                new Vector3(-0.50f, 1.05f, -0.20f), profile.Accent, scale: new Vector3(1, 1, 0.55f), metallic: 0.7f, roughness: 0.3f);
        }
    }

    private void BuildBlacksmithEquipment(ResidentVisualProfile profile)
    {
        AddPart("SmithApron", new BoxMesh { Size = new Vector3(0.55f, 0.06f, 0.96f) },
            new Vector3(0, 1.02f, -0.34f), new Color("4a2f21"), roughness: 0.6f);
        AddPart("ApronChest", new BoxMesh { Size = new Vector3(0.44f, 0.07f, 0.34f) },
            new Vector3(0, 1.42f, -0.35f), new Color("5d3b27"), roughness: 0.6f);
        AddPart("SmithHammerHandle", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.82f, RadialSegments = 12 },
            new Vector3(0.48f, 0.92f, -0.05f), new Color("5b371f"), rotation: new Vector3(0, 0, -0.16f), roughness: 0.5f);
        AddPart("SmithHammerHead", new BoxMesh { Size = new Vector3(0.34f, 0.17f, 0.16f) },
            new Vector3(0.56f, 1.31f, -0.05f), new Color("55595a"), rotation: new Vector3(0, 0, -0.16f), metallic: 0.82f, roughness: 0.3f);
        AddPart("SmithGloveL", new SphereMesh { Radius = 0.12f, Height = 0.22f, RadialSegments = 14, Rings = 8 },
            new Vector3(-0.45f, 0.65f, -0.02f), new Color("3b2a20"), scale: new Vector3(0.8f, 1, 0.75f));
        AddPart("SmithGloveR", new SphereMesh { Radius = 0.12f, Height = 0.22f, RadialSegments = 14, Rings = 8 },
            new Vector3(0.45f, 0.65f, -0.02f), new Color("3b2a20"), scale: new Vector3(0.8f, 1, 0.75f));
    }

    private void BuildInnkeeperEquipment(ResidentVisualProfile profile)
    {
        AddPart("InnApron", new BoxMesh { Size = new Vector3(0.55f, 0.05f, 0.86f) },
            new Vector3(0, 0.92f, -0.34f), profile.Secondary, roughness: 0.88f);
        AddPart("InnApronTie", new BoxMesh { Size = new Vector3(0.72f, 0.06f, 0.07f) },
            new Vector3(0, 1.14f, -0.34f), profile.Accent, roughness: 0.72f);
        AddPart("Tankard", new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.11f, Height = 0.25f, RadialSegments = 14 },
            new Vector3(0.47f, 0.70f, -0.15f), new Color("8b7250"), metallic: 0.18f, roughness: 0.48f);
        AddPart("TankardRim", new CylinderMesh { TopRadius = 0.115f, BottomRadius = 0.115f, Height = 0.035f, RadialSegments = 14 },
            new Vector3(0.47f, 0.835f, -0.15f), new Color("c8a960"), metallic: 0.35f, roughness: 0.38f);
    }

    private void BuildHealerEquipment(ResidentVisualProfile profile)
    {
        AddPart("HealerMantle", new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.39f, Height = 0.38f, RadialSegments = 18 },
            new Vector3(0, 1.48f, 0), profile.Secondary, roughness: 0.88f);
        AddPart("HealerSash", new BoxMesh { Size = new Vector3(0.09f, 0.055f, 1.0f) },
            new Vector3(0, 1.18f, -0.36f), profile.Secondary, rotation: new Vector3(0, 0, -0.48f), roughness: 0.85f);
        AddPart("HealerSatchel", new BoxMesh { Size = new Vector3(0.28f, 0.16f, 0.30f) },
            new Vector3(-0.37f, 0.81f, 0.02f), new Color("64442d"), rotation: new Vector3(0, 0, 0.08f), roughness: 0.6f);
        AddPart("HealerStaff", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.04f, Height = 1.65f, RadialSegments = 12 },
            new Vector3(0.52f, 0.90f, 0), new Color("654225"), rotation: new Vector3(0, 0, -0.04f), roughness: 0.55f);
        AddPart("StaffHerb", new SphereMesh { Radius = 0.11f, Height = 0.18f, RadialSegments = 14, Rings = 8 },
            new Vector3(0.56f, 1.73f, 0), new Color("6f8d4f"), scale: new Vector3(1.1f, 0.8f, 0.75f), roughness: 0.9f);
    }

    private void BuildStorekeeperEquipment(ResidentVisualProfile profile)
    {
        AddPart("MerchantVest", new BoxMesh { Size = new Vector3(0.54f, 0.06f, 0.62f) },
            new Vector3(0, 1.28f, -0.34f), profile.Secondary, roughness: 0.66f);
        AddPart("MerchantPack", new BoxMesh { Size = new Vector3(0.55f, 0.30f, 0.72f) },
            new Vector3(0, 1.17f, 0.30f), new Color("4b3425"), roughness: 0.62f);
        AddPart("Ledger", new BoxMesh { Size = new Vector3(0.26f, 0.055f, 0.34f) },
            new Vector3(-0.46f, 0.76f, -0.12f), new Color("76502e"), rotation: new Vector3(0.15f, 0, -0.15f), roughness: 0.64f);
        AddPart("LedgerClasp", new BoxMesh { Size = new Vector3(0.055f, 0.065f, 0.34f) },
            new Vector3(-0.46f, 0.76f, -0.16f), profile.Accent, rotation: new Vector3(0.15f, 0, -0.15f), metallic: 0.45f, roughness: 0.36f);
    }

    private void BuildVillagerEquipment(ResidentVisualProfile profile)
    {
        AddPart("VillagerShawl", new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.37f, Height = 0.30f, RadialSegments = 18 },
            new Vector3(0, 1.50f, 0), profile.Secondary, roughness: 0.9f);
        AddPart("GatheringBasket", new CylinderMesh { TopRadius = 0.20f, BottomRadius = 0.16f, Height = 0.32f, RadialSegments = 12 },
            new Vector3(0.47f, 0.69f, -0.02f), new Color("89633b"), roughness: 0.82f);
        AddPart("BasketCloth", new SphereMesh { Radius = 0.16f, Height = 0.18f, RadialSegments = 14, Rings = 8 },
            new Vector3(0.47f, 0.87f, -0.02f), profile.Accent, scale: new Vector3(1, 0.45f, 1), roughness: 0.92f);
    }

    private void AddPart(
        string name,
        Mesh mesh,
        Vector3 position,
        Color color,
        Vector3? rotation = null,
        Vector3? scale = null,
        float metallic = 0,
        float roughness = 0.82f)
    {
        var part = new MeshInstance3D
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
                Roughness = roughness,
                Metallic = metallic
            }
        };
        _visualRoot.AddChild(part);
    }

    private sealed record ResidentVisualProfile(
        bool Feminine,
        float BodyScale,
        Color Skin,
        Color Hair,
        Color Primary,
        Color Secondary,
        Color Accent);
}
