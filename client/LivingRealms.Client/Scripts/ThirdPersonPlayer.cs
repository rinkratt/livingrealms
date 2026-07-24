using Godot;

namespace LivingRealms.Client;

public partial class ThirdPersonPlayer : CharacterBody3D
{
    private const float WalkSpeed = 5.0f;
    private const float SprintSpeed = 8.0f;
    private const float Acceleration = 22.0f;
    private const float JumpVelocity = 5.4f;
    private const float MouseSensitivity = 0.0026f;
    private const float MinimumCameraDistance = 2.8f;
    private const float MaximumCameraDistance = 18.0f;
    private const float CameraZoomStep = 1.0f;
    private const float CameraPivotHeight = 1.45f;
    private const float MinimumCameraGroundClearance = 0.35f;
    private const float MaximumUpwardCameraPitch = 0.18f;

    private readonly Color _gold = new("d8a94b");
    private Node3D _visualRoot = null!;
    private Node3D _cameraYaw = null!;
    private Node3D _cameraPitch = null!;
    private SpringArm3D _springArm = null!;
    private float _cameraYawAngle;
    private float _cameraPitchAngle = -0.22f;
    private float _targetCameraDistance = 6.2f;
    private float _attackCooldown;
    private float _gravity = 9.8f;
    private string _characterName = "Alden";
    private string _archetype = "Vanguard";
    private Node3D? _acceptedCharacterModel;
    private Skeleton3D? _characterSkeleton;
    private float _locomotionCycle;
    private float _locomotionBlend;
    private float _idleCycle;
    private Node3D? _heldBow;
    private Node3D? _heldSword;
    private MeshInstance3D? _bowStringUpper;
    private MeshInstance3D? _bowStringLower;
    private Node3D? _nockedArrow;
    private readonly List<Node3D> _stowedSwordParts = [];
    private bool _weaponReady;
    private Vector3 _combatTargetPosition;
    private float _attackAnimationRemaining;
    private float _attackAnimationDuration;

    public bool InputEnabled { get; set; } = true;
    public bool CombatEnabled { get; set; } = true;
    public bool IsRanger => _archetype.Equals("Ranger", StringComparison.OrdinalIgnoreCase);
    public event Action? AttackRequested;

    public void Configure(string characterName, string archetype)
    {
        _characterName = characterName;
        _archetype = archetype;
    }

    public void SetCombatTarget(Vector3? targetPosition)
    {
        _weaponReady = targetPosition.HasValue;
        if (targetPosition.HasValue)
        {
            _combatTargetPosition = targetPosition.Value;
        }
        UpdateWeaponVisibility();
    }

    public void PlayCombatAttack(Vector3 targetPosition)
    {
        _combatTargetPosition = targetPosition;
        _attackAnimationDuration = IsRanger ? 0.72f : 0.52f;
        _attackAnimationRemaining = _attackAnimationDuration;
        UpdateWeaponVisibility();
    }

    public Vector3 GetRangedProjectileOrigin(Vector3 targetPosition)
    {
        if (_characterSkeleton is not null)
        {
            var handIndex = _characterSkeleton.FindBone("hand_l");
            if (handIndex >= 0)
            {
                var handWorld = _characterSkeleton.GlobalTransform *
                                _characterSkeleton.GetBoneGlobalPose(handIndex);
                var direction = (targetPosition - handWorld.Origin).Normalized();
                return handWorld.Origin + direction * 0.34f + Vector3.Up * 0.04f;
            }
        }

        return GlobalPosition + new Vector3(0, 1.2f, 0);
    }

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1 | 4 | 8;
        FloorSnapLength = 0.3f;
        FloorMaxAngle = Mathf.DegToRad(48.0f);
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        var collider = new CollisionShape3D
        {
            Name = "PlayerCollider",
            Position = new Vector3(0, 0.95f, 0),
            Shape = new CapsuleShape3D
            {
                Radius = 0.34f,
                Height = 1.9f
            }
        };
        AddChild(collider);

        _visualRoot = new Node3D { Name = "CharacterModel" };
        AddChild(_visualRoot);
        if (!TryLoadAcceptedCharacterModel())
        {
            GD.PushWarning($"The accepted 3D model for {_characterName} could not be loaded; using the legacy fallback visual.");
            BuildCharacterModel();
        }
        BuildCameraRig();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
        {
            return;
        }

        // Keep the keyboard attack available even when F10 has released the
        // mouse for screenshots or another window. Mouse-look and mouse attacks
        // still require a captured pointer, but F must remain a dependable
        // combat control for both player archetypes.
        if (@event is InputEventKey
            {
                Keycode: Key.F,
                Pressed: true,
                Echo: false
            })
        {
            if (CombatEnabled && _attackCooldown <= 0)
            {
                _attackCooldown = IsRanger ? 0.75f : 0.55f;
                AttackRequested?.Invoke();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            _cameraYawAngle -= mouseMotion.Relative.X * MouseSensitivity;
            _cameraPitchAngle = Mathf.Clamp(
                _cameraPitchAngle - mouseMotion.Relative.Y * MouseSensitivity,
                -1.05f,
                MaximumUpwardCameraPitch);
            _cameraYaw.Rotation = new Vector3(0, _cameraYawAngle, 0);
            _cameraPitch.Rotation = new Vector3(_cameraPitchAngle, 0, 0);
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } wheelEvent &&
            wheelEvent.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            var change = wheelEvent.ButtonIndex == MouseButton.WheelUp
                ? -CameraZoomStep
                : CameraZoomStep;
            _targetCameraDistance = Mathf.Clamp(
                _targetCameraDistance + change,
                MinimumCameraDistance,
                MaximumCameraDistance);
            GetViewport().SetInputAsHandled();
            return;
        }

        var attackPressed = @event is InputEventMouseButton
        {
            ButtonIndex: MouseButton.Left,
            Pressed: true
        };
        if (attackPressed && CombatEnabled && _attackCooldown <= 0)
        {
            _attackCooldown = IsRanger ? 0.75f : 0.55f;
            AttackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var seconds = (float)delta;
        if (IsInstanceValid(_springArm))
        {
            var groundSafeDistance = _targetCameraDistance;
            if (_cameraPitchAngle > 0.001f)
            {
                groundSafeDistance = Mathf.Min(
                    groundSafeDistance,
                    (CameraPivotHeight - MinimumCameraGroundClearance) /
                    Mathf.Sin(_cameraPitchAngle));
            }
            _springArm.SpringLength = Mathf.MoveToward(
                _springArm.SpringLength,
                Mathf.Max(MinimumCameraDistance, groundSafeDistance),
                9.0f * seconds);
        }
        _attackCooldown = Mathf.Max(0, _attackCooldown - seconds);
        _attackAnimationRemaining = Mathf.Max(0, _attackAnimationRemaining - seconds);
        var velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= _gravity * seconds;
        }
        else if (InputEnabled && Input.IsKeyPressed(Key.Space))
        {
            velocity.Y = JumpVelocity;
        }
        else
        {
            velocity.Y = -0.1f;
        }

        var direction = InputEnabled ? ReadMovementDirection() : Vector3.Zero;
        var sprinting = InputEnabled &&
            (Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.Ctrl));
        var speed = sprinting ? SprintSpeed : WalkSpeed;
        var targetX = direction.X * speed;
        var targetZ = direction.Z * speed;
        velocity.X = Mathf.MoveToward(velocity.X, targetX, Acceleration * seconds);
        velocity.Z = Mathf.MoveToward(velocity.Z, targetZ, Acceleration * seconds);

        if (direction.LengthSquared() > 0.001f)
        {
            var targetRotation = Mathf.Atan2(direction.X, direction.Z);
            _visualRoot.Rotation = new Vector3(
                0,
                Mathf.LerpAngle(_visualRoot.Rotation.Y, targetRotation, 12.0f * seconds),
                0);
        }

        Velocity = velocity;
        MoveAndSlide();
        UpdateCharacterMotion(seconds, sprinting);
        UpdateWeaponVisibility();
    }

    private Vector3 ReadMovementDirection()
    {
        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y += 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y -= 1;
        if (input.LengthSquared() > 1.0f) input = input.Normalized();

        var forward = -_cameraYaw.GlobalTransform.Basis.Z;
        var right = _cameraYaw.GlobalTransform.Basis.X;
        forward.Y = 0;
        right.Y = 0;
        return (right.Normalized() * input.X + forward.Normalized() * input.Y).Normalized();
    }

    private void BuildCameraRig()
    {
        _cameraYaw = new Node3D
        {
            Name = "CameraYaw",
            Position = new Vector3(0, CameraPivotHeight, 0)
        };
        AddChild(_cameraYaw);

        _cameraPitch = new Node3D
        {
            Name = "CameraPitch",
            Rotation = new Vector3(_cameraPitchAngle, 0, 0)
        };
        _cameraYaw.AddChild(_cameraPitch);

        _springArm = new SpringArm3D
        {
            Name = "CameraCollisionArm",
            SpringLength = _targetCameraDistance,
            Shape = new SphereShape3D { Radius = 0.24f },
            Margin = 0.22f,
            CollisionMask = 1
        };
        _cameraPitch.AddChild(_springArm);

        var camera = new Camera3D
        {
            Name = "ThirdPersonCamera",
            Current = true,
            Fov = 68.0f,
            Near = 0.08f
        };
        _springArm.AddChild(camera);
    }

    private void BuildCharacterModel()
    {
        var isRanger = _characterName.Equals("Elara", StringComparison.OrdinalIgnoreCase) ||
                       _archetype.Equals("Ranger", StringComparison.OrdinalIgnoreCase);
        var cloth = isRanger ? new Color("263c2d") : new Color("182a43");
        var darkCloth = isRanger ? new Color("14241a") : new Color("101b2b");
        var leather = new Color("3a281c");
        var skin = isRanger ? new Color("c88f6c") : new Color("a97050");
        var metal = new Color("7d8388");

        AddPart("Torso", new CylinderMesh
        {
            TopRadius = 0.3f,
            BottomRadius = 0.42f,
            Height = 0.82f,
            RadialSegments = 10
        }, new Vector3(0, 1.18f, 0), Vector3.Zero, cloth);
        AddPart("ChestCrest", new BoxMesh { Size = new Vector3(0.2f, 0.32f, 0.035f) },
            new Vector3(0, 1.22f, -0.38f), Vector3.Zero, _gold);
        AddPart("Belt", new CylinderMesh
        {
            TopRadius = 0.43f,
            BottomRadius = 0.43f,
            Height = 0.14f,
            RadialSegments = 10
        }, new Vector3(0, 0.8f, 0), Vector3.Zero, leather);
        AddPart("Head", new SphereMesh
        {
            Radius = 0.25f,
            Height = 0.5f,
            RadialSegments = 12,
            Rings = 6
        }, new Vector3(0, 1.82f, 0), Vector3.Zero, skin);
        AddPart("Hair", new SphereMesh
        {
            Radius = 0.27f,
            Height = 0.32f,
            RadialSegments = 12,
            Rings = 6
        }, new Vector3(0, 1.96f, 0.02f), Vector3.Zero, new Color("241a14"));

        AddLimb("LeftArm", new Vector3(-0.42f, 1.22f, 0), new Vector3(0, 0, -0.12f), cloth);
        AddLimb("RightArm", new Vector3(0.42f, 1.22f, 0), new Vector3(0, 0, 0.12f), cloth);
        AddLimb("LeftLeg", new Vector3(-0.18f, 0.4f, 0), Vector3.Zero, darkCloth, 0.72f, 0.13f);
        AddLimb("RightLeg", new Vector3(0.18f, 0.4f, 0), Vector3.Zero, darkCloth, 0.72f, 0.13f);
        AddPart("LeftBoot", new BoxMesh { Size = new Vector3(0.25f, 0.22f, 0.38f) },
            new Vector3(-0.18f, 0.13f, -0.05f), Vector3.Zero, leather);
        AddPart("RightBoot", new BoxMesh { Size = new Vector3(0.25f, 0.22f, 0.38f) },
            new Vector3(0.18f, 0.13f, -0.05f), Vector3.Zero, leather);
        AddPart("Cape", new BoxMesh { Size = new Vector3(0.66f, 1.05f, 0.055f) },
            new Vector3(0, 1.0f, 0.34f), new Vector3(0.08f, 0, 0), darkCloth);

        if (isRanger)
        {
            BuildRangerEquipment(leather, metal);
        }
        else
        {
            BuildVanguardEquipment(leather, metal);
        }
    }

    private bool TryLoadAcceptedCharacterModel()
    {
        var isRanger = _characterName.Equals("Elara", StringComparison.OrdinalIgnoreCase) ||
                       _archetype.Equals("Ranger", StringComparison.OrdinalIgnoreCase);
        var modelPath = isRanger
            ? "res://Assets/Characters3D/elara.glb"
            : "res://Assets/Characters3D/alden.glb";
        if (!ResourceLoader.Exists(modelPath))
        {
            return false;
        }

        var packedModel = GD.Load<PackedScene>(modelPath);
        if (packedModel is null)
        {
            return false;
        }

        var model = packedModel.Instantiate<Node3D>();
        model.Name = isRanger ? "ElaraAcceptedModel" : "AldenAcceptedModel";
        model.Position = Vector3.Zero;
        model.Rotation = Vector3.Zero;
        model.Scale = Vector3.One;
        _visualRoot.AddChild(model);
        _acceptedCharacterModel = model;
        _characterSkeleton = FindDescendant<Skeleton3D>(model);
        if (_characterSkeleton is null)
        {
            GD.PushWarning($"The accepted 3D model for {_characterName} has no Skeleton3D; procedural movement is disabled.");
        }
        else
        {
            BuildAcceptedCombatEquipment(model);
        }
        return true;
    }

    private void UpdateCharacterMotion(float seconds, bool sprinting)
    {
        if (!IsInstanceValid(_acceptedCharacterModel) || !IsInstanceValid(_characterSkeleton))
        {
            return;
        }

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var moving = horizontalSpeed > 0.2f;
        var targetBlend = moving ? Mathf.Clamp(horizontalSpeed / WalkSpeed, 0.0f, 1.25f) : 0.0f;
        _locomotionBlend = Mathf.MoveToward(_locomotionBlend, targetBlend, seconds * 5.5f);
        _idleCycle += seconds * 1.7f;
        if (moving)
        {
            _locomotionCycle += seconds * (sprinting ? 11.5f : 8.5f);
        }

        var stride = Mathf.Sin(_locomotionCycle);
        var strideAmount = 0.42f * _locomotionBlend;
        var armWidth = IsRanger ? 0.17f : 0.19f;
        var idleSway = Mathf.Sin(_idleCycle) * 0.018f * (1.0f - Mathf.Min(_locomotionBlend, 1.0f));

        // The MPFB bind pose carries the arms roughly forty degrees away from
        // the body. Aim the upper-arm bones down beside the torso, then add an
        // opposing swing while walking or running.
        SetBoneDirection(
            "upperarm_l",
            new Vector3(armWidth, -1.0f, (stride * strideAmount) + idleSway));
        SetBoneDirection(
            "upperarm_r",
            new Vector3(-armWidth, -1.0f, (-stride * strideAmount) - idleSway));

        UpdateCombatPose(seconds);

        var legStride = stride * 0.34f * _locomotionBlend;
        SetBoneDirection("thigh_l", new Vector3(0.07f, -1.0f, -legStride));
        SetBoneDirection("thigh_r", new Vector3(-0.07f, -1.0f, legStride));
        var leftKneeBend = Mathf.Max(0.0f, stride) * 0.55f * Mathf.Min(_locomotionBlend, 1.0f);
        var rightKneeBend = Mathf.Max(0.0f, -stride) * 0.55f * Mathf.Min(_locomotionBlend, 1.0f);
        SetBoneLocalRotation("calf_l", new Quaternion(Vector3.Right, -leftKneeBend));
        SetBoneLocalRotation("calf_r", new Quaternion(Vector3.Right, -rightKneeBend));

        var gaitBob = moving
            ? Mathf.Abs(Mathf.Sin(_locomotionCycle * 2.0f)) * 0.035f * Mathf.Min(_locomotionBlend, 1.0f)
            : Mathf.Sin(_idleCycle) * 0.006f;
        _acceptedCharacterModel!.Position = new Vector3(0, gaitBob, 0);
    }

    private void UpdateCombatPose(float seconds)
    {
        var attacking = _attackAnimationRemaining > 0 && _attackAnimationDuration > 0;
        if (!_weaponReady && !attacking)
        {
            if (IsRanger && _characterSkeleton is not null)
            {
                var leftForearm = _characterSkeleton.FindBone("lowerarm_l");
                var rightForearm = _characterSkeleton.FindBone("lowerarm_r");
                if (leftForearm >= 0) _characterSkeleton.ResetBonePose(leftForearm);
                if (rightForearm >= 0) _characterSkeleton.ResetBonePose(rightForearm);
            }
            UpdateBowDraw(0.0f, false);
            return;
        }

        if (attacking)
        {
            var targetOffset = _combatTargetPosition - GlobalPosition;
            targetOffset.Y = 0;
            if (targetOffset.LengthSquared() > 0.01f)
            {
                var targetRotation = Mathf.Atan2(targetOffset.X, targetOffset.Z);
                _visualRoot.Rotation = new Vector3(
                    0,
                    Mathf.LerpAngle(_visualRoot.Rotation.Y, targetRotation, 22.0f * seconds),
                    0);
            }
        }

        var progress = attacking
            ? 1.0f - (_attackAnimationRemaining / _attackAnimationDuration)
            : 0.0f;
        if (IsRanger)
        {
            var draw = !attacking
                ? 0.12f
                : progress < 0.55f
                    ? Mathf.Clamp(progress / 0.55f, 0.0f, 1.0f)
                    : progress < 0.68f
                        ? 1.0f - Mathf.Clamp((progress - 0.55f) / 0.13f, 0.0f, 1.0f)
                        : 0.0f;
            SetBoneDirection("upperarm_l", new Vector3(0.22f, -0.08f, 1.0f));
            SetBoneDirection(
                "upperarm_r",
                new Vector3(-0.38f, -0.08f, 0.72f).Lerp(
                    new Vector3(-0.72f, 0.18f, 0.20f),
                    draw));
            SetBoneCurrentDirection("lowerarm_l", new Vector3(0.03f, -0.04f, 1.0f));
            SetBoneCurrentDirection(
                "lowerarm_r",
                new Vector3(0.52f, 0.14f, 0.68f).Lerp(
                    new Vector3(0.82f, 0.20f, 0.24f),
                    draw));
            UpdateBowDraw(draw, attacking && progress >= 0.62f);
            return;
        }

        var ready = new Vector3(-0.18f, -0.78f, 0.48f);
        if (!attacking)
        {
            SetBoneDirection("upperarm_r", ready);
            return;
        }

        var windup = new Vector3(-0.38f, 0.28f, -0.82f);
        var strike = new Vector3(-0.12f, -0.30f, 1.0f);
        var swordArm = progress < 0.34f
            ? ready.Lerp(windup, Mathf.SmoothStep(0.0f, 1.0f, progress / 0.34f))
            : windup.Lerp(strike, Mathf.SmoothStep(0.0f, 1.0f, (progress - 0.34f) / 0.66f));
        SetBoneDirection("upperarm_r", swordArm);
    }

    private void SetBoneDirection(string boneName, Vector3 desiredDirection)
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        var boneIndex = _characterSkeleton.FindBone(boneName);
        if (boneIndex < 0)
        {
            return;
        }

        var globalRest = _characterSkeleton.GetBoneGlobalRest(boneIndex);
        var restDirection = globalRest.Basis.Y.Normalized();
        var desired = desiredDirection.Normalized();
        var globalDelta = new Quaternion(restDirection, desired);
        var desiredGlobalPose = new Transform3D(
            new Basis(globalDelta) * globalRest.Basis,
            globalRest.Origin);
        _characterSkeleton.SetBoneGlobalPose(boneIndex, desiredGlobalPose);
    }

    private void SetBoneLocalRotation(string boneName, Quaternion rotation)
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        var boneIndex = _characterSkeleton.FindBone(boneName);
        if (boneIndex >= 0)
        {
            _characterSkeleton.SetBonePoseRotation(boneIndex, rotation.Normalized());
        }
    }

    private void SetBoneCurrentDirection(string boneName, Vector3 desiredDirection)
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        var boneIndex = _characterSkeleton.FindBone(boneName);
        if (boneIndex < 0)
        {
            return;
        }

        var parentIndex = _characterSkeleton.GetBoneParent(boneIndex);
        if (parentIndex >= 0)
        {
            _characterSkeleton.ForceUpdateBoneChildTransform(parentIndex);
        }
        var currentPose = _characterSkeleton.GetBoneGlobalPose(boneIndex);
        var currentDirection = currentPose.Basis.Y.Normalized();
        var desired = desiredDirection.Normalized();
        var globalDelta = new Quaternion(currentDirection, desired);
        _characterSkeleton.SetBoneGlobalPose(
            boneIndex,
            new Transform3D(new Basis(globalDelta) * currentPose.Basis, currentPose.Origin));
    }

    private static T? FindDescendant<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void BuildAcceptedCombatEquipment(Node3D model)
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        foreach (var node in EnumerateDescendants(model))
        {
            if (node is not Node3D spatial)
            {
                continue;
            }

            var nodeName = spatial.Name.ToString();
            if (IsRanger &&
                (nodeName.Contains("ElaraRecurveBow", StringComparison.OrdinalIgnoreCase) ||
                 nodeName.Contains("ElaraBowString", StringComparison.OrdinalIgnoreCase)))
            {
                spatial.Visible = false;
            }
            else if (!IsRanger &&
                     (nodeName.Contains("AldenSwordGrip", StringComparison.OrdinalIgnoreCase) ||
                      nodeName.Contains("AldenSwordGuard", StringComparison.OrdinalIgnoreCase) ||
                      nodeName.Contains("AldenSwordPommel", StringComparison.OrdinalIgnoreCase)))
            {
                _stowedSwordParts.Add(spatial);
            }
        }

        if (IsRanger)
        {
            BuildHeldBow();
        }
        else
        {
            BuildHeldSword();
        }
        UpdateWeaponVisibility();
    }

    private void BuildHeldBow()
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        var attachment = new BoneAttachment3D
        {
            Name = "ElaraBowHandAttachment",
            BoneName = new StringName("hand_l")
        };
        _characterSkeleton.AddChild(attachment);
        _heldBow = new Node3D
        {
            Name = "ElaraCombatBow",
            Position = new Vector3(0, 0.09f, 0),
            Visible = false
        };
        attachment.AddChild(_heldBow);

        var bowWood = new Color("6f351d");
        var bowEdge = new Color("c28b3e");
        var leather = new Color("24130d");
        var stringColor = new Color("d8cfbb");
        var limbPoints = new[]
        {
            new Vector3(-0.72f, 0.0f, 0.015f),
            new Vector3(-0.61f, -0.045f, -0.035f),
            new Vector3(-0.34f, -0.11f, -0.07f),
            new Vector3(-0.08f, -0.07f, -0.025f),
            Vector3.Zero,
            new Vector3(0.08f, -0.07f, -0.025f),
            new Vector3(0.34f, -0.11f, -0.07f),
            new Vector3(0.61f, -0.045f, -0.035f),
            new Vector3(0.72f, 0.0f, 0.015f)
        };
        for (var index = 0; index < limbPoints.Length - 1; index++)
        {
            AddWeaponBeam(
                _heldBow,
                $"RecurveLimb{index}",
                limbPoints[index],
                limbPoints[index + 1],
                index is 3 or 4 ? 0.022f : 0.017f,
                index is 0 or 7 ? bowEdge : bowWood,
                0.08f,
                0.48f);
        }

        AddWeaponBeam(
            _heldBow,
            "LeatherGrip",
            new Vector3(-0.09f, -0.065f, -0.02f),
            new Vector3(0.09f, -0.065f, -0.02f),
            0.032f,
            leather,
            0.0f,
            0.72f);
        _bowStringUpper = AddWeaponBeam(
            _heldBow,
            "BowStringUpper",
            limbPoints[^1],
            Vector3.Zero,
            0.0045f,
            stringColor,
            0.0f,
            0.35f);
        _bowStringLower = AddWeaponBeam(
            _heldBow,
            "BowStringLower",
            limbPoints[0],
            Vector3.Zero,
            0.0045f,
            stringColor,
            0.0f,
            0.35f);

        _nockedArrow = new Node3D { Name = "NockedArrow", Visible = false };
        _heldBow.AddChild(_nockedArrow);
        AddWeaponBeam(
            _nockedArrow,
            "NockedArrowShaft",
            new Vector3(0, -0.33f, 0),
            new Vector3(0, 0.54f, 0),
            0.009f,
            new Color("8a572e"),
            0.0f,
            0.58f);
        AddWeaponCone(
            _nockedArrow,
            "NockedArrowHead",
            new Vector3(0, 0.60f, 0),
            Vector3.Up,
            0.030f,
            0.10f,
            new Color("aeb4b5"),
            0.82f);
        AddWeaponFletching(_nockedArrow, new Vector3(0, -0.29f, 0));
    }

    private void BuildHeldSword()
    {
        if (_characterSkeleton is null)
        {
            return;
        }

        var attachment = new BoneAttachment3D
        {
            Name = "AldenSwordHandAttachment",
            BoneName = new StringName("hand_r")
        };
        _characterSkeleton.AddChild(attachment);
        _heldSword = new Node3D
        {
            Name = "AldenCombatSword",
            Position = new Vector3(0, 0.09f, 0),
            Visible = false
        };
        attachment.AddChild(_heldSword);

        AddWeaponBoxBeam(
            _heldSword,
            "SwordBlade",
            new Vector3(0.10f, 0, 0),
            new Vector3(0.91f, 0, 0),
            0.068f,
            0.024f,
            new Color("aeb7bb"),
            0.92f,
            0.22f);
        AddWeaponCone(
            _heldSword,
            "SwordPoint",
            new Vector3(0.98f, 0, 0),
            Vector3.Right,
            0.052f,
            0.15f,
            new Color("c8d0d2"),
            0.94f);
        AddWeaponBeam(
            _heldSword,
            "SwordGuard",
            new Vector3(0.05f, 0, -0.17f),
            new Vector3(0.05f, 0, 0.17f),
            0.022f,
            _gold,
            0.78f,
            0.28f);
        AddWeaponBeam(
            _heldSword,
            "SwordGrip",
            new Vector3(-0.02f, 0, 0),
            new Vector3(-0.24f, 0, 0),
            0.03f,
            new Color("2b1710"),
            0.0f,
            0.66f);
        var pommel = new MeshInstance3D
        {
            Name = "SwordPommel",
            Mesh = new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 12, Rings = 6 },
            Position = new Vector3(-0.28f, 0, 0),
            MaterialOverride = CreateWeaponMaterial(_gold, 0.76f, 0.3f)
        };
        _heldSword.AddChild(pommel);
    }

    private void UpdateWeaponVisibility()
    {
        var showHeldWeapon = _weaponReady || _attackAnimationRemaining > 0;
        if (IsInstanceValid(_heldBow))
        {
            _heldBow!.Visible = IsRanger && showHeldWeapon;
        }
        if (IsInstanceValid(_heldSword))
        {
            _heldSword!.Visible = !IsRanger && showHeldWeapon;
        }
        foreach (var stowedPart in _stowedSwordParts)
        {
            if (IsInstanceValid(stowedPart))
            {
                stowedPart.Visible = !showHeldWeapon;
            }
        }
        if (!showHeldWeapon && IsInstanceValid(_nockedArrow))
        {
            _nockedArrow!.Visible = false;
        }
    }

    private void UpdateBowDraw(float draw, bool released)
    {
        if (!IsInstanceValid(_bowStringUpper) || !IsInstanceValid(_bowStringLower))
        {
            return;
        }

        var nock = new Vector3(0, -0.31f * draw, 0);
        SetWeaponBeam(_bowStringUpper!, new Vector3(0.72f, 0, 0.015f), nock);
        SetWeaponBeam(_bowStringLower!, new Vector3(-0.72f, 0, 0.015f), nock);
        if (IsInstanceValid(_nockedArrow))
        {
            _nockedArrow!.Position = new Vector3(0, -0.31f * draw, 0);
            _nockedArrow.Visible = (_weaponReady || _attackAnimationRemaining > 0) && !released;
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static MeshInstance3D AddWeaponBeam(
        Node3D parent,
        string name,
        Vector3 from,
        Vector3 to,
        float radius,
        Color color,
        float metallic,
        float roughness)
    {
        var beam = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 1.0f,
                RadialSegments = radius < 0.007f ? 6 : 10
            },
            MaterialOverride = CreateWeaponMaterial(color, metallic, roughness),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        parent.AddChild(beam);
        SetWeaponBeam(beam, from, to);
        return beam;
    }

    private static void SetWeaponBeam(MeshInstance3D beam, Vector3 from, Vector3 to)
    {
        var difference = to - from;
        var length = Mathf.Max(0.001f, difference.Length());
        if (beam.Mesh is CylinderMesh cylinder)
        {
            cylinder.Height = length;
        }
        beam.Position = (from + to) * 0.5f;
        beam.Quaternion = new Quaternion(Vector3.Up, difference / length);
    }

    private static void AddWeaponBoxBeam(
        Node3D parent,
        string name,
        Vector3 from,
        Vector3 to,
        float width,
        float depth,
        Color color,
        float metallic,
        float roughness)
    {
        var difference = to - from;
        var length = Mathf.Max(0.001f, difference.Length());
        var part = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = new Vector3(width, length, depth) },
            Position = (from + to) * 0.5f,
            Quaternion = new Quaternion(Vector3.Up, difference / length),
            MaterialOverride = CreateWeaponMaterial(color, metallic, roughness),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        parent.AddChild(part);
    }

    private static void AddWeaponCone(
        Node3D parent,
        string name,
        Vector3 center,
        Vector3 direction,
        float radius,
        float height,
        Color color,
        float metallic)
    {
        var part = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 8
            },
            Position = center,
            Quaternion = new Quaternion(Vector3.Up, direction.Normalized()),
            MaterialOverride = CreateWeaponMaterial(color, metallic, 0.24f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        parent.AddChild(part);
    }

    private static void AddWeaponFletching(Node3D parent, Vector3 position)
    {
        var material = CreateWeaponMaterial(new Color("8e2119"), 0.0f, 0.75f);
        for (var index = 0; index < 2; index++)
        {
            var feather = new MeshInstance3D
            {
                Name = $"ArrowFletching{index}",
                Mesh = new BoxMesh { Size = new Vector3(0.058f, 0.13f, 0.009f) },
                Position = position,
                Rotation = new Vector3(0, index * Mathf.Pi * 0.5f, 0),
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            parent.AddChild(feather);
        }
    }

    private static StandardMaterial3D CreateWeaponMaterial(Color color, float metallic, float roughness) => new()
    {
        AlbedoColor = color,
        Metallic = metallic,
        Roughness = roughness
    };

    private void BuildVanguardEquipment(Color leather, Color metal)
    {
        AddPart("LeftShoulder", new SphereMesh { Radius = 0.25f, Height = 0.32f, RadialSegments = 10, Rings = 4 },
            new Vector3(-0.42f, 1.52f, 0), Vector3.Zero, metal);
        AddPart("RightShoulder", new SphereMesh { Radius = 0.25f, Height = 0.32f, RadialSegments = 10, Rings = 4 },
            new Vector3(0.42f, 1.52f, 0), Vector3.Zero, metal);
        AddPart("SwordBlade", new BoxMesh { Size = new Vector3(0.075f, 1.0f, 0.035f) },
            new Vector3(0.57f, 0.75f, -0.05f), new Vector3(0, 0, -0.13f), new Color("c6c8c5"), 0.85f);
        AddPart("SwordGuard", new BoxMesh { Size = new Vector3(0.3f, 0.06f, 0.08f) },
            new Vector3(0.64f, 0.26f, -0.05f), new Vector3(0, 0, -0.13f), _gold, 0.65f);
        AddPart("SwordGrip", new CylinderMesh
        {
            TopRadius = 0.045f,
            BottomRadius = 0.045f,
            Height = 0.28f,
            RadialSegments = 8
        }, new Vector3(0.68f, 0.09f, -0.05f), new Vector3(0, 0, -0.13f), leather);
    }

    private void BuildRangerEquipment(Color leather, Color metal)
    {
        AddPart("BowUpper", new CylinderMesh
        {
            TopRadius = 0.035f,
            BottomRadius = 0.035f,
            Height = 0.85f,
            RadialSegments = 8
        }, new Vector3(0.58f, 1.13f, -0.05f), new Vector3(0, 0, -0.22f), leather);
        AddPart("BowLower", new CylinderMesh
        {
            TopRadius = 0.035f,
            BottomRadius = 0.035f,
            Height = 0.85f,
            RadialSegments = 8
        }, new Vector3(0.58f, 0.39f, -0.05f), new Vector3(0, 0, 0.22f), leather);
        AddPart("BowGrip", new BoxMesh { Size = new Vector3(0.12f, 0.24f, 0.08f) },
            new Vector3(0.58f, 0.76f, -0.05f), Vector3.Zero, _gold);
        AddPart("Quiver", new CylinderMesh
        {
            TopRadius = 0.12f,
            BottomRadius = 0.12f,
            Height = 0.9f,
            RadialSegments = 8
        }, new Vector3(-0.28f, 1.15f, 0.38f), new Vector3(0, 0, -0.28f), leather);
        AddPart("ArrowTips", new CylinderMesh
        {
            TopRadius = 0.04f,
            BottomRadius = 0.09f,
            Height = 0.22f,
            RadialSegments = 6
        }, new Vector3(-0.48f, 1.65f, 0.4f), new Vector3(0, 0, -0.28f), metal, 0.65f);
    }

    private void AddLimb(
        string name,
        Vector3 position,
        Vector3 rotation,
        Color color,
        float height = 0.72f,
        float radius = 0.12f)
    {
        AddPart(name, new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius * 1.08f,
            Height = height,
            RadialSegments = 9
        }, position, rotation, color);
    }

    private void AddPart(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        float metallic = 0.0f)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = metallic > 0.5f ? 0.28f : 0.82f,
            Metallic = metallic
        };
        var part = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        _visualRoot.AddChild(part);
    }
}
