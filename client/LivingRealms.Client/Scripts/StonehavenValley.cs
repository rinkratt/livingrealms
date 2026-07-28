using Godot;

namespace LivingRealms.Client;

public partial class StonehavenValley : Node3D
{
    private static readonly string FeedbackUrl =
        $"https://living-realms.com/feedback.php?source=game&build={BuildInfo.Version}";
    private const float KnockoutProtectionDuration = 8.0f;
    private const float TargetCycleRadius = 32.0f;
    private const float WorldGridSize = 96.0f;
    private const float WorldHalfExtent = WorldGridSize * 1.5f;
    private const float PlayableWorldLimit = 140.0f;
    private const float ResidentActivationRadius = 52.0f;
    private const int MaximumActiveResidents = 24;
    private const int MaximumRaidResidents = 32;
    private const float DarkwoodCampClearingRadius = 19.5f;
    private const string StylizedEnvironmentScenePath = "res://Assets/Environment/stonehaven_vertical_slice.glb";
    private const string WillowmereDragonScenePath = "res://Assets/Creatures3D/dragon-3dhaupt.glb";
    private const string MedievalFarmhouseScenePath =
        "res://Assets/Environment/Production/medieval-farmhouse.glb";
    private const string MeadowGrassScenePath =
        "res://Assets/Environment/Production/meadow-grass-clump.glb";
    private const string WoodlandBushScenePath =
        "res://Assets/Environment/Production/woodland-bush.glb";
    private const string StonehavenStoneWallScenePath =
        "res://Assets/Environment/Production/stonehaven-stone-wall.glb";
    private static readonly string[] ProductionTreeScenePaths =
    [
        "res://Assets/Environment/Production/meadow-oak.glb",
        "res://Assets/Environment/Production/mature-broadleaf.glb"
    ];
    private static readonly string[] ProductionRockScenePaths =
    [
        "res://Assets/Environment/Production/moss-rock-01.glb",
        "res://Assets/Environment/Production/moss-rock-02.glb",
        "res://Assets/Environment/Production/moss-rock-03.glb"
    ];
    private static readonly Vector3[] ProductionRockDimensions =
    [
        new(2.0f, 1.001575f, 1.390607f),
        new(2.0f, 0.570262f, 1.870107f),
        new(2.0f, 1.088725f, 1.203366f)
    ];
    private static readonly Vector3 DarkwoodCampCenter = new(-116.0f, 0, -104.0f);
    private static readonly Vector3 MirrorwaterLakeCenter = new(101.0f, 0, -20.0f);
    private static readonly Vector3 IronMineCenter = new(104.0f, 0, -104.0f);
    private static readonly Vector3 StonehavenFarmlandCenter = new(0, 0, 96.0f);
    private static readonly Vector3 CreatureTestingGroundsCenter = new(96.0f, 0, 92.0f);
    private static readonly Vector3 WillowmereDragonRoostCenter = new(-112.0f, 0, 104.0f);
    private static readonly Vector2 StonehavenLumberYardCenter = new(-22.0f, -19.5f);
    private static readonly Vector2 StonehavenLumberYardClearance = new(5.25f, 4.0f);
    private static readonly Color Gold = new("d8a94b");
    private static readonly Color Red = new("8e2119");
    private static readonly Color Ink = new("101116");
    private static readonly Color Parchment = new("d8cfbb");
    private static readonly Vector3[] DragonRoamingAnchors =
    [
        new(-112.0f, 0.12f, 104.0f),
        new(0.0f, 0.12f, 116.0f),
        new(112.0f, 0.12f, 100.0f),
        new(-112.0f, 0.12f, 0.0f),
        new(-42.0f, 0.12f, 34.0f),
        new(65.0f, 0.12f, -8.0f),
        new(-82.0f, 0.12f, -112.0f),
        new(0.0f, 0.12f, -112.0f),
        new(90.0f, 0.12f, -90.0f)
    ];
    private static readonly string[,] WorldGridNames =
    {
        { "Darkwood Reach", "Northwatch Moor", "Irondeep Mining District" },
        { "Amberfield", "Stonehaven Valley", "Mirrorwater Lake" },
        { "Willowmere", "Stonehaven Farmlands", "Creature Testing Grounds" }
    };

    private readonly Vector3 _safeSpawn = new(0, 0.08f, 8);
    private readonly Dictionary<Guid, CombatCreature> _creatures = [];
    private readonly Dictionary<Guid, SettlementNpc> _residents = [];
    private readonly Dictionary<Guid, WorldResidentData> _residentRoster = [];
    private readonly Dictionary<Guid, HarvestResourceNode> _resourceNodes = [];
    private readonly List<NaturalResourceTarget> _naturalResourceTargets = [];
    private readonly Dictionary<Guid, ConstructionProjectData> _constructionProjects = [];
    private readonly Dictionary<string, DestructibleStructureData> _destructibleStructures =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorldSkillData> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RoamingDragon> _roamingDragons = [];
    private readonly List<WorldPathObstacle> _pathObstacles = [];
    private readonly List<WorldPathObstacle> _constructionPathObstacles = [];
    private readonly Dictionary<string, Node3D> _worldRegionRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackedScene> _productionEnvironmentScenes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Mesh> _productionEnvironmentMeshes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Vector2 Center, float Radius)> _natureFootprints = [];
    private WorldPathfinder _pathfinder = null!;
    private ThirdPersonPlayer _player = null!;
    private Label _coordinates = null!;
    private Label _saveStatus = null!;
    private Label _identityLabel = null!;
    private Label _healthLabel = null!;
    private Label _experienceLabel = null!;
    private Label _combatStatus = null!;
    private Label _targetLabel = null!;
    private Label _skillLabel = null!;
    private Label _inventoryStats = null!;
    private ProgressBar _healthBar = null!;
    private ProgressBar _experienceBar = null!;
    private Control _menuOverlay = null!;
    private Control _inventoryOverlay = null!;
    private Control _worldOverlay = null!;
    private VBoxContainer _inventoryRows = null!;
    private Label _worldHudLabel = null!;
    private Label _raidHudLabel = null!;
    private Label _developmentHudLabel = null!;
    private Label _carriedResourcesHudLabel = null!;
    private Label _worldSummary = null!;
    private Label _worldDetails = null!;
    private Label _darkwoodReadiness = null!;
    private Label _counterattackReadiness = null!;
    private Label _raidDetails = null!;
    private Label _chronicleText = null!;
    private Button _advanceWorldButton = null!;
    private Button _resetWorldButton = null!;
    private Button _startRaidButton = null!;
    private Button _startCounterattackButton = null!;
    private Button _refreshWorldButton = null!;
    private Node3D _darkwoodCamp = null!;
    private Node3D _constructionRoot = null!;
    private Node3D _structureStateRoot = null!;
    private Node3D? _stylizedEnvironmentRoot;
    private Button _menuSaveButton = null!;
    private Button _menuReturnButton = null!;
    private Button _menuLogoutButton = null!;
    private Godot.Timer _autosaveTimer = null!;
    private Godot.Timer _creatureRefreshTimer = null!;
    private Godot.Timer _raidAdvanceTimer = null!;
    private Godot.Timer _developmentRefreshTimer = null!;
    private string _characterName = "Alden";
    private string _archetype = "Vanguard";
    private string _region = "Stonehaven Valley";
    private int _level = 1;
    private int _health = 100;
    private int _maximumHealth = 100;
    private long _experience;
    private Vector3 _requestedSpawn;
    private bool _configured;
    private bool _menuOpen;
    private bool _inventoryOpen;
    private bool _worldOpen;
    private bool _mouseReleasedForSharing;
    private bool _isAdministrator;
    private bool _showDetailedOverhead;
    private int _campStage;
    private float _resetConfirmationSeconds;
    private float _knockoutProtectionSeconds;
    private bool _raidActive;
    private string _darkwoodRaidPhase = string.Empty;
    private bool _counterattackActive;
    private bool _canStartDarkwoodRaid;
    private bool _canStartCounterattack;
    private string _counterattackPhase = string.Empty;
    private bool _raidCombatObservedSinceAdvance;
    private bool _stylizedEnvironmentLoaded;
    private string _loadedRegionCenterCode = string.Empty;
    private Guid? _selectedTargetId;
    private float _settlementDefenseRefreshSeconds;
    private float _resourceMarkerRefreshSeconds;
    private float _residentStreamRefreshSeconds;
    private Vector3 _lastResidentStreamPosition = new(float.MaxValue, 0, float.MaxValue);

    public event Action<Vector3>? SaveRequested;
    public event Action<Vector3>? ReturnRequested;
    public event Action<Vector3>? LogoutRequested;
    public event Action<Guid, Vector3, Vector3>? PlayerAttackRequested;
    public event Action<Guid, Vector3, Vector3>? CreatureAttackRequested;
    public event Action? CreatureRefreshRequested;
    public event Action<string, Guid?, Vector3, Vector3?>? SkillRequested;
    public event Action<Guid, string, Vector3>? InventoryActionRequested;
    public event Action? WorldStateRequested;
    public event Action? DevelopmentStateRequested;
    public event Action<Guid, Vector3>? ResourceHarvestRequested;
    public event Action<string, Vector3, Vector3>? NaturalResourceHarvestRequested;
    public event Action<Guid, Vector3>? ProjectContributionRequested;
    public event Action<string, Guid>? NpcWorkRequested;
    public event Action<int>? WorldAdvanceRequested;
    public event Action? WorldResetRequested;
    public event Action? RaidStateRequested;
    public event Action? RaidStartRequested;
    public event Action? CounterattackStartRequested;
    public event Action? RaidAdvanceRequested;
    public event Action<Guid, Guid, Vector3, Vector3>? SettlementDefenseAttackRequested;

    public Vector3 PlayerPosition => IsInstanceValid(_player) ? _player.GlobalPosition : _requestedSpawn;

    public IReadOnlyCollection<WorldCreaturePosition> CreaturePositions => _creatures.Values
        .Where(creature => creature.IsAlive && creature.GlobalPosition.IsFinite())
        .Select(creature => new WorldCreaturePosition(
            creature.CreatureId,
            SanitizeCreatureSavePosition(creature.GlobalPosition)))
        .ToArray();

    public void Configure(
        string characterName,
        string archetype,
        int level,
        long experience,
        int health,
        int maximumHealth,
        string region,
        Vector3 savedPosition,
        bool isAdministrator)
    {
        _characterName = characterName;
        _archetype = archetype;
        _level = level;
        _experience = experience;
        _health = health;
        _maximumHealth = maximumHealth;
        _region = region;
        _requestedSpawn = SanitizeSpawn(savedPosition);
        _isAdministrator = isAdministrator;
        _configured = true;
    }

    public override void _Ready()
    {
        if (!_configured)
        {
            _requestedSpawn = _safeSpawn;
        }

        BuildLightingAndSky();
        CreateWorldRegionRoots();
        LoadStylizedEnvironment();
        BuildValleyFloor();
        BuildStonehavenVillage();
        BuildOutskirts();
        BuildRequestedRegionalFeatures();
        BuildExpandedRegions();
        RefreshLoadedWorldRegions(_requestedSpawn, true);
        _constructionRoot = new Node3D { Name = "PersistentConstruction" };
        AddChild(_constructionRoot);
        _structureStateRoot = new Node3D { Name = "PersistentStructureState" };
        AddChild(_structureStateRoot);
        BuildWorldPathfinder();
        SpawnPlayer();
        SpawnRoamingDragons();
        BuildInterface();
        if (!_configured)
        {
            LoadCreatures(CreatePreviewCreatures());
            SetInventory(new WorldInventoryData(
                34,
                11,
                12,
                24,
                80,
                [
                    new WorldInventoryItem(Guid.NewGuid(), "stonehaven-training-blade", "Stonehaven Training Blade", "Weapon", "Common", "Weapon", 5, 0, 0, 6, 6, "Brann the Blacksmith", 1, true),
                    new WorldInventoryItem(Guid.NewGuid(), "stonehaven-leather-guard", "Stonehaven Leather Guard", "Armor", "Common", "Armor", 0, 3, 0, 8, 8, "Brann the Blacksmith", 1, true),
                    new WorldInventoryItem(Guid.NewGuid(), "field-tonic", "Field Tonic", "Consumable", "Uncommon", null, 0, 0, 35, 1, 2, "Elowen the Healer", 2, false),
                    new WorldInventoryItem(Guid.NewGuid(), "raw-timber", "Raw Timber", "Resource", "Common", null, 0, 0, 0, 1, 8, "Oren the Storekeeper", 8, false)
                ]));
            SetSkills(
            [
                new WorldSkillData("shield-bash", "Shield Bash", "A crushing close-range strike.", "Q", 5, true, 3.2f),
                new WorldSkillData("second-wind", "Second Wind", "Recover health.", "E", 14, false, 0)
            ]);
            SetWorldState(CreatePreviewWorldState());
        }

        _autosaveTimer = new Godot.Timer
        {
            Name = "AutosaveTimer",
            WaitTime = 10.0,
            OneShot = false,
            Autostart = true
        };
        _autosaveTimer.Timeout += () => SaveRequested?.Invoke(PlayerPosition);
        AddChild(_autosaveTimer);

        _creatureRefreshTimer = new Godot.Timer
        {
            Name = "CreatureRefreshTimer",
            WaitTime = 15.0,
            OneShot = false,
            Autostart = true
        };
        _creatureRefreshTimer.Timeout += () => CreatureRefreshRequested?.Invoke();
        AddChild(_creatureRefreshTimer);

        _raidAdvanceTimer = new Godot.Timer
        {
            Name = "RaidAdvanceTimer",
            // Raid rounds use a readable real-time pace. The accelerated world
            // clock must not make an on-screen battle resolve in a few seconds.
            WaitTime = 12.0,
            OneShot = false,
            Autostart = true
        };
        _raidAdvanceTimer.Timeout += () =>
        {
            if (_raidActive)
            {
                _raidCombatObservedSinceAdvance = false;
                RaidAdvanceRequested?.Invoke();
            }
        };
        AddChild(_raidAdvanceTimer);

        _developmentRefreshTimer = new Godot.Timer
        {
            Name = "DevelopmentRefreshTimer",
            WaitTime = 12.0,
            OneShot = false,
            Autostart = true
        };
        _developmentRefreshTimer.Timeout += () => DevelopmentStateRequested?.Invoke();
        AddChild(_developmentRefreshTimer);

        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print($"Living Realms Phase 7B: {_characterName} entered {_region} at {PlayerPosition}.");
    }

    public override void _ExitTree()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _pathfinder?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (_knockoutProtectionSeconds > 0)
        {
            _knockoutProtectionSeconds = Mathf.Max(0, _knockoutProtectionSeconds - (float)delta);
            if (_knockoutProtectionSeconds <= 0)
            {
                ApplyOverlayPauseState();
                SetCombatStatus("Stonehaven's sanctuary protection has faded.", false);
            }
        }
        if (_resetConfirmationSeconds > 0)
        {
            _resetConfirmationSeconds = Mathf.Max(0, _resetConfirmationSeconds - (float)delta);
            if (_resetConfirmationSeconds <= 0 && IsInstanceValid(_resetWorldButton))
            {
                _resetWorldButton.Text = "Reset Living World  [Playtest]";
            }
        }
        if (!IsInstanceValid(_player))
        {
            return;
        }

        if (_player.GlobalPosition.Y < -8.0f ||
            Mathf.Abs(_player.GlobalPosition.X) > PlayableWorldLimit + 2.0f ||
            Mathf.Abs(_player.GlobalPosition.Z) > PlayableWorldLimit + 2.0f)
        {
            _player.GlobalPosition = _safeSpawn;
            _player.Velocity = Vector3.Zero;
            SetSaveStatus("Returned safely to Stonehaven's gate.", false);
        }

        var position = _player.GlobalPosition;
        _residentStreamRefreshSeconds -= (float)delta;
        if (_residentStreamRefreshSeconds <= 0 ||
            HorizontalDistance(position, _lastResidentStreamPosition) >= 4.0f)
        {
            _residentStreamRefreshSeconds = 0.75f;
            _lastResidentStreamPosition = position;
            RefreshActiveResidents();
        }
        var currentGrid = GetWorldGrid(position);
        RefreshLoadedWorldRegions(position);
        _coordinates.Text = $"GRID {currentGrid.Code}  •  {currentGrid.Name.ToUpperInvariant()}    " +
                            $"X {position.X:0.0}   Y {position.Y:0.0}   Z {position.Z:0.0}";
        _settlementDefenseRefreshSeconds -= (float)delta;
        if (_settlementDefenseRefreshSeconds <= 0)
        {
            _settlementDefenseRefreshSeconds = 0.35f;
            UpdateRaidCombatAssignments();
        }
        UpdateTargetLabel();
        UpdatePlayerCombatReadiness();
        _resourceMarkerRefreshSeconds -= (float)delta;
        if (_resourceMarkerRefreshSeconds <= 0)
        {
            _resourceMarkerRefreshSeconds = 0.2f;
            UpdateNaturalResourceMarkers();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F12)
            {
                CaptureScreenshot();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.F9)
            {
                OpenFeedbackPortal();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.F8 && _isAdministrator)
            {
                _showDetailedOverhead = !_showDetailedOverhead;
                ApplyOverheadLabelDetail();
                SetSaveStatus(
                    _showDetailedOverhead
                        ? "Administrator diagnostics enabled: detailed overhead statistics are visible."
                        : "Administrator diagnostics hidden: overhead labels show identity and occupation only.",
                    false);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.F10)
            {
                _mouseReleasedForSharing = !_mouseReleasedForSharing;
                ApplyOverlayPauseState();
                SetSaveStatus(
                    _mouseReleasedForSharing
                        ? "Mouse released. The realm is still running; press F10 to return control to the game."
                        : "Mouse returned to the game.",
                    false);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.Escape)
            {
                if (_worldOpen)
                {
                    SetWorldOpen(false);
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (_inventoryOpen)
                {
                    SetInventoryOpen(false);
                    GetViewport().SetInputAsHandled();
                    return;
                }
                SetMenuOpen(!_menuOpen);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.F5)
            {
                SaveRequested?.Invoke(PlayerPosition);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.I)
            {
                SetInventoryOpen(!_inventoryOpen);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (keyEvent.Keycode == Key.J)
            {
                SetWorldOpen(!_worldOpen);
                if (_worldOpen)
                {
                    WorldStateRequested?.Invoke();
                    DevelopmentStateRequested?.Invoke();
                    RaidStateRequested?.Invoke();
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode == Key.Tab)
            {
                CycleTarget(keyEvent.ShiftPressed);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode == Key.R)
            {
                InteractWithNearestResident();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode == Key.H)
            {
                HarvestNearestResource();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode == Key.B)
            {
                ContributeToNearestProject();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode == Key.U)
            {
                MovePlayerToSafeSpawn("Unstuck: returned safely to Stonehaven's north gate.");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_menuOpen && !_inventoryOpen && !_worldOpen && keyEvent.Keycode is Key.Q or Key.E)
            {
                UseHotkeySkill(keyEvent.Keycode == Key.Q ? "Q" : "E");
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            !_menuOpen &&
            !_inventoryOpen &&
            !_worldOpen &&
            !_mouseReleasedForSharing &&
            Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void CaptureScreenshot()
    {
        try
        {
            var pictures = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            var screenshotDirectory = string.IsNullOrWhiteSpace(pictures)
                ? ProjectSettings.GlobalizePath("user://Screenshots")
                : Path.Combine(pictures, "Living Realms");
            Directory.CreateDirectory(screenshotDirectory);

            var filename = $"LivingRealms_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
            var screenshotPath = Path.Combine(screenshotDirectory, filename);
            var image = GetViewport().GetTexture().GetImage();
            var result = image.SavePng(screenshotPath);
            if (result != Error.Ok)
            {
                SetSaveStatus($"Screenshot could not be saved ({result}).", true);
                return;
            }

            SetSaveStatus($"Screenshot saved to Pictures\\Living Realms: {filename}", false);
            GD.Print($"Living Realms screenshot saved to {screenshotPath}");
        }
        catch (Exception exception)
        {
            GD.PushError($"Living Realms screenshot failed: {exception.Message}");
            SetSaveStatus("Screenshot could not be saved. Check the game log for details.", true);
        }
    }

    private void OpenFeedbackPortal()
    {
        _mouseReleasedForSharing = true;
        ApplyOverlayPauseState();
        var result = OS.ShellOpen(FeedbackUrl);
        if (result == Error.Ok)
        {
            SetSaveStatus("Feedback page opened in your browser. Sign in with your player account to send a bug report or feature request.", false);
        }
        else
        {
            SetSaveStatus($"The feedback page could not be opened ({result}). Visit living-realms.com/feedback.php.", true);
        }
    }

    public void SetSaveStatus(string message, bool isError)
    {
        if (!IsInstanceValid(_saveStatus))
        {
            return;
        }

        _saveStatus.Text = message;
        _saveStatus.Modulate = isError ? new Color("f17a65") : new Color("e5bd62");
    }

    public void SetSaving(bool saving)
    {
        if (IsInstanceValid(_menuSaveButton)) _menuSaveButton.Disabled = saving;
        if (IsInstanceValid(_menuReturnButton)) _menuReturnButton.Disabled = saving;
        if (IsInstanceValid(_menuLogoutButton)) _menuLogoutButton.Disabled = saving;
        if (saving) SetSaveStatus("Saving your location...", false);
    }

    public void SetCombatStatus(string message, bool isError)
    {
        if (!IsInstanceValid(_combatStatus))
        {
            return;
        }

        _combatStatus.Text = message;
        _combatStatus.Modulate = isError ? new Color("f17a65") : new Color("efc866");
    }

    private void InteractWithNearestResident()
    {
        const float interactionRange = 3.6f;
        var resident = _residents.Values
            .Where(candidate => candidate.IsAvailable)
            .OrderBy(candidate => candidate.DistanceToPlayer())
            .FirstOrDefault();
        if (resident is null || resident.DistanceToPlayer() > interactionRange)
        {
            SetCombatStatus("Move closer to a Stonehaven resident before pressing R to talk.", true);
            return;
        }

        SetCombatStatus(resident.Interact(), false);
    }

    private void HarvestNearestResource()
    {
        const float harvestRange = 4.8f;
        var node = _resourceNodes.Values
            .Where(candidate => candidate.IsAvailable)
            .OrderBy(candidate => HorizontalDistance(candidate.GlobalPosition, PlayerPosition))
            .FirstOrDefault();
        var natural = _naturalResourceTargets
            .Where(candidate =>
                IsInstanceValid(candidate.Body) &&
                candidate.Body.IsInsideTree())
            .OrderBy(candidate => HorizontalDistance(candidate.Body.GlobalPosition, PlayerPosition))
            .FirstOrDefault();
        var nodeDistance = node is null
            ? float.MaxValue
            : HorizontalDistance(node.GlobalPosition, PlayerPosition);
        var naturalDistance = natural is null
            ? float.MaxValue
            : HorizontalDistance(natural.Body.GlobalPosition, PlayerPosition);
        if (Math.Min(nodeDistance, naturalDistance) > harvestRange)
        {
            SetCombatStatus("Move beside any tree or exposed stone and press H. Nearby resources show a gold marker.", true);
            return;
        }

        if (node is not null && nodeDistance <= naturalDistance)
        {
            node.PlayGatherImpact();
            ResourceHarvestRequested?.Invoke(node.ResourceId, PlayerPosition);
            SetCombatStatus(
                node.Kind.Equals("Wood", StringComparison.OrdinalIgnoreCase)
                    ? $"Chopping {node.ResourceName}..."
                    : $"Mining {node.ResourceName}...",
                false);
            return;
        }

        var selectedNatural = natural!;
        NaturalResourceHarvestRequested?.Invoke(
            selectedNatural.Kind,
            selectedNatural.Body.GlobalPosition,
            PlayerPosition);
        SetCombatStatus(
            selectedNatural.Kind.Equals("Wood", StringComparison.OrdinalIgnoreCase)
                ? $"Chopping {selectedNatural.Name}..."
                : $"Mining {selectedNatural.Name}...",
            false);
    }

    private void UpdateNaturalResourceMarkers()
    {
        _naturalResourceTargets.RemoveAll(target =>
            !IsInstanceValid(target.Body) ||
            !target.Body.IsInsideTree() ||
            !IsInstanceValid(target.Label) ||
            !target.Label.IsInsideTree());
        NaturalResourceTarget? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var target in _naturalResourceTargets)
        {
            var distance = HorizontalDistance(target.Body.GlobalPosition, PlayerPosition);
            target.Label.Visible = distance <= 11.0f;
            if (distance < nearestDistance)
            {
                nearest = target;
                nearestDistance = distance;
            }
        }
        if (nearest is not null && nearestDistance <= 11.0f)
        {
            nearest.Label.Modulate = nearestDistance <= 4.8f ? new Color("ffe08a") : new Color("d8a94b");
        }
    }

    private void MovePlayerToSafeSpawn(string message)
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }
        _player.GlobalPosition = _safeSpawn;
        _player.Velocity = Vector3.Zero;
        SetCombatStatus(message, false);
        SaveRequested?.Invoke(PlayerPosition);
    }

    private void ContributeToNearestProject()
    {
        const float contributionRange = 2.0f;
        var project = _constructionProjects.Values
            .Where(candidate => candidate.Owner.Equals("Stonehaven", StringComparison.OrdinalIgnoreCase) &&
                                candidate.CurrentLevel < candidate.MaximumLevel)
            .OrderBy(candidate => HorizontalDistance(ConstructionMarkerPosition(candidate), PlayerPosition))
            .FirstOrDefault();
        if (project is null || HorizontalDistance(ConstructionMarkerPosition(project), PlayerPosition) > contributionRange)
        {
            SetCombatStatus("Move beside a Stonehaven construction marker before pressing B.", true);
            return;
        }

        ProjectContributionRequested?.Invoke(project.Id, PlayerPosition);
        SetCombatStatus($"Delivering a resource bundle to {project.Name}...", false);
    }

    public void SetDevelopmentState(DevelopmentStateData state)
    {
        var received = new HashSet<Guid>();
        foreach (var data in state.Nodes)
        {
            received.Add(data.Id);
            if (_resourceNodes.TryGetValue(data.Id, out var existing))
            {
                existing.ApplyData(data);
                continue;
            }

            var node = new HarvestResourceNode { Name = $"Resource-{data.Key}" };
            node.Configure(data);
            _resourceNodes[data.Id] = node;
            AddWorldNode(node, data.Position);
        }
        foreach (var removed in _resourceNodes.Keys.Where(id => !received.Contains(id)).ToArray())
        {
            _resourceNodes[removed].QueueFree();
            _resourceNodes.Remove(removed);
        }

        _constructionProjects.Clear();
        foreach (var project in state.Projects)
        {
            _constructionProjects[project.Id] = project;
        }
        ApplyDestructibleStructures(state.Structures);
        if (IsInstanceValid(_developmentHudLabel))
        {
            var wall = state.Projects.FirstOrDefault(x =>
                x.Key.Equals("stonehaven-curtain-wall", StringComparison.OrdinalIgnoreCase));
            _developmentHudLabel.Text = wall is null
                ? $"STONEHAVEN STORES  •  WOOD {state.SettlementWood}  •  STONE {state.SettlementStone}"
                : $"STORES  WOOD {state.SettlementWood}  •  STONE {state.SettlementStone}     " +
                  $"WALL L{wall.CurrentLevel}/{wall.MaximumLevel}  {wall.Progress:P0}";
        }
        RebuildConstructionVisuals(state.Projects);
    }

    private void ApplyDestructibleStructures(IEnumerable<DestructibleStructureData> structures)
    {
        var incoming = structures.ToArray();
        var changed = incoming.Length != _destructibleStructures.Count ||
                      incoming.Any(candidate =>
                          !_destructibleStructures.TryGetValue(candidate.Key, out var existing) ||
                          existing.Health != candidate.Health ||
                          existing.MaximumHealth != candidate.MaximumHealth ||
                          existing.IsBuilt != candidate.IsBuilt ||
                          existing.BlocksMovement != candidate.BlocksMovement ||
                          !existing.Status.Equals(candidate.Status, StringComparison.OrdinalIgnoreCase));
        _destructibleStructures.Clear();
        foreach (var structure in incoming)
        {
            _destructibleStructures[structure.Key] = structure;
        }
        UpdateRaidStructureTargets();
        if (!changed)
        {
            return;
        }

        RebuildStructureStateVisuals();
        ApplyStructureNodeVisibility();
        if (_pathfinder is not null)
        {
            _pathfinder.SetPassableAreas(_destructibleStructures.Values
                .Where(x => x.IsBuilt && !x.BlocksMovement)
                .Select(StructurePassage));
        }
        if (IsInstanceValid(_constructionRoot) && _constructionProjects.Count > 0)
        {
            RebuildConstructionVisuals(_constructionProjects.Values);
        }
    }

    private void RebuildStructureStateVisuals()
    {
        if (!IsInstanceValid(_structureStateRoot))
        {
            return;
        }
        foreach (var child in _structureStateRoot.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var structure in _destructibleStructures.Values.Where(x => x.IsBuilt))
        {
            var ratio = Mathf.Clamp(
                (float)structure.Health / Math.Max(1, structure.MaximumHealth),
                0,
                1);
            var filled = Math.Clamp(Mathf.RoundToInt(ratio * 10), 0, 10);
            var bar = new string('█', filled) + new string('░', 10 - filled);
            var color = structure.Status switch
            {
                "Destroyed" => new Color("f0644c"),
                "Breached" or "Critical" => new Color("ff7b58"),
                "Damaged" => new Color("e9b54a"),
                _ => structure.Owner.Equals("Stonehaven", StringComparison.OrdinalIgnoreCase)
                    ? new Color("d8a94b")
                    : new Color("d35a45")
            };
            var indicator = new Label3D
            {
                Name = $"Structure-{structure.Key}",
                Text = $"{structure.Name.ToUpperInvariant()}  •  {structure.Status.ToUpperInvariant()}\n" +
                       $"{bar}  HP {structure.Health}/{structure.MaximumHealth}  •  ARMOR {structure.Armor}",
                Position = structure.Position + new Vector3(0, StructureLabelHeight(structure.Kind), 0),
                FontSize = 22,
                Modulate = color,
                OutlineSize = 7,
                OutlineModulate = new Color(0, 0, 0, 0.94f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = structure.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase)
                    ? 24.0f
                    : 42.0f
            };
            _structureStateRoot.AddChild(indicator);

            if (!structure.Status.Equals("Destroyed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            for (var rubble = 0; rubble < 5; rubble++)
            {
                var offset = new Vector3(
                    (rubble % 3 - 1) * 0.75f,
                    0.18f + (rubble % 2) * 0.14f,
                    (rubble / 3 - 0.5f) * 0.8f);
                var debris = CreateMesh(
                    $"Rubble-{structure.Key}-{rubble}",
                    new BoxMesh
                    {
                        Size = new Vector3(
                            0.7f + rubble % 2 * 0.25f,
                            0.35f + rubble % 3 * 0.12f,
                            0.65f + (rubble + 1) % 2 * 0.3f)
                    },
                    structure.Position + offset,
                    new Vector3(0.15f * rubble, 0.47f * rubble, 0.08f * rubble),
                    structure.Kind is "Wall" or "Gate"
                        ? new Color("514f4a")
                        : new Color("4f3424"));
                _structureStateRoot.AddChild(debris);
            }
        }
    }

    private void ApplyStructureNodeVisibility()
    {
        foreach (var structure in _destructibleStructures.Values.Where(x => x.IsBuilt))
        {
            var prefixes = StructureVisualPrefixes(structure.Key);
            if (prefixes.Length == 0)
            {
                continue;
            }
            var intact = structure.Health > 0;
            foreach (var node in EnumerateDescendants(this))
            {
                if (!prefixes.Any(prefix =>
                        node.Name.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (node is Node3D node3D)
                {
                    node3D.Visible = intact;
                }
                if (node is StaticBody3D body)
                {
                    body.CollisionLayer = intact ? 1u : 0u;
                    body.CollisionMask = intact ? 2u | 4u | 8u : 0u;
                }
                if (node is CollisionShape3D collision)
                {
                    collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, !intact);
                }
            }
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static string[] StructureVisualPrefixes(string key) => key switch
    {
        "stonehaven-gate" => ["GateLeft", "GateRight", "GateBeam", "GateRoof", "GateBanner", "GateCrest"],
        "stonehaven-blacksmith" => ["Blacksmith"],
        "stonehaven-inn" => ["WayfarerInn", "InnLabel"],
        "stonehaven-herbalist" => ["Herbalist"],
        "stonehaven-storehouse" => ["Storehouse"],
        "stonehaven-west-farmhouse" => ["WestFarmhouse"],
        "stonehaven-east-farmhouse" => ["EastFarmhouse"],
        "stonehaven-farm-1" => ["FarmPlot1"],
        "stonehaven-farm-2" => ["FarmPlot2"],
        "stonehaven-farm-3" => ["FarmPlot3"],
        "stonehaven-farm-4" => ["FarmPlot4"],
        "stonehaven-farm-5" => ["FarmPlot5"],
        "stonehaven-farm-6" => ["FarmPlot6"],
        "stonehaven-farm-7" => ["FarmPlot7"],
        "stonehaven-farm-8" => ["FarmPlot8"],
        "irondeep-mine" => ["IrondeepMine", "IrondeepRock", "IrondeepOreCart"],
        "mirrorwater-dock" => ["MirrorwaterDock"],
        _ => []
    };

    private static float StructureLabelHeight(string kind) => kind switch
    {
        "Wall" or "Gate" => 4.5f,
        "Building" or "Stockpile" => 5.2f,
        "Mine" => 7.0f,
        "Dock" or "Farm" => 2.6f,
        _ => 3.5f
    };

    private static WorldPathObstacle StructurePassage(DestructibleStructureData structure)
    {
        var size = structure.Kind switch
        {
            "Wall" when structure.Key.Contains("north", StringComparison.OrdinalIgnoreCase) =>
                new Vector3(24, 5, 3.5f),
            "Wall" when structure.Key.Contains("south", StringComparison.OrdinalIgnoreCase) =>
                new Vector3(58, 5, 3.5f),
            "Wall" => new Vector3(3.5f, 5, 40),
            "Gate" => new Vector3(12, 7, 8),
            "Dock" => new Vector3(32, 3, 8),
            "Farm" => new Vector3(18, 2, 19),
            "Mine" => new Vector3(18, 8, 12),
            _ => new Vector3(9, 6, 8)
        };
        return WorldPathObstacle.FromBox(structure.Position, size);
    }

    private static string FormatStructureSummary(IEnumerable<DestructibleStructureData> structures)
    {
        static string OwnerSummary(
            string owner,
            IEnumerable<DestructibleStructureData> all)
        {
            var built = all.Where(x =>
                    x.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && x.IsBuilt)
                .ToArray();
            var totalHealth = built.Sum(x => x.Health);
            var totalMaximum = built.Sum(x => x.MaximumHealth);
            var damaged = built
                .Where(x => !x.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{x.Name} {x.Status.ToUpperInvariant()} {x.Health}/{x.MaximumHealth}")
                .ToArray();
            return $"{owner.ToUpperInvariant()}  {totalHealth}/{totalMaximum} HP across {built.Length} built assets" +
                   (damaged.Length == 0 ? "  •  all healthy" : $"\n  {string.Join("  •  ", damaged)}");
        }

        var allStructures = structures.ToArray();
        return $"{OwnerSummary("Stonehaven", allStructures)}\n{OwnerSummary("Darkwood", allStructures)}";
    }

    private void OnResourceWorkPulse(string workerKey)
    {
        var normalized = workerKey.Trim().ToLowerInvariant();
        var owner = normalized is "skrit" or "vrak" ? "Darkwood" : "Stonehaven";
        var kind = normalized is "nessa" or "skrit" ? "Wood" : "Stone";
        var node = _resourceNodes.Values
            .Where(candidate => IsInstanceValid(candidate) &&
                                candidate.IsInsideTree() &&
                                !candidate.IsQueuedForDeletion() &&
                                candidate.IsAvailable &&
                                candidate.ResourceOwnerName.Equals(owner, StringComparison.OrdinalIgnoreCase) &&
                                candidate.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => HorizontalDistance(candidate.GlobalPosition,
                normalized is "nessa" or "dain"
                    ? _residents.Values.FirstOrDefault(x => x.ResidentName.Equals(normalized, StringComparison.OrdinalIgnoreCase))?.GlobalPosition ?? candidate.GlobalPosition
                    : _creatures.Values.FirstOrDefault(x => x.CreatureName.Equals(normalized, StringComparison.OrdinalIgnoreCase))?.GlobalPosition ?? candidate.GlobalPosition))
            .FirstOrDefault();
        if (node is null)
        {
            return;
        }
        node.PlayGatherImpact();
        NpcWorkRequested?.Invoke(normalized, node.ResourceId);
    }

    private void RebuildConstructionVisuals(IEnumerable<ConstructionProjectData> projects)
    {
        if (!IsInstanceValid(_constructionRoot))
        {
            return;
        }
        foreach (var child in _constructionRoot.GetChildren())
        {
            child.QueueFree();
        }
        _constructionPathObstacles.Clear();

        foreach (var project in projects)
        {
            BuildConstructionMarker(project);
            if (project.Key.Equals("stonehaven-curtain-wall", StringComparison.OrdinalIgnoreCase))
            {
                BuildStonehavenConstructionWall(project);
            }
            else if (project.Key.Equals("darkwood-perimeter-palisade", StringComparison.OrdinalIgnoreCase))
            {
                BuildDarkwoodConstructionPalisade(project);
            }
            else
            {
                BuildConstructionBuilding(project);
            }
        }
        _pathfinder?.SetDynamicObstacles(_constructionPathObstacles);
    }

    private void BuildConstructionMarker(ConstructionProjectData project)
    {
        var markerPosition = ConstructionMarkerPosition(project);
        AddConstructionMesh(
            $"{project.Key}-MarkerPost",
            new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.12f, Height = 2.1f, RadialSegments = 8 },
            markerPosition + new Vector3(0, 1.05f, 0),
            Vector3.Zero,
            new Color("5a351d"));
        var levelText = project.CurrentLevel >= project.MaximumLevel
            ? $"LEVEL {project.MaximumLevel}  •  COMPLETE"
            : $"LEVEL {project.CurrentLevel}/{project.MaximumLevel}  •  {project.Stage.ToUpperInvariant()}";
        var action = project.Owner.Equals("Stonehaven", StringComparison.OrdinalIgnoreCase) &&
                     project.CurrentLevel < project.MaximumLevel
            ? "\nB  DEPOSIT CARRIED MATERIALS"
            : string.Empty;
        var builders = project.Key.Equals("stonehaven-curtain-wall", StringComparison.OrdinalIgnoreCase)
            ? "\nBUILD CREW  STONEHAVEN MASONS & CARPENTERS"
            : project.Key.Equals("stonehaven-lumber-yard", StringComparison.OrdinalIgnoreCase)
                ? "\nFOREWOMAN  NESSA  •  TIMBER CREW"
                : project.Key.Equals("stonehaven-quarry-works", StringComparison.OrdinalIgnoreCase)
                    ? "\nFOREMAN  DAIN  •  STONE CREW"
            : project.Key.Equals("darkwood-perimeter-palisade", StringComparison.OrdinalIgnoreCase)
                ? "\nBUILDERS  SKRIT (TIMBER)  •  VRAK (STONE)"
                : string.Empty;
        var label = new Label3D
        {
            Name = $"{project.Key}-MarkerLabel",
            Text = $"{project.Name.ToUpperInvariant()}\n{levelText}  •  {project.Progress:P0}\n" +
                   $"WOOD {project.WoodContributed}/{project.WoodRequired}  •  " +
                   $"STONE {project.StoneContributed}/{project.StoneRequired}{builders}{action}",
            Position = markerPosition + new Vector3(0, 3.0f, 0),
            FontSize = 23,
            Modulate = project.Owner.Equals("Stonehaven", StringComparison.OrdinalIgnoreCase) ? Gold : new Color("d35a45"),
            OutlineSize = 7,
            OutlineModulate = new Color(0, 0, 0, 0.92f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        _constructionRoot.AddChild(label);
    }

    private static Vector3 ConstructionMarkerPosition(ConstructionProjectData project)
    {
        if (project.Key.Equals("stonehaven-curtain-wall", StringComparison.OrdinalIgnoreCase) ||
            project.Key.Equals("darkwood-perimeter-palisade", StringComparison.OrdinalIgnoreCase))
        {
            return project.Position;
        }
        return project.Position + new Vector3(0, 0, 4.2f);
    }

    private void BuildStonehavenConstructionWall(ConstructionProjectData project)
    {
        var segments = new List<(Vector3 Position, Vector3 Size)>();
        // The north entrance remains the main gate. The east gate keeps the
        // lake and Irondeep road usable after the curtain wall is complete.
        AddWallRun(segments, new Vector3(-29.0f, 0, 3.5f), new Vector3(-6.10f, 0, 3.5f));
        AddWallRun(segments, new Vector3(6.10f, 0, 3.5f), new Vector3(29.0f, 0, 3.5f));
        AddWallRun(segments, new Vector3(-29.0f, 0, 3.5f), new Vector3(-29.0f, 0, -36.0f));
        AddWallRun(segments, new Vector3(29.0f, 0, 3.5f), new Vector3(29.0f, 0, -3.5f));
        AddWallRun(segments, new Vector3(29.0f, 0, -10.5f), new Vector3(29.0f, 0, -36.0f));
        AddWallRun(segments, new Vector3(-29.0f, 0, -36.0f), new Vector3(29.0f, 0, -36.0f));

        var built = Math.Clamp((int)MathF.Floor(project.Progress * segments.Count + 0.001f), 0, segments.Count);
        var height = 1.65f + project.CurrentLevel * 0.48f;
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var structureKey = ResolveStonehavenWallStructureKey(segment.Position);
            AddConstructionMesh(
                $"StonehavenFoundation{index}",
                new BoxMesh { Size = new Vector3(segment.Size.X + 0.10f, 0.28f, segment.Size.Z + 0.10f) },
                segment.Position + new Vector3(0, 0.14f, 0),
                Vector3.Zero,
                new Color("4a4945"));
            if (index < built && IsStructureBlocking(structureKey))
            {
                AddStoneConstructionWall(
                    $"StonehavenBuiltWall{index}",
                    segment.Position + new Vector3(0, 0.26f + height * 0.5f, 0),
                    new Vector3(segment.Size.X, height, segment.Size.Z),
                    project.CurrentLevel);
                AddConstructionMesh(
                    $"StonehavenWallCap{index}",
                    new BoxMesh { Size = new Vector3(segment.Size.X + 0.10f, 0.20f, segment.Size.Z + 0.10f) },
                    segment.Position + new Vector3(0, 0.26f + height + 0.10f, 0),
                    Vector3.Zero,
                    project.CurrentLevel >= 2 ? new Color("81837f") : new Color("747671"));
            }
            else if (index == built && built < segments.Count)
            {
                AddWallScaffold(segment.Position, segment.Size);
            }
        }

        foreach (var corner in new[]
                 {
                     new Vector3(-29.0f, 0, 3.5f),
                     new Vector3(29.0f, 0, 3.5f),
                     new Vector3(-29.0f, 0, -36.0f),
                     new Vector3(29.0f, 0, -36.0f)
                 })
        {
            var reachesCorner = segments
                .Take(built)
                .Any(segment => new Vector2(
                    segment.Position.X - corner.X,
                    segment.Position.Z - corner.Z).Length() <= 3.5f);
            if (!reachesCorner || !IsStructureBlocking(ResolveStonehavenWallStructureKey(corner)))
            {
                continue;
            }
            AddStoneConstructionWall(
                $"StonehavenCornerPier{corner.X}{corner.Z}",
                corner + new Vector3(0, 0.26f + height * 0.5f, 0),
                new Vector3(1.65f, height + 0.18f, 1.65f),
                project.CurrentLevel);
        }
    }

    private void AddStoneConstructionWall(string name, Vector3 position, Vector3 size, int level)
    {
        EnsurePlayerOutsideConstructionBox(position, size, Vector3.Zero);
        var horizontal = size.X >= size.Z;
        var length = horizontal ? size.X : size.Z;
        var thickness = horizontal ? size.Z : size.X;
        var yaw = horizontal ? 0.0f : Mathf.Pi * 0.5f;
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        var wall = InstantiateProductionEnvironmentAsset(StonehavenStoneWallScenePath, name + "Stonework");
        if (wall is not null)
        {
            wall.Rotation = new Vector3(0, yaw, 0);
            wall.Scale = new Vector3(length / 4.0f, size.Y / 2.5f, thickness / 0.85f);
            GroundProductionAsset(wall, -size.Y * 0.5f);
            body.AddChild(wall);
        }
        else
        {
            var color = level >= 2 ? new Color("686b68") : new Color("74746f");
            body.AddChild(CreateConstructionMesh(
                name + "Fallback",
                new BoxMesh { Size = size },
                Vector3.Zero,
                Vector3.Zero,
                color));
        }
        _constructionRoot.AddChild(body);
        _constructionPathObstacles.Add(WorldPathObstacle.FromRotatedBox(position, size, 0));
    }

    private static void AddWallRun(
        List<(Vector3 Position, Vector3 Size)> segments,
        Vector3 start,
        Vector3 end)
    {
        const float targetSectionLength = 5.6f;
        const float wallThickness = 0.82f;
        const float seamOverlap = 0.08f;
        var delta = end - start;
        var runLength = new Vector2(delta.X, delta.Z).Length();
        var sectionCount = Math.Max(1, (int)MathF.Ceiling(runLength / targetSectionLength));
        for (var section = 0; section < sectionCount; section++)
        {
            var from = start.Lerp(end, (float)section / sectionCount);
            var to = start.Lerp(end, (float)(section + 1) / sectionCount);
            var sectionLength = new Vector2(to.X - from.X, to.Z - from.Z).Length() + seamOverlap;
            var horizontal = MathF.Abs(delta.X) >= MathF.Abs(delta.Z);
            segments.Add(((from + to) * 0.5f,
                horizontal
                    ? new Vector3(sectionLength, 1, wallThickness)
                    : new Vector3(wallThickness, 1, sectionLength)));
        }
    }

    private void BuildDarkwoodConstructionPalisade(ConstructionProjectData project)
    {
        const int segmentCount = 32;
        const int gateLeft = 7;
        const int gateRight = 8;
        var eligibleSegments = Enumerable.Range(0, segmentCount)
            .Where(index => index is not gateLeft and not gateRight)
            .ToArray();
        var buildOrder = new List<int>(eligibleSegments.Length);
        for (var step = 1; buildOrder.Count < eligibleSegments.Length; step++)
        {
            var clockwise = (gateRight + step) % segmentCount;
            var counterClockwise = (gateLeft - step + segmentCount) % segmentCount;
            if (clockwise is not gateLeft and not gateRight && !buildOrder.Contains(clockwise)) buildOrder.Add(clockwise);
            if (counterClockwise is not gateLeft and not gateRight && !buildOrder.Contains(counterClockwise)) buildOrder.Add(counterClockwise);
        }
        var builtCount = Math.Clamp((int)MathF.Floor(project.Progress * eligibleSegments.Length + 0.001f), 0, eligibleSegments.Length);
        var builtSegments = buildOrder.Take(builtCount).ToHashSet();
        var height = 2.1f + project.CurrentLevel * 0.42f;
        foreach (var index in eligibleSegments)
        {
            var angle = Mathf.Tau * index / segmentCount;
            var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var position = DarkwoodCampCenter + radial * 17.0f;
            var rotation = new Vector3(0, -angle - Mathf.Pi * 0.5f, 0);
            var structureKey = ResolveDarkwoodPalisadeStructureKey(radial);
            AddConstructionMesh(
                $"DarkwoodStakeLine{index}",
                new BoxMesh { Size = new Vector3(3.25f, 0.12f, 0.45f) },
                position + new Vector3(0, 0.06f, 0),
                rotation,
                new Color("3b2718"));
            if (builtSegments.Contains(index) && IsStructureBlocking(structureKey))
            {
                AddConstructionCollisionBox(
                    $"DarkwoodBuiltPalisade{index}",
                    position + new Vector3(0, height * 0.5f, 0),
                    new Vector3(3.35f, height, 0.7f),
                    rotation);
                for (var stake = -2; stake <= 2; stake++)
                {
                    var stakeHeight = height + ((index + stake + 8) % 3 - 1) * 0.14f;
                    AddConstructionMesh(
                        $"DarkwoodPalisadeStake{index}_{stake}",
                        new CylinderMesh
                        {
                            TopRadius = 0.13f,
                            BottomRadius = 0.3f,
                            Height = stakeHeight,
                            RadialSegments = 7
                        },
                        position + tangent * (stake * 0.66f) + new Vector3(0, stakeHeight * 0.5f, 0),
                        Vector3.Zero,
                        stake % 2 == 0 ? new Color("59371f") : new Color("452a18"));
                }
            }
        }
    }

    private void BuildConstructionBuilding(ConstructionProjectData project)
    {
        var structureKey = project.Key switch
        {
            "stonehaven-lumber-yard" => "stonehaven-lumber-yard",
            "stonehaven-quarry-works" => "stonehaven-quarry-works",
            "darkwood-supply-hut" => "darkwood-supply-hut",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(structureKey) && !IsStructureBlocking(structureKey))
        {
            return;
        }
        var darkwood = project.Owner.Equals("Darkwood", StringComparison.OrdinalIgnoreCase);
        var timber = darkwood ? new Color("4b2d1b") : new Color("72502f");
        var stone = darkwood ? new Color("403b37") : new Color("696a65");
        var width = project.Key.Contains("quarry", StringComparison.OrdinalIgnoreCase) ? 6.5f : 7.5f;
        var depth = 5.0f;
        var buildingPosition = project.Position;
        ClearStylizedEnvironmentFootprint(
            buildingPosition,
            new Vector2(width * 0.5f + 1.25f, depth * 0.5f + 1.25f));
        AddConstructionMesh(project.Key + "-Pad", new BoxMesh { Size = new Vector3(width, 0.22f, depth) },
            buildingPosition + new Vector3(0, 0.11f, 0), Vector3.Zero, stone);
        if (project.Progress > 0.08f)
        {
            foreach (var x in new[] { -width * 0.42f, width * 0.42f })
            foreach (var z in new[] { -depth * 0.40f, depth * 0.40f })
            {
                AddConstructionMesh(project.Key + "-Frame", new BoxMesh { Size = new Vector3(0.28f, 3.4f, 0.28f) },
                    buildingPosition + new Vector3(x, 1.7f, z), Vector3.Zero, timber);
            }
        }
        if (project.Progress > 0.35f)
        {
            var wallColor = darkwood ? new Color("55402c") : new Color("756044");
            const float wallThickness = 0.28f;
            const float doorwayWidth = 1.8f;
            var frontSectionWidth = (width - doorwayWidth) * 0.5f;
            AddConstructionBox(project.Key + "-BackWall", buildingPosition + new Vector3(0, 1.25f, -depth * 0.5f),
                new Vector3(width, 2.5f, wallThickness), wallColor);
            AddConstructionBox(project.Key + "-LeftWall", buildingPosition + new Vector3(-width * 0.5f, 1.25f, 0),
                new Vector3(wallThickness, 2.5f, depth), wallColor);
            AddConstructionBox(project.Key + "-RightWall", buildingPosition + new Vector3(width * 0.5f, 1.25f, 0),
                new Vector3(wallThickness, 2.5f, depth), wallColor);
            AddConstructionBox(project.Key + "-FrontLeft", buildingPosition + new Vector3(-(doorwayWidth + frontSectionWidth) * 0.5f, 1.25f, depth * 0.5f),
                new Vector3(frontSectionWidth, 2.5f, wallThickness), wallColor);
            AddConstructionBox(project.Key + "-FrontRight", buildingPosition + new Vector3((doorwayWidth + frontSectionWidth) * 0.5f, 1.25f, depth * 0.5f),
                new Vector3(frontSectionWidth, 2.5f, wallThickness), wallColor);
        }
        if (project.Progress > 0.68f)
        {
            AddConstructionMesh(project.Key + "-Roof", new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = width * 0.66f,
                Height = 2.2f,
                RadialSegments = 4
            }, buildingPosition + new Vector3(0, 3.6f, 0), new Vector3(0, Mathf.DegToRad(45), 0),
                darkwood ? new Color("391713") : new Color("593124"));
        }
    }

    private bool IsStructureBlocking(string key) =>
        !_destructibleStructures.TryGetValue(key, out var structure) ||
        !structure.IsBuilt ||
        structure.BlocksMovement;

    private static string ResolveStonehavenWallStructureKey(Vector3 position)
    {
        if (MathF.Abs(position.Z - 3.5f) < 1.0f)
        {
            return position.X < 0
                ? "stonehaven-wall-northwest"
                : "stonehaven-wall-northeast";
        }
        if (MathF.Abs(position.Z + 36.0f) < 1.0f)
        {
            return "stonehaven-wall-south";
        }
        return position.X < 0
            ? "stonehaven-wall-west"
            : "stonehaven-wall-east";
    }

    private static string ResolveDarkwoodPalisadeStructureKey(Vector3 radial)
    {
        if (MathF.Abs(radial.X) >= MathF.Abs(radial.Z))
        {
            return radial.X >= 0
                ? "darkwood-palisade-east"
                : "darkwood-palisade-west";
        }
        return radial.Z >= 0
            ? "darkwood-palisade-south"
            : "darkwood-palisade-north";
    }

    private void AddWallScaffold(Vector3 position, Vector3 size)
    {
        var wood = new Color("8a673b");
        var xExtent = MathF.Max(0.5f, size.X * 0.45f);
        var zExtent = MathF.Max(0.5f, size.Z * 0.45f);
        AddConstructionMesh("ScaffoldPost", new BoxMesh { Size = new Vector3(0.18f, 2.3f, 0.18f) },
            position + new Vector3(-xExtent, 1.15f, -zExtent), Vector3.Zero, wood);
        AddConstructionMesh("ScaffoldPost", new BoxMesh { Size = new Vector3(0.18f, 2.3f, 0.18f) },
            position + new Vector3(xExtent, 1.15f, zExtent), Vector3.Zero, wood);
        AddConstructionMesh("ScaffoldBeam", new BoxMesh { Size = new Vector3(size.X, 0.16f, size.Z) },
            position + new Vector3(0, 1.75f, 0), Vector3.Zero, wood);
    }

    private void AddConstructionBox(
        string name,
        Vector3 position,
        Vector3 size,
        Color color,
        Vector3 rotation = default)
    {
        EnsurePlayerOutsideConstructionBox(position, size, rotation);
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(CreateConstructionMesh(name + "Mesh", new BoxMesh { Size = size }, Vector3.Zero, Vector3.Zero, color));
        _constructionRoot.AddChild(body);
        _constructionPathObstacles.Add(WorldPathObstacle.FromRotatedBox(position, size, rotation.Y));
    }

    private void AddConstructionCollisionBox(
        string name,
        Vector3 position,
        Vector3 size,
        Vector3 rotation)
    {
        EnsurePlayerOutsideConstructionBox(position, size, rotation);
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        _constructionRoot.AddChild(body);
        _constructionPathObstacles.Add(WorldPathObstacle.FromRotatedBox(position, size, rotation.Y));
    }

    private void EnsurePlayerOutsideConstructionBox(Vector3 position, Vector3 size, Vector3 rotation)
    {
        if (!PlayerOverlapsConstructionBox(position, size, rotation))
        {
            return;
        }

        var offset = PlayerPosition - position;
        var cosine = MathF.Cos(-rotation.Y);
        var sine = MathF.Sin(-rotation.Y);
        var localX = offset.X * cosine - offset.Z * sine;
        var localZ = offset.X * sine + offset.Z * cosine;
        var safeHalfX = size.X * 0.5f + 1.0f;
        var safeHalfZ = size.Z * 0.5f + 1.0f;
        if (safeHalfX - MathF.Abs(localX) < safeHalfZ - MathF.Abs(localZ))
        {
            localX = (localX < 0 ? -1 : 1) * safeHalfX;
        }
        else
        {
            localZ = (localZ < 0 ? -1 : 1) * safeHalfZ;
        }

        cosine = MathF.Cos(rotation.Y);
        sine = MathF.Sin(rotation.Y);
        var worldX = localX * cosine - localZ * sine;
        var worldZ = localX * sine + localZ * cosine;
        _player.GlobalPosition = new Vector3(position.X + worldX, MathF.Max(PlayerPosition.Y, 0.08f), position.Z + worldZ);
    }

    private bool PlayerOverlapsConstructionBox(Vector3 position, Vector3 size, Vector3 rotation)
    {
        if (!IsInstanceValid(_player))
        {
            return false;
        }
        var offset = PlayerPosition - position;
        var cosine = MathF.Cos(-rotation.Y);
        var sine = MathF.Sin(-rotation.Y);
        var localX = offset.X * cosine - offset.Z * sine;
        var localZ = offset.X * sine + offset.Z * cosine;
        return MathF.Abs(localX) <= size.X * 0.5f + 0.75f &&
               MathF.Abs(localZ) <= size.Z * 0.5f + 0.75f &&
               MathF.Abs(offset.Y) <= size.Y * 0.5f + 1.2f;
    }

    private void AddConstructionMesh(
        string name,
        PrimitiveMesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color)
    {
        _constructionRoot.AddChild(CreateConstructionMesh(name, mesh, position, rotation, color));
    }

    private static MeshInstance3D CreateConstructionMesh(
        string name,
        PrimitiveMesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color)
    {
        mesh.Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.88f };
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
    }

    public void SetInventory(WorldInventoryData inventory)
    {
        var timber = inventory.Items.FirstOrDefault(item =>
            item.Key.Equals("raw-timber", StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        var stone = inventory.Items.FirstOrDefault(item =>
            item.Key.Equals("rough-stone", StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        var iron = inventory.Items.FirstOrDefault(item =>
            item.Key.Equals("raw-iron-ore", StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        if (IsInstanceValid(_carriedResourcesHudLabel))
        {
            _carriedResourcesHudLabel.Text =
                $"PACK {inventory.UsedCapacity}/{inventory.CarryCapacity}  •  WOOD {timber}  •  STONE {stone}  •  IRON {iron}  •  GOLD {inventory.Gold}";
            _carriedResourcesHudLabel.Modulate = inventory.UsedCapacity >= inventory.CarryCapacity
                ? new Color("f0644c")
                : new Color("e5bd62");
        }
        if (!IsInstanceValid(_inventoryRows))
        {
            return;
        }

        _inventoryStats.Text =
            $"ATTACK {inventory.Attack}   •   DEFENSE {inventory.Defense}   •   " +
            $"PACK {inventory.UsedCapacity}/{inventory.CarryCapacity}   •   GOLD {inventory.Gold}";
        foreach (var child in _inventoryRows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var item in inventory.Items)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 54) };
            row.AddThemeConstantOverride("separation", 10);
            _inventoryRows.AddChild(row);
            var equipped = item.IsEquipped ? "  [EQUIPPED]" : string.Empty;
            var bonuses = string.Join("  ", new[]
            {
                item.AttackBonus > 0 ? $"+{item.AttackBonus} ATK" : string.Empty,
                item.DefenseBonus > 0 ? $"+{item.DefenseBonus} DEF" : string.Empty,
                item.HealingAmount > 0 ? $"HEAL {item.HealingAmount}" : string.Empty
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var demand = item.Key switch
            {
                "raw-timber" or "rough-stone" => "Needed by construction projects • Oren buys surplus",
                "raw-iron-ore" => "Brann needs local ore • Oren buys surplus",
                _ => $"Buyer: {item.BuyerName}"
            };
            var label = CreateLabel(
                $"{item.Name} x{item.Quantity}{equipped}   •   WEIGHT {item.TotalWeight}\n" +
                $"{item.Rarity.ToUpperInvariant()} {item.Kind.ToUpperInvariant()}  {bonuses}   •   {demand}",
                13,
                RarityColor(item.Rarity));
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);

            string? action = item.HealingAmount > 0
                ? "use"
                : item.EquipmentSlot is not null
                    ? item.IsEquipped ? "unequip" : "equip"
                    : null;
            if (action is not null)
            {
                var actionButton = CreateButton(action.ToUpperInvariant());
                actionButton.CustomMinimumSize = new Vector2(110, 42);
                var entryId = item.Id;
                var requestedAction = action;
                actionButton.Pressed += () => InventoryActionRequested?.Invoke(entryId, requestedAction, PlayerPosition);
                row.AddChild(actionButton);
            }
            if (!item.IsEquipped)
            {
                var sellButton = CreateButton("SELL 1");
                sellButton.TooltipText = $"Sell one to {item.BuyerName}. You must be standing beside that buyer.";
                sellButton.CustomMinimumSize = new Vector2(88, 42);
                var entryId = item.Id;
                sellButton.Pressed += () => InventoryActionRequested?.Invoke(entryId, "sell", PlayerPosition);
                row.AddChild(sellButton);
            }
        }
    }

    public void SetSkills(IEnumerable<WorldSkillData> skills)
    {
        _skills.Clear();
        foreach (var skill in skills)
        {
            _skills[skill.Hotkey] = skill;
        }
        _skillLabel.Text = _skills.Count == 0
            ? "Q / E  Skills loading..."
            : string.Join("     ", _skills.Values.OrderBy(x => x.Hotkey.Equals("Q", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .Select(x => $"{x.Hotkey}  {x.Name}  ({x.CooldownSeconds:0}s)"));
    }

    public void SetWorldState(WorldStateData state)
    {
        if (!IsInstanceValid(_worldHudLabel))
        {
            return;
        }

        var faction = state.Faction;
        ApplyDestructibleStructures(state.Structures);
        _worldHudLabel.Text = $"WORLD DAY {state.WorldDay}  •  DARKWOOD: {faction.StageName.ToUpperInvariant()}";
        _worldSummary.Text =
            $"WORLD DAY {state.WorldDay}   •   {state.SimulationSpeed.ToUpperInvariant()}\n" +
            $"DARKWOOD {faction.Population} GOBLINS   •   STONEHAVEN {state.Settlement.LivingResidents} RESIDENTS";
        var resources = string.Join("     ", faction.Resources.Select(resource =>
            $"{resource.Kind.ToUpperInvariant()} {resource.Amount}/{resource.Capacity}"));
        var structures = faction.Structures.Count == 0
            ? "No permanent structures"
            : string.Join("   •   ", faction.Structures.Select(structure =>
                $"{structure.Name} (L{structure.Level}): {StructurePurpose(structure.Name)}"));
        var threat = faction.Leader.Level >= 20
            ? "EXTREME"
            : faction.Leader.Level >= 15
                ? "SEVERE"
                : faction.Leader.Level >= 10 ? "HIGH" : "RISING";
        var darkwoodRaid = state.EventReadiness.DarkwoodRaid;
        var counterattack = state.EventReadiness.StonehavenCounterattack;
        var structureSummary = FormatStructureSummary(state.Structures);
        var stonehavenFood = FormatFoodEconomy("STONEHAVEN", state.Survival.Stonehaven);
        var darkwoodFood = FormatFoodEconomy("DARKWOOD", state.Survival.Darkwood);
        var ironEconomy = FormatIronEconomy(state.IronEconomy);
        var factionBanks = FormatFactionBanks(state.Banks);
        var recovery = FormatSettlementRecovery(state.Recovery);
        var recoveryAlert = RecoveryAlert(state.Recovery);
        if (!string.IsNullOrWhiteSpace(recoveryAlert))
        {
            _worldHudLabel.Text += $"  â€¢  {recoveryAlert}";
        }
        var adminJobs = state.CanAccelerate
            ? $"\n\nADMIN SIMULATION JOBS\n{state.Events.Pending} pending   •   {state.Events.Completed} completed   •   {state.Events.Failed} failed"
            : string.Empty;
        _worldDetails.Text =
            $"DARKWOOD CLAN — GOBLIN FACTION\n" +
            $"Leader: {faction.Leader.Name}, {faction.Leader.Title}   •   level {faction.Leader.Level}   •   leadership {faction.Leader.Leadership}\n" +
            $"Current leader record: {faction.Leader.Status.ToUpperInvariant()}   •   health {faction.Leader.Health}/{faction.Leader.MaximumHealth}   •   attack {faction.Leader.Attack}   •   defense {faction.Leader.Defense}   •   threat {threat}\n" +
            $"Clan: {faction.Population} living goblins / {faction.PopulationCapacity} capacity   •   military power {faction.MilitaryStrength}   •   morale {faction.Morale}   •   aggression {faction.Aggression}\n" +
            $"Darkwood resources: {resources}\n" +
            $"Darkwood structures: {structures}\n\n" +
            $"STONEHAVEN — HUMAN SETTLEMENT\n" +
            $"Leader: {state.Settlement.Leader.Name}, {state.Settlement.Leader.Title} ({state.Settlement.Leader.Role})   •   health {state.Settlement.Leader.Health}/{state.Settlement.Leader.MaximumHealth}   •   {state.Settlement.Leader.Status}\n" +
            $"LEADERSHIP PROFILE   {state.Settlement.Leader.PrimarySkill} L{state.Settlement.Leader.SkillLevel}   •   {state.Settlement.Leader.Trait}   •   {(state.Settlement.Leader.IsMajor ? "MAJOR RESIDENT" : "RESIDENT")}\n" +
            $"MEMORY   {state.Settlement.Leader.MemorySummary}\n" +
            $"Population: {state.Settlement.LivingResidents}/{state.Settlement.HousingCapacity} housed named residents   •   combat-ready {state.Settlement.CombatReadyResidents}   •   defense rating {state.Settlement.DefenseRating}   •   military power {state.Settlement.GuardStrength}\n" +
            $"Village supplies: food {state.Settlement.Food}   •   wood {state.Settlement.Wood}   •   stone {state.Settlement.Stone}   •   iron {state.Settlement.Iron}\n" +
            $"\nSURVIVAL & WORKERS\n{stonehavenFood}\n{darkwoodFood}\n" +
            $"Huntable wildlife: {state.Survival.Wildlife.Available}/{state.Survival.Wildlife.Total} active   •   {state.Survival.Wildlife.Respawning} respawning\n\n" +
            $"IRONDEEP & EQUIPMENT\n{ironEconomy}\n\n" +
            $"FACTION BANKS\n{factionBanks}\n\n" +
            $"DESTRUCTION & RECOVERY\n{recovery}\n\n" +
            $"Destructible settlement assets:\n{structureSummary}\n" +
            $"Growth: at most one new resident per world day, only when housing and the required food, timber, and stone are available.\n\n" +
            "CAMPAIGN READINESS\nBattles become ready from living-world conditions, but only an online administrator can authorize their start." +
            adminJobs;
        SetReadinessLabel(_darkwoodReadiness, darkwoodRaid);
        SetReadinessLabel(_counterattackReadiness, counterattack);
        _chronicleText.Text = state.RecentHistory.Count == 0
            ? "No chronicles have been recorded yet."
            : string.Join("\n\n", state.RecentHistory.Take(6).Select(entry =>
                $"{entry.OccurredAtCentral:MMM d, h:mm tt} CST  •  {entry.Title.ToUpperInvariant()}\n{entry.Description}"));
        _advanceWorldButton.Visible = state.CanAccelerate;
        _advanceWorldButton.Disabled = false;
        _resetWorldButton.Visible = state.CanAccelerate;
        _resetWorldButton.Disabled = false;
        _refreshWorldButton.Visible = state.CanAccelerate;
        _refreshWorldButton.Disabled = false;
        _resetWorldButton.Text = "Reset Living World  [Playtest]";
        _resetConfirmationSeconds = 0;
        ApplyDarkwoodCampStage(faction.DevelopmentStage);
    }

    private static string FormatFoodEconomy(string owner, WorldFoodEconomyData economy)
    {
        var net = economy.NetFoodPerHour >= 0
            ? $"+{economy.NetFoodPerHour}"
            : economy.NetFoodPerHour.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var recruitment = economy.RecommendedRecruitmentRole == "None"
            ? "food workforce currently sustainable"
            : $"next needed worker: {economy.RecommendedRecruitmentRole}";
        return
            $"{owner}: {economy.Population} mouths consume {economy.FoodConsumedPerHour}/hour   •   " +
            $"{economy.FoodProducedPerHour}/hour produced ({economy.Farmers} farmers, {economy.Fishers} fishers, {economy.Hunters} hunters)   •   net {net}/hour\n" +
            $"Stores {economy.FoodStored}   •   about {economy.HoursOfFoodRemaining} full hours banked   •   {recruitment}" +
            (economy.IsShortage ? "   •   FOOD SHORTAGE" : string.Empty);
    }

    private static string FormatIronEconomy(WorldIronEconomyData economy)
    {
        var source = economy.Source;
        var guardNames = economy.StonehavenMineGuards.Names.Count == 0
            ? "none hired"
            : string.Join(", ", economy.StonehavenMineGuards.Names);
        return
            $"{source.Grid} ONLY SOURCE: {source.Name} {source.Remaining}/{source.Capacity} ore   •   mine {source.MineHealth}/{source.MineMaximumHealth} HP   •   {(source.Operational ? "OPERATIONAL" : "STOPPED")}\n" +
            $"{FormatIronOperation(economy.Stonehaven)}\n" +
            $"{FormatIronOperation(economy.Darkwood)}\n" +
            $"Stonehaven mine guard: {economy.StonehavenMineGuards.Count} ({guardNames})   •   " +
            $"{economy.StonehavenMineGuards.GoldPerGuardPerWorldDay} gold each/world day   •   " +
            $"current cost {economy.StonehavenMineGuards.CurrentDailyCost}/day   •   treasury {economy.StonehavenMineGuards.TreasuryGold} gold";
    }

    private static string FormatIronOperation(WorldIronOperationData operation)
    {
        var weaponNext = operation.NextWeaponTierCost is null
            ? "MAX"
            : $"{operation.NextWeaponTierCost} iron next";
        var armorNext = operation.NextArmorTierCost is null
            ? "MAX"
            : $"{operation.NextArmorTierCost} iron next";
        return
            $"{operation.Owner}: {operation.MinerName}   •   {SplitPascalCase(operation.Status)}   •   " +
            $"cargo {operation.CargoIron}   •   delivered {operation.TotalIronDelivered} over {operation.TripsCompleted} trips   •   stores {operation.StoredIron}\n" +
            $"  Persistent equipment: weapons L{operation.WeaponTier} ({weaponNext})   •   armor L{operation.ArmorTier} ({armorNext})";
    }

    private static string FormatFactionBanks(WorldFactionBanksData banks) =>
        $"{FormatFactionBank(banks.Stonehaven)}\n{FormatFactionBank(banks.Darkwood)}";

    private static string FormatSettlementRecovery(WorldSettlementRecoveriesData recovery) =>
        $"{FormatSettlementRecovery(recovery.Stonehaven)}\n{FormatSettlementRecovery(recovery.Darkwood)}";

    private static string FormatSettlementRecovery(WorldSettlementRecoveryData recovery)
    {
        var status = recovery.Status.ToUpperInvariant();
        var timer = recovery.Status.Equals("Defeated", StringComparison.OrdinalIgnoreCase)
            ? $"   â€¢   founders return in {TimeSpan.FromSeconds(recovery.RecoverySecondsRemaining):mm\\:ss}"
            : string.Empty;
        var target = recovery.Status.Equals("Rebuilding", StringComparison.OrdinalIgnoreCase)
            ? $"   â€¢   current work: {SplitPascalCase((recovery.CurrentStructureKey ?? "waiting-for-materials").Replace("-", " "))}"
            : string.Empty;
        return
            $"{recovery.Name}: {status}{timer}{target}\n" +
            $"  Founders {recovery.FoundingPopulation}   â€¢   functional structures {recovery.FunctionalStructuresRestored}/{recovery.FunctionalStructuresTotal}   â€¢   gates/walls {recovery.DefensesRestored}/{recovery.DefensesTotal}\n" +
            $"  Structure health {recovery.StructureHealth}/{recovery.StructureMaximumHealth}   â€¢   rebuild cycles {recovery.RebuildCycles}";
    }

    private static string RecoveryAlert(WorldSettlementRecoveriesData recovery)
    {
        var active = new[] { recovery.Stonehaven, recovery.Darkwood }
            .FirstOrDefault(x => !x.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase));
        if (active is null)
        {
            return string.Empty;
        }
        return active.Status.Equals("Defeated", StringComparison.OrdinalIgnoreCase)
            ? $"{active.Name.ToUpperInvariant()} DEFEATED â€” {TimeSpan.FromSeconds(active.RecoverySecondsRemaining):mm\\:ss}"
            : $"{active.Name.ToUpperInvariant()} REBUILDING";
    }

    private static string FormatFactionBank(WorldFactionBankData bank)
    {
        var inventory = string.Join("   •   ", bank.Inventory.Select(item =>
            $"{item.Kind.ToUpperInvariant()} bank {item.BankQuantity}, faction {item.FactionStored}/{item.TargetReserve}" +
            (item.Shortage > 0 ? $" SHORT {item.Shortage}" : string.Empty) +
            $", pays {item.BankBuyPrice}/charges {item.BankSellPrice}"));
        var transactions = bank.RecentTransactions.Count == 0
            ? "No purchases yet; every resource shelf started at zero."
            : string.Join(" | ", bank.RecentTransactions.Take(3).Select(x =>
                $"{x.OccurredAtCentral:MMM d h:mm tt} CST: {x.Description}"));
        return
            $"{bank.Name} ({bank.Owner})   •   bank gold {bank.BankGold}   •   faction treasury {bank.FactionGold}\n" +
            $"  {inventory}\n" +
            $"  Recent: {transactions}";
    }

    private static string SplitPascalCase(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    private static void SetReadinessLabel(Label label, WorldTriggerReadinessData readiness)
    {
        if (!GodotObject.IsInstanceValid(label))
        {
            return;
        }

        var status = readiness.Active
            ? "ACTIVE"
            : readiness.Ready
                ? readiness.AdministratorOnline
                    ? "READY — ADMINISTRATOR MAY AUTHORIZE"
                    : "READY — WAITING FOR ADMINISTRATOR"
                : "NOT READY";
        label.Text =
            $"{readiness.Name.ToUpperInvariant()}  •  {status}\n" +
            $"{readiness.Current}/{readiness.Required}  •  {readiness.Explanation}";
        label.Modulate = readiness.Active
            ? new Color("f0644c")
            : readiness.Ready
                ? new Color("6ed58a")
                : new Color("d7c7a5");
    }

    public void SetWorldAdvanceBusy(bool busy)
    {
        if (IsInstanceValid(_advanceWorldButton))
        {
            _advanceWorldButton.Disabled = busy;
            _advanceWorldButton.Text = busy ? "Advancing the Living World..." : "Advance 24 World Hours  [Playtest]";
        }
    }

    public void SetWorldResetBusy(bool busy)
    {
        if (IsInstanceValid(_resetWorldButton))
        {
            _resetWorldButton.Disabled = busy;
            _resetWorldButton.Text = busy ? "Resetting the living world..." : "Reset Living World  [Playtest]";
        }
    }

    public void SetRaidStartBusy(bool busy)
    {
        if (IsInstanceValid(_startRaidButton))
        {
            _startRaidButton.Disabled = busy || !_canStartDarkwoodRaid;
            _startRaidButton.Text = busy
                ? "Darkwood Is Assembling..."
                : _canStartDarkwoodRaid
                    ? "Authorize Darkwood Raid"
                    : "Darkwood Raid Not Ready";
        }
    }

    public void SetCounterattackStartBusy(bool busy)
    {
        if (IsInstanceValid(_startCounterattackButton))
        {
            _startCounterattackButton.Disabled = busy || !_canStartCounterattack;
            _startCounterattackButton.Text = busy
                ? "Stonehaven Is Assembling..."
                : _canStartCounterattack
                    ? "Authorize Stonehaven Counterattack"
                    : "Stonehaven Counterattack Not Ready";
        }
    }

    public void SetRaidState(WorldRaidStateData state)
    {
        _raidActive = state.Active;
        _canStartDarkwoodRaid = state.CanStartDarkwoodRaid;
        _canStartCounterattack = state.CanStartCounterattack;
        _darkwoodRaidPhase = state.Raid?.Phase ?? string.Empty;
        _counterattackPhase = state.Counterattack?.Status ?? string.Empty;
        _counterattackActive = state.Counterattack is not null &&
                               IsActiveCounterattackStatus(_counterattackPhase);
        if (!_raidActive)
        {
            _raidCombatObservedSinceAdvance = false;
        }
        UpdateRaidCombatAssignments();
        if (!IsInstanceValid(_raidHudLabel))
        {
            return;
        }

        _startRaidButton.Visible = state.IsAdministrator;
        _startRaidButton.Disabled = !state.CanStartDarkwoodRaid;
        _startRaidButton.Text = state.CanStartDarkwoodRaid
            ? "Authorize Darkwood Raid"
            : "Darkwood Raid Not Ready";
        _startCounterattackButton.Visible = state.IsAdministrator;
        _startCounterattackButton.Disabled = !state.CanStartCounterattack;
        _startCounterattackButton.Text = state.CanStartCounterattack
            ? "Authorize Stonehaven Counterattack"
            : "Stonehaven Counterattack Not Ready";
        var darkwoodRaidActive = state.Raid?.Status.Equals(
            "Active",
            StringComparison.OrdinalIgnoreCase) == true;
        if (state.Counterattack is not null &&
            (_counterattackActive ||
             (!darkwoodRaidActive &&
              (state.Raid is null || state.Counterattack.WorldDay >= state.Raid.WorldDay))))
        {
            var assault = state.Counterattack;
            _raidHudLabel.Visible = true;
            _raidHudLabel.Modulate = _counterattackActive
                ? new Color("e5bd62")
                : assault.Status.Equals("StonehavenVictory", StringComparison.OrdinalIgnoreCase)
                    ? new Color("e5bd62")
                    : new Color("f0644c");
            _raidHudLabel.Text = _counterattackActive
                ? $"STONEHAVEN COUNTERATTACK  •  {FormatCounterattackStatus(assault.Status).ToUpperInvariant()}  •  SOLDIERS {assault.SoldiersRemaining}/{assault.InitialSoldierCount}  •  GOBLINS {assault.GoblinsRemaining}/{assault.InitialGoblinCount}"
                : assault.Status.Equals("StonehavenVictory", StringComparison.OrdinalIgnoreCase)
                    ? $"LAST COUNTERATTACK: DARKWOOD CAMP REDUCED TO LEVEL {assault.CampLevelAfter}"
                    : "LAST COUNTERATTACK: DARKWOOD HELD ITS CAMP";
            _raidDetails.Text =
                $"STONEHAVEN COUNTERATTACK — {FormatCounterattackStatus(assault.Status).ToUpperInvariant()} ON WORLD DAY {assault.WorldDay}\n" +
                $"Guard Captain Mira's force: {assault.SoldiersRemaining}/{assault.InitialSoldierCount} standing   •   " +
                $"Darkwood defenders: {assault.GoblinsRemaining}/{assault.InitialGoblinCount} standing\n" +
                $"Camp: level {assault.CampLevelBefore}   •   strength {assault.CampStrength}/{assault.InitialCampStrength}   •   " +
                $"Stonehaven casualties {assault.StonehavenCasualties}   •   Darkwood casualties {assault.DarkwoodCasualties}\n" +
                (assault.OutcomeSummary ?? CounterattackInstruction(assault.Status));
            return;
        }
        if (!state.HasRaid || state.Raid is null)
        {
            _raidHudLabel.Visible = false;
            _raidDetails.Text =
                "No battle is underway. A ready campaign remains paused until an online administrator authorizes it from this Journey page.";
            return;
        }

        var raid = state.Raid;
        var attackersStanding = raid.Attackers.Count(x => !x.IsDefeated);
        if (state.Active)
        {
            _raidHudLabel.Visible = true;
            _raidHudLabel.Text =
                $"DARKWOOD CAMPAIGN  •  {FormatDarkwoodRaidPhase(raid.Phase).ToUpperInvariant()}  •  RAIDERS {attackersStanding}/{raid.Attackers.Count}";
            _raidHudLabel.Modulate = new Color("f0644c");
            _raidDetails.Text =
                $"DARKWOOD RAID — {FormatDarkwoodRaidPhase(raid.Phase).ToUpperInvariant()} ON WORLD DAY {raid.WorldDay}\n" +
                $"ATTACKER STRENGTH {raid.AttackerStrength}/{raid.InitialAttackerStrength}   •   " +
                $"DEFENDER STRENGTH {raid.DefenderStrength}/{raid.InitialDefenderStrength}   •   " +
                $"STRUCTURE HEALTH {raid.StructureStrength}/{raid.InitialStructureStrength}\n" +
                $"ATTACKERS STANDING {attackersStanding}/{raid.Attackers.Count}   •   " +
                $"PLAYER CONTRIBUTION {raid.PlayerContribution}\n" +
                DarkwoodRaidInstruction(raid.Phase);
        }
        else
        {
            var survivingRaiders = raid.Attackers.Count(x =>
                !x.IsDefeated && x.Status.Equals("Alive", StringComparison.OrdinalIgnoreCase));
            _raidHudLabel.Visible = true;
            _raidHudLabel.Text = raid.Status.Equals("DefendersWon", StringComparison.OrdinalIgnoreCase)
                ? $"LAST RAID: STONEHAVEN HELD  •  PLAYER CONTRIBUTION {raid.PlayerContribution}"
                : survivingRaiders > 0
                    ? $"STONEHAVEN BREACHED  •  {survivingRaiders} RAIDERS REMAIN"
                    : "LAST RAID: DARKWOOD BREACHED STONEHAVEN";
            _raidHudLabel.Modulate = raid.Status.Equals("DefendersWon", StringComparison.OrdinalIgnoreCase)
                ? new Color("e5bd62")
                : new Color("f0644c");
            _raidDetails.Text =
                $"DARKWOOD RAID — {FormatRaidStatus(raid.Status).ToUpperInvariant()}\n" +
                $"SETTLEMENT DAMAGE {raid.SettlementDamage}   •   INJURIES {raid.ResidentInjuries}   •   " +
                $"CASUALTIES {raid.ResidentCasualties}   •   PLAYER CONTRIBUTION {raid.PlayerContribution}\n" +
                (raid.OutcomeSummary ?? "The raid outcome was recorded in the World Chronicle.") +
                (survivingRaiders > 0
                    ? "\nSurviving raiders remain physically in Stonehaven until players defeat them or the world is reset."
                    : string.Empty);
        }
    }

    public void LoadCreatures(
        IEnumerable<WorldCreatureData> creatures,
        bool removeMissing = false,
        bool synchronizePositions = false)
    {
        var received = new HashSet<Guid>();
        foreach (var data in creatures)
        {
            var normalizedData = NormalizeCreatureLocation(data);
            received.Add(normalizedData.Id);
            if (_creatures.TryGetValue(normalizedData.Id, out var existing))
            {
                existing.ApplyServerState(normalizedData, synchronizePosition: synchronizePositions);
                existing.SetOverheadDetail(_isAdministrator && _showDetailedOverhead);
                continue;
            }

            var creature = new CombatCreature { Name = $"Creature-{normalizedData.Name}" };
            creature.Configure(normalizedData, _player, _pathfinder);
            creature.AttackPlayerRequested += OnCreatureAttackRequested;
            creature.RaidCombatPulse += OnRaidCombatPulse;
            creature.ResourceWorkPulse += OnResourceWorkPulse;
            _creatures[normalizedData.Id] = creature;
            AddChild(creature);
            creature.AiEnabled = !_menuOpen && !_inventoryOpen && !_worldOpen;
            creature.PlayerTargetable = _knockoutProtectionSeconds <= 0;
            creature.SetPlayerSelected(_selectedTargetId == normalizedData.Id);
            creature.SetOverheadDetail(_isAdministrator && _showDetailedOverhead);
        }

        if (removeMissing)
        {
            foreach (var removed in _creatures.Keys.Where(id => !received.Contains(id)).ToArray())
            {
                var creature = _creatures[removed];
                if (_selectedTargetId == removed)
                {
                    SetSelectedTarget(null);
                }
                creature.AttackPlayerRequested -= OnCreatureAttackRequested;
                creature.RaidCombatPulse -= OnRaidCombatPulse;
                creature.ResourceWorkPulse -= OnResourceWorkPulse;
                creature.QueueFree();
                _creatures.Remove(removed);
            }
        }

        UpdateRaidCombatAssignments();
    }

    public void LoadResidents(
        IEnumerable<WorldResidentData> residents,
        bool synchronizePositions = false)
    {
        var received = new HashSet<Guid>();
        foreach (var data in residents)
        {
            received.Add(data.Id);
            _residentRoster[data.Id] = data;
        }

        foreach (var removed in _residentRoster.Keys.Where(id => !received.Contains(id)).ToArray())
        {
            _residentRoster.Remove(removed);
        }

        RefreshActiveResidents(synchronizePositions, refreshExisting: true);
    }

    private void RefreshActiveResidents(
        bool synchronizePositions = false,
        bool refreshExisting = false)
    {
        if (!IsInstanceValid(_player) || _residentRoster.Count == 0)
        {
            return;
        }

        var playerPosition = _player.GlobalPosition;
        var activeLimit = _raidActive ? MaximumRaidResidents : MaximumActiveResidents;
        var desired = _residentRoster.Values
            .Where(data => !data.Status.Equals("Missing", StringComparison.OrdinalIgnoreCase))
            .Select(data => new
            {
                Data = data,
                Distance = HorizontalDistance(playerPosition, data.Position)
            })
            .Where(candidate => candidate.Distance <= ResidentActivationRadius)
            .OrderBy(candidate => _raidActive && candidate.Data.CanFight ? 0 : 1)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Data.Id)
            .Take(activeLimit)
            .Select(candidate => candidate.Data)
            .ToArray();
        var desiredIds = desired.Select(data => data.Id).ToHashSet();

        foreach (var removed in _residents.Keys.Where(id => !desiredIds.Contains(id)).ToArray())
        {
            RemoveActiveResident(removed);
        }

        foreach (var data in desired)
        {
            if (_residents.TryGetValue(data.Id, out var existing))
            {
                if (refreshExisting)
                {
                    existing.ApplyData(data, synchronizePositions);
                }
                existing.SetOverheadDetail(_isAdministrator && _showDetailedOverhead);
                continue;
            }

            var resident = new SettlementNpc { Name = $"Resident-{data.Name}" };
            resident.Configure(data, _player, _pathfinder);
            resident.RaidCombatPulse += OnRaidCombatPulse;
            resident.ResourceWorkPulse += OnResourceWorkPulse;
            resident.SettlementDefenseAttackRequested += OnSettlementDefenseAttackRequested;
            _residents[data.Id] = resident;
            AddChild(resident);
            resident.AiEnabled = !_menuOpen && !_inventoryOpen && !_worldOpen;
            resident.SetOverheadDetail(_isAdministrator && _showDetailedOverhead);
        }

        UpdateRaidCombatAssignments();
    }

    private void ApplyOverheadLabelDetail()
    {
        var detailed = _isAdministrator && _showDetailedOverhead;
        foreach (var resident in _residents.Values)
        {
            resident.SetOverheadDetail(detailed);
        }
        foreach (var creature in _creatures.Values)
        {
            creature.SetOverheadDetail(detailed);
        }
    }

    private void RemoveActiveResident(Guid residentId)
    {
        if (!_residents.Remove(residentId, out var resident))
        {
            return;
        }

        resident.RaidCombatPulse -= OnRaidCombatPulse;
        resident.ResourceWorkPulse -= OnResourceWorkPulse;
        resident.SettlementDefenseAttackRequested -= OnSettlementDefenseAttackRequested;
        resident.QueueFree();
    }

    private void OnRaidCombatPulse()
    {
        if (_raidActive)
        {
            _raidCombatObservedSinceAdvance = true;
        }
    }

    private void OnSettlementDefenseAttackRequested(
        Guid residentId,
        Guid creatureId,
        Vector3 residentPosition,
        Vector3 creaturePosition)
    {
        if (_raidActive &&
            _creatures.TryGetValue(creatureId, out var creature) &&
            (creature.IsRaidAttacker || (_counterattackActive && creature.IsDarkwoodClanMember)))
        {
            // Formal raid damage is resolved in synchronized raid rounds.
            return;
        }

        SettlementDefenseAttackRequested?.Invoke(
            residentId,
            creatureId,
            residentPosition,
            creaturePosition);
    }

    public void ApplyCreatureState(
        WorldCreatureData data,
        bool flashDamage,
        bool synchronizePosition = false)
    {
        if (!_creatures.TryGetValue(data.Id, out var creature))
        {
            LoadCreatures([data]);
            return;
        }

        creature.ApplyServerState(data, synchronizePosition);
        if (flashDamage)
        {
            creature.FlashDamage();
        }
        UpdateRaidCombatAssignments();
    }

    private void UpdateRaidCombatAssignments()
    {
        UpdateRaidStructureTargets();
        if (_counterattackActive)
        {
            var soldiers = _residents.Values
                .Where(resident => resident.IsCounterattackSoldier)
                .OrderBy(resident => resident.ResidentName)
                .ToArray();
            var goblins = _counterattackPhase.Equals("FightingGoblins", StringComparison.OrdinalIgnoreCase)
                ? _creatures.Values
                    .Where(creature => creature.IsAlive &&
                                       creature.IsDarkwoodClanMember &&
                                       HorizontalDistance(creature.GlobalPosition, DarkwoodCampCenter) <= 34.0f)
                    .OrderBy(creature => creature.CreatureName)
                    .ToArray()
                : [];
            AssignOpposingCombatGroups(goblins, soldiers);
            return;
        }

        var attackers = _creatures.Values
            .Where(creature => creature.IsAlive &&
                               ((_raidActive && creature.IsRaidAttacker &&
                                 IsAtStonehavenBattlefront(creature.GlobalPosition)) ||
                                (IsInsideStonehavenDefense(creature.GlobalPosition) &&
                                 (creature.IsEngagedWithPlayer ||
                                  creature.HasSettlementDefenseTarget ||
                                  creature.IsBoss))))
            .OrderBy(creature => creature.CreatureName)
            .ToArray();
        var defenders = _residents.Values
            .Where(resident => resident.IsRaidDefender &&
                               resident.Role.Contains("Guard", StringComparison.OrdinalIgnoreCase))
            .OrderBy(resident => resident.ResidentName)
            .ToArray();
        if (defenders.Length == 0)
        {
            defenders = _residents.Values
                .Where(resident => resident.IsRaidDefender)
                .OrderBy(resident => resident.ResidentName)
                .ToArray();
        }
        AssignOpposingCombatGroups(attackers, defenders);
    }

    private void UpdateRaidStructureTargets()
    {
        Vector3? targetPosition = null;
        if (_raidActive &&
            _darkwoodRaidPhase.Equals("AttackingStructures", StringComparison.OrdinalIgnoreCase))
        {
            targetPosition = _destructibleStructures.Values
                .Where(structure =>
                    structure.Owner.Equals("Stonehaven", StringComparison.OrdinalIgnoreCase) &&
                    structure.IsBuilt &&
                    structure.Health > 0)
                .OrderBy(structure => structure.Kind switch
                {
                    "Wall" => 0,
                    "Gate" => 1,
                    "Mine" => 2,
                    "Farm" => 3,
                    "Stockpile" => 4,
                    "Building" => 5,
                    "Dock" => 6,
                    _ => 7
                })
                .ThenBy(structure => structure.Health)
                .ThenBy(structure => structure.Name)
                .Select(structure => (Vector3?)structure.Position)
                .FirstOrDefault();
        }

        foreach (var creature in _creatures.Values)
        {
            creature.SetRaidStructureTarget(
                creature.IsRaidAttacker ? targetPosition : null);
        }
    }

    private void AssignOpposingCombatGroups(
        CombatCreature[] attackers,
        SettlementNpc[] defenders)
    {
        if (attackers.Length == 0 || defenders.Length == 0)
        {
            foreach (var creature in _creatures.Values)
            {
                creature.SetRaidDefenseTarget(null);
            }
            foreach (var resident in _residents.Values)
            {
                resident.SetRaidCombatTarget(null);
            }
            return;
        }

        for (var index = 0; index < attackers.Length; index++)
        {
            attackers[index].SetRaidDefenseTarget(defenders[index % defenders.Length]);
        }
        for (var index = 0; index < defenders.Length; index++)
        {
            defenders[index].SetRaidCombatTarget(attackers[index % attackers.Length]);
        }
        foreach (var defeatedAttacker in _creatures.Values.Where(creature => !attackers.Contains(creature)))
        {
            defeatedAttacker.SetRaidDefenseTarget(null);
        }
        foreach (var nonDefender in _residents.Values.Where(resident => !defenders.Contains(resident)))
        {
            nonDefender.SetRaidCombatTarget(null);
        }
    }

    private static bool IsInsideStonehavenDefense(Vector3 position) =>
        Mathf.Abs(position.X) <= 30.5f &&
        position.Z is >= -37.0f and <= 8.5f;

    private static bool IsAtStonehavenBattlefront(Vector3 position) =>
        Mathf.Abs(position.X) <= 46.0f &&
        position.Z is >= -42.0f and <= 18.0f;

    public void ApplyCharacterState(
        int level,
        long experience,
        int health,
        int maximumHealth,
        Vector3 position,
        bool knockedOut,
        string message)
    {
        _level = level;
        _experience = experience;
        _health = health;
        _maximumHealth = maximumHealth;
        _identityLabel.Text = $"{_characterName.ToUpperInvariant()}  •  LEVEL {_level} {_archetype.ToUpperInvariant()}";
        _healthBar.MaxValue = Math.Max(1, _maximumHealth);
        _healthBar.Value = _health;
        _healthLabel.Text = $"HEALTH  {_health}/{_maximumHealth}";
        _experienceBar.MaxValue = Math.Max(1, _level * 100L);
        _experienceBar.Value = _experience;
        _experienceLabel.Text = $"EXPERIENCE  {_experience}/{_level * 100L}";
        if (knockedOut)
        {
            _player.GlobalPosition = position;
            _player.Velocity = Vector3.Zero;
            _knockoutProtectionSeconds = KnockoutProtectionDuration;
            ApplyOverlayPauseState();
            message += " Stonehaven's sanctuary protects you for 8 seconds while attackers disengage from you; raid forces continue fighting Stonehaven's defenders.";
        }
        SetCombatStatus(message, false);
    }

    private void SpawnPlayer()
    {
        _player = new ThirdPersonPlayer
        {
            Name = _characterName,
            Position = _requestedSpawn
        };
        _player.Configure(_characterName, _archetype);
        _player.AttackRequested += OnPlayerAttackRequested;
        AddChild(_player);
    }

    private void OnPlayerAttackRequested()
    {
        if (_knockoutProtectionSeconds > 0)
        {
            SetCombatStatus("Stonehaven's sanctuary prevents combat while you recover.", false);
            return;
        }
        var range = _player.IsRanger ? 18.0f : 2.8f;
        var target = FindAttackTarget(range);
        if (target is null)
        {
            var selected = GetSelectedTarget();
            SetCombatStatus(
                selected is not null
                    ? $"{selected.CreatureName} is {HorizontalDistance(selected.GlobalPosition, PlayerPosition):0.0} meters away. Move within {range:0.0} meters to attack your selected target."
                    : _player.IsRanger
                    ? "No creature is within Elara's bow range."
                    : "Move closer to a creature before Alden attacks.",
                true);
            return;
        }

        var creatureId = target.CreatureId;
        var attackFrom = PlayerPosition;
        var attackTo = target.GlobalPosition;
        PlayWeaponAttack(
            target,
            () => PlayerAttackRequested?.Invoke(creatureId, attackFrom, attackTo));
    }

    private void UseHotkeySkill(string hotkey)
    {
        if (_knockoutProtectionSeconds > 0)
        {
            SetCombatStatus("Recover beneath Stonehaven's sanctuary before using another skill.", false);
            return;
        }
        if (!_skills.TryGetValue(hotkey, out var skill))
        {
            SetCombatStatus("Skills are still loading.", true);
            return;
        }

        if (!skill.IsOffensive)
        {
            SkillRequested?.Invoke(skill.Key, null, PlayerPosition, null);
            return;
        }

        var target = FindAttackTarget(skill.Range);
        if (target is null)
        {
            var selected = GetSelectedTarget();
            SetCombatStatus(
                selected is not null
                    ? $"{selected.CreatureName} is outside {skill.Name}'s {skill.Range:0.0}-meter range."
                    : $"No creature is within {skill.Name}'s range.",
                true);
            return;
        }

        var skillKey = skill.Key;
        var creatureId = target.CreatureId;
        var attackFrom = PlayerPosition;
        var attackTo = target.GlobalPosition;
        PlayWeaponAttack(
            target,
            () => SkillRequested?.Invoke(skillKey, creatureId, attackFrom, attackTo));
    }

    private static IReadOnlyCollection<WorldCreatureData> CreatePreviewCreatures()
    {
        return
        [
            new WorldCreatureData(
                Guid.Parse("8bd3a92f-80a8-46a6-8349-427975490a01"),
                "forest-rat", "Forest Rat", "Brambletail", null, "Wild Creature", 1, 30, 30, 4, 2,
                3.2f, 7.0f, 1.35f, 20, "Alive", new Vector3(76, 0.08f, 68), new Vector3(76, 0.08f, 68), null, false),
            new WorldCreatureData(
                Guid.Parse("5d8a9637-a327-4f42-8ec3-a292f548d101"),
                "prairie-wolf", "Prairie Wolf", "Ashfang", null, "Wild Creature", 2, 55, 55, 10, 5,
                4.2f, 10.0f, 1.7f, 45, "Alive", new Vector3(84, 0.08f, 101), new Vector3(84, 0.08f, 101), null, false),
            new WorldCreatureData(
                Guid.Parse("9230414d-a60d-46ca-9c59-36cc3b867201"),
                "goblin-raider", "Goblin Raider", "Skrit", null, "Woodcutter", 5, 90, 90, 15, 9,
                3.6f, 12.0f, 1.8f, 70, "Alive", new Vector3(-124.0f, 0.08f, -99.0f), new Vector3(-124.0f, 0.08f, -99.0f), null, false),
            new WorldCreatureData(
                Guid.Parse("f4c5a7b9-644f-4c85-b18f-ac38294e3001"),
                "goblin-chief", "Goblin Chief", "Gorvak", "Clan Chief", "Chief", 8, 180, 180, 22, 14,
                3.2f, 15.0f, 2.1f, 90, "Alive", new Vector3(-116.0f, 0.08f, -112.0f), new Vector3(-116.0f, 0.08f, -112.0f), null, true)
        ];
    }

    private static WorldCreatureData NormalizeCreatureLocation(WorldCreatureData data)
    {
        Vector3? trainingSpawn = data.Id switch
        {
            var id when id == Guid.Parse("8bd3a92f-80a8-46a6-8349-427975490a01") => new Vector3(76, 0.08f, 68),
            var id when id == Guid.Parse("8bd3a92f-80a8-46a6-8349-427975490a02") => new Vector3(92, 0.08f, 78),
            var id when id == Guid.Parse("8bd3a92f-80a8-46a6-8349-427975490a03") => new Vector3(110, 0.08f, 72),
            var id when id == Guid.Parse("5d8a9637-a327-4f42-8ec3-a292f548d101") => new Vector3(84, 0.08f, 101),
            var id when id == Guid.Parse("5d8a9637-a327-4f42-8ec3-a292f548d102") => new Vector3(111, 0.08f, 105),
            _ => null
        };
        if (trainingSpawn is not null &&
            (!data.Position.IsFinite() ||
             data.Position.X < 50 || data.Position.Z < 50 ||
             data.Position.X > 139 || data.Position.Z > 139))
        {
            return data with { Position = trainingSpawn.Value, SpawnPosition = trainingSpawn.Value };
        }

        if (data.IsRaidAttacker && data.SpawnPosition.Z > -60.0f)
        {
            var spawn = GetDarkwoodRaidSpawn(data.Name);
            return data with { Position = spawn, SpawnPosition = spawn };
        }

        Vector3? campSpawn = data.Name switch
        {
            "Skrit" => new Vector3(-124.0f, 0.08f, -99.0f),
            "Vrak" => new Vector3(-107.0f, 0.08f, -103.0f),
            "Gorvak" => new Vector3(-116.0f, 0.08f, -112.0f),
            _ => null
        };
        if (campSpawn is not null && data.SpawnPosition.Z > -60.0f)
        {
            return data with { Position = campSpawn.Value, SpawnPosition = campSpawn.Value };
        }

        return data;
    }

    private static Vector3 GetDarkwoodRaidSpawn(string name)
    {
        if (name.Contains("Captain", StringComparison.OrdinalIgnoreCase))
        {
            return new Vector3(-119.0f, 0.08f, -94.0f);
        }
        if (name.EndsWith('1'))
        {
            return new Vector3(-124.0f, 0.08f, -96.0f);
        }
        if (name.EndsWith('3'))
        {
            return new Vector3(-113.0f, 0.08f, -94.0f);
        }
        return new Vector3(-108.0f, 0.08f, -96.0f);
    }

    private static WorldStateData CreatePreviewWorldState() => new(
        0,
        1,
        "Real time: 1 real minute = 1 world minute",
        true,
        new WorldFactionData(
            "Darkwood Clan", 7, 10, 1, "Encampment", 1, 45, 55, 1, 66,
            [
                new WorldResourceData("Food", 80, 250),
                new WorldResourceData("Wood", 50, 250),
                new WorldResourceData("Stone", 15, 180),
                new WorldResourceData("Iron", 5, 120)
            ],
            [
                new WorldStructureData("Hide Tents", 1, 100),
                new WorldStructureData("Crude Stockpile", 1, 100)
            ],
            new WorldLeaderData("Gorvak", "Goblin Chief", 8, 10, 180, 180, 22, 14, "Alive")),
        new WorldSettlementData(
            "Stonehaven Village",
            11,
            11,
            3,
            24,
            64,
            40,
            24,
            4,
            65,
            42,
            new WorldSettlementLeaderData(
                "Reeve Aldric Vale",
                "Reeve of Stonehaven",
                "Reeve of Stonehaven",
                105,
                105,
                "Active",
                "Administration",
                4,
                "Pragmatic",
                true,
                "Aldric keeps Stonehaven's stores, work assignments, and defenses accountable.")),
        new WorldSurvivalData(
            new WorldFoodEconomyData(11, 64, 2, 1, 0, 10, 3, 0, 13, 11, 2, false, 5, "None"),
            new WorldFoodEconomyData(7, 80, 0, 0, 1, 0, 0, 10, 10, 7, 3, false, 11, "None"),
            new WorldWildlifeData(15, 15, 0)),
        new WorldIronEconomyData(
            new WorldIronSourceData("A3", "Irondeep Ore Vein", 45, 45, 1400, 1400, true),
            new WorldIronOperationData(
                "Stonehaven", "Dain", "TravelingToMine", 8, 0.08f, -24, 0, 0, 0, 4, 0, 12, 0, 10),
            new WorldIronOperationData(
                "Darkwood", "Darkwood miner not yet assigned", "TravelingToMine",
                -116, 0.08f, -104, 0, 0, 0, 5, 0, 12, 0, 10),
            new WorldMineGuardData(0, 5, 0, 30, [])),
        new WorldFactionBanksData(
            new WorldFactionBankData(
                "Stonehaven",
                "Stonehaven Exchange",
                300,
                30,
                [
                    new WorldBankInventoryData("Food", 0, 1, 2, 64, 88, 24, null, null),
                    new WorldBankInventoryData("Wood", 0, 2, 3, 40, 80, 40, null, null),
                    new WorldBankInventoryData("Stone", 0, 3, 5, 24, 60, 36, null, null),
                    new WorldBankInventoryData("Iron", 0, 6, 9, 4, 10, 6, null, null)
                ],
                []),
            new WorldFactionBankData(
                "Darkwood",
                "Darkwood Clan Vault",
                300,
                0,
                [
                    new WorldBankInventoryData("Food", 0, 1, 2, 80, 56, 0, null, null),
                    new WorldBankInventoryData("Wood", 0, 2, 3, 50, 90, 40, null, null),
                    new WorldBankInventoryData("Stone", 0, 3, 5, 15, 65, 50, null, null),
                    new WorldBankInventoryData("Iron", 0, 6, 9, 5, 10, 5, null, null)
                ],
                [])),
        new WorldSettlementRecoveriesData(
            new WorldSettlementRecoveryData(
                "Stonehaven", "Stonehaven", "Healthy", 11,
                null, null, 0, null, null, null, null, 0,
                18, 18, 1, 1, 15000, 15000),
            new WorldSettlementRecoveryData(
                "Darkwood", "Darkwood", "Healthy", 7,
                null, null, 0, null, null, null, null, 0,
                2, 2, 0, 0, 1500, 1500)),
        [
            new DestructibleStructureData(
                Guid.Parse("83000000-0000-4000-8000-000000000001"),
                "stonehaven-gate",
                "Stonehaven Main Gate",
                "Stonehaven",
                "Gate",
                1800,
                1800,
                12,
                true,
                true,
                "Healthy",
                0,
                new Vector3(0, 0.08f, 3.5f),
                null,
                null),
            new DestructibleStructureData(
                Guid.Parse("83000000-0000-4000-8000-000000000029"),
                "darkwood-hide-tents",
                "Darkwood Hide Tents",
                "Darkwood",
                "Building",
                800,
                800,
                4,
                true,
                true,
                "Healthy",
                0,
                new Vector3(-119, 0.08f, -107),
                null,
                null)
        ],
        new WorldEventReadinessData(
            new WorldTriggerReadinessData(
                "Darkwood raid on Stonehaven",
                6,
                15,
                false,
                false,
                false,
                "A raid launches when Darkwood has 15 living raid-ready goblins; its current leader remains at the camp and is not counted."),
            new WorldTriggerReadinessData(
                "Stonehaven counterattack on Darkwood",
                11,
                20,
                false,
                false,
                false,
                "Guard Captain Mira assembles 20 living residents after Darkwood completes camp level 3.")),
        new WorldEventQueueData(0, 0, 0),
        [
            new WorldHistoryData(
                "The Darkwood Clan Raised Its First Tents",
                "Gorvak gathered seven goblins beneath the Darkwood boughs and declared the valley theirs.",
                DateTimeOffset.Now)
        ]);

    private void OnCreatureAttackRequested(Guid creatureId)
    {
        if (_knockoutProtectionSeconds <= 0 && !_menuOpen && !_inventoryOpen && !_worldOpen)
        {
            if (_creatures.TryGetValue(creatureId, out var creature))
            {
                CreatureAttackRequested?.Invoke(creatureId, PlayerPosition, creature.GlobalPosition);
            }
        }
    }

    private void CycleTarget(bool reverse)
    {
        var targets = _creatures.Values
            .Where(creature => creature.IsAlive &&
                               HorizontalDistance(creature.GlobalPosition, PlayerPosition) <= TargetCycleRadius)
            .OrderBy(creature => HorizontalDistance(creature.GlobalPosition, PlayerPosition))
            .ThenBy(creature => creature.CreatureName)
            .ToArray();
        if (targets.Length == 0)
        {
            SetSelectedTarget(null);
            SetCombatStatus($"No living creature is within {TargetCycleRadius:0} meters.", true);
            return;
        }

        var currentIndex = Array.FindIndex(targets, creature => creature.CreatureId == _selectedTargetId);
        var nextIndex = currentIndex < 0
            ? reverse ? targets.Length - 1 : 0
            : (currentIndex + (reverse ? -1 : 1) + targets.Length) % targets.Length;
        var target = targets[nextIndex];
        SetSelectedTarget(target);
        SetCombatStatus(
            $"Target locked: {target.CreatureName}, level {target.Level}, health {target.Health}/{target.MaximumHealth}, " +
            $"{HorizontalDistance(target.GlobalPosition, PlayerPosition):0.0} meters away. TAB cycles forward; SHIFT+TAB cycles backward.",
            false);
    }

    private void SetSelectedTarget(CombatCreature? target)
    {
        _selectedTargetId = target?.CreatureId;
        foreach (var creature in _creatures.Values)
        {
            creature.SetPlayerSelected(ReferenceEquals(creature, target));
        }
    }

    private CombatCreature? GetSelectedTarget()
    {
        if (_selectedTargetId is not Guid selectedId ||
            !_creatures.TryGetValue(selectedId, out var selected) ||
            !selected.IsAlive ||
            HorizontalDistance(selected.GlobalPosition, PlayerPosition) > TargetCycleRadius * 1.25f)
        {
            if (_selectedTargetId is not null)
            {
                SetSelectedTarget(null);
            }
            return null;
        }

        return selected;
    }

    private CombatCreature? FindAttackTarget(float range)
    {
        var selected = GetSelectedTarget();
        if (selected is not null)
        {
            return HorizontalDistance(selected.GlobalPosition, PlayerPosition) <= range
                ? selected
                : null;
        }

        return FindNearestTarget(range);
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private CombatCreature? FindNearestTarget(float range)
    {
        return _creatures.Values
            .Where(creature => creature.IsAlive &&
                               creature.GlobalPosition.DistanceTo(PlayerPosition) <= range)
            .OrderBy(creature => creature.GlobalPosition.DistanceSquaredTo(PlayerPosition))
            .FirstOrDefault();
    }

    private void UpdateTargetLabel()
    {
        if (!IsInstanceValid(_targetLabel))
        {
            return;
        }

        var selected = GetSelectedTarget();
        if (selected is not null)
        {
            _targetLabel.Text =
                $"◆  TARGET LOCKED  ◆\n{selected.CreatureName}  •  LEVEL {selected.Level}  •  HP {selected.Health}/{selected.MaximumHealth}\n" +
                $"{HorizontalDistance(selected.GlobalPosition, PlayerPosition):0.0} METERS  •  TAB TO CYCLE";
            _targetLabel.Modulate = new Color("f1b545");
            return;
        }

        var range = _player.IsRanger ? 18.0f : 2.8f;
        var target = FindNearestTarget(range);
        _targetLabel.Text = target is null
            ? "+"
            : $"+\n{target.CreatureName}  •  HP {target.Health}/{target.MaximumHealth}";
        _targetLabel.Modulate = target is null ? new Color("e7d4aa") : new Color("f1b545");
    }

    private void UpdatePlayerCombatReadiness()
    {
        if (!IsInstanceValid(_player) || !_player.CombatEnabled || _knockoutProtectionSeconds > 0)
        {
            _player?.SetCombatTarget(null);
            return;
        }

        var range = _player.IsRanger ? 18.0f : 2.8f;
        var target = FindAttackTarget(range);
        _player.SetCombatTarget(target?.GlobalPosition);
    }

    private void PlayWeaponAttack(CombatCreature target, Action onImpact)
    {
        _player.PlayCombatAttack(target.GlobalPosition);
        if (_player.IsRanger)
        {
            var targetPosition = target.GlobalPosition;
            var releaseTween = CreateTween();
            releaseTween.TweenInterval(0.40);
            releaseTween.TweenCallback(Callable.From(() =>
                ShowArrowProjectile(
                    IsInstanceValid(target) && target.IsAlive ? target.GlobalPosition : targetPosition,
                    onImpact)));
            return;
        }

        var tween = CreateTween();
        tween.TweenInterval(0.23);
        tween.TweenCallback(Callable.From(onImpact));
    }

    private void ShowArrowProjectile(Vector3 targetPosition, Action onImpact)
    {
        var impactPosition = targetPosition + new Vector3(0, 0.78f, 0);
        var startPosition = _player.GetRangedProjectileOrigin(impactPosition);
        var flight = impactPosition - startPosition;
        var distance = flight.Length();
        if (distance < 0.01f)
        {
            onImpact();
            return;
        }

        var arrow = new Node3D
        {
            Name = "ElaraArrowProjectile",
            GlobalPosition = startPosition,
            Quaternion = new Quaternion(Vector3.Up, flight / distance)
        };
        AddChild(arrow);
        arrow.AddChild(CreateMesh(
            "ArrowShaft",
            new CylinderMesh
            {
                TopRadius = 0.014f,
                BottomRadius = 0.014f,
                Height = 0.82f,
                RadialSegments = 8
            },
            Vector3.Zero,
            Vector3.Zero,
            new Color("8a572e")));
        arrow.AddChild(CreateMesh(
            "ArrowHead",
            new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = 0.038f,
                Height = 0.13f,
                RadialSegments = 8
            },
            new Vector3(0, 0.49f, 0),
            Vector3.Zero,
            new Color("aeb7bb"),
            metallic: 0.88f));
        arrow.AddChild(CreateMesh(
            "ArrowFletchingHorizontal",
            new BoxMesh { Size = new Vector3(0.075f, 0.15f, 0.012f) },
            new Vector3(0, -0.34f, 0),
            Vector3.Zero,
            Red));
        arrow.AddChild(CreateMesh(
            "ArrowFletchingVertical",
            new BoxMesh { Size = new Vector3(0.012f, 0.15f, 0.075f) },
            new Vector3(0, -0.34f, 0),
            Vector3.Zero,
            new Color("b68535")));

        var flightTime = Mathf.Clamp(distance / 26.0f, 0.16f, 0.65f);
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(arrow, "global_position", impactPosition, flightTime);
        tween.TweenCallback(Callable.From(onImpact));
        tween.TweenCallback(Callable.From(arrow.QueueFree));
    }

    private void BuildLightingAndSky()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("172331"),
            SkyHorizonColor = new Color("9b7751"),
            GroundBottomColor = new Color("10130f"),
            GroundHorizonColor = new Color("76684d"),
            SunAngleMax = 18.0f,
            SunCurve = 0.08f
        };
        var sky = new Sky { SkyMaterial = skyMaterial };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.82f,
            AmbientLightSkyContribution = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapAgxContrast = 1.08f,
            SsaoEnabled = true,
            SsaoRadius = 1.45f,
            SsaoIntensity = 2.1f,
            SsilEnabled = true,
            SsilIntensity = 0.75f,
            GlowEnabled = true,
            GlowIntensity = 0.32f,
            FogEnabled = true,
            FogLightColor = new Color("9a8060"),
            FogDensity = 0.0022f,
            FogSkyAffect = 0.28f
        };
        AddChild(new WorldEnvironment
        {
            Name = "ValleyEnvironment",
            Environment = environment
        });

        AddChild(new DirectionalLight3D
        {
            Name = "MorningSun",
            RotationDegrees = new Vector3(-46, -32, 0),
            LightColor = new Color("ffd59a"),
            LightEnergy = 1.7f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 280.0f
        });

        foreach (var (name, position, color, energy, range) in new[]
                 {
                     ("SquareLanternWest", new Vector3(-6.35f, 2.25f, -5.5f), new Color("ffad5a"), 2.2f, 8.0f),
                     ("SquareLanternEast", new Vector3(6.85f, 2.25f, -5.5f), new Color("ffad5a"), 2.2f, 8.0f),
                     ("VillageLanternWest", new Vector3(-5.15f, 2.25f, -20.2f), new Color("ff9d47"), 1.8f, 7.0f),
                     ("VillageLanternEast", new Vector3(5.85f, 2.25f, -20.5f), new Color("ff9d47"), 1.8f, 7.0f)
                 })
        {
            AddChild(new OmniLight3D
            {
                Name = name,
                Position = position,
                LightColor = color,
                LightEnergy = energy,
                OmniRange = range,
                ShadowEnabled = false
            });
        }
    }

    private void LoadStylizedEnvironment()
    {
        if (!ResourceLoader.Exists(StylizedEnvironmentScenePath))
        {
            GD.PushWarning($"Stylized Stonehaven environment was not found at {StylizedEnvironmentScenePath}. Using prototype visuals.");
            return;
        }

        var scene = GD.Load<PackedScene>(StylizedEnvironmentScenePath);
        var environment = scene?.Instantiate<Node3D>();
        if (environment is null)
        {
            GD.PushWarning("Stylized Stonehaven environment could not be instantiated. Using prototype visuals.");
            return;
        }

        environment.Name = "StonehavenStylizedEnvironment";
        HideLegacyStonehavenWallMeshes(environment);
        AddChild(environment);
        _stylizedEnvironmentRoot = environment;
        _stylizedEnvironmentLoaded = true;
    }

    private void ClearStylizedEnvironmentFootprint(
        Vector3 center,
        Vector2 halfExtents,
        bool hideLegacyRoads = false)
    {
        if (!IsInstanceValid(_stylizedEnvironmentRoot))
        {
            return;
        }

        HideEnvironmentFeaturesInsideFootprint(
            _stylizedEnvironmentRoot!,
            center,
            halfExtents,
            hideLegacyRoads);
    }

    private static void HideEnvironmentFeaturesInsideFootprint(
        Node node,
        Vector3 center,
        Vector2 halfExtents,
        bool hideLegacyRoads)
    {
        foreach (var child in node.GetChildren())
        {
            var childName = child.Name.ToString();
            if (child is GeometryInstance3D geometry &&
                (IsNatureDecorationName(childName) ||
                 hideLegacyRoads && IsRoadDecorationName(childName)) &&
                MathF.Abs(geometry.GlobalPosition.X - center.X) <= halfExtents.X &&
                MathF.Abs(geometry.GlobalPosition.Z - center.Z) <= halfExtents.Y)
            {
                geometry.Visible = false;
            }
            HideEnvironmentFeaturesInsideFootprint(child, center, halfExtents, hideLegacyRoads);
        }
    }

    private static bool IsRoadDecorationName(string name) =>
        name.Contains("Road", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Trail", StringComparison.OrdinalIgnoreCase);

    private static bool IsNatureDecorationName(string name) =>
        IsTreeDecorationName(name) ||
        name.StartsWith("Shrub_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("ShrubFlower_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("ValleyRock_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("ClusteredGrassFields", StringComparison.OrdinalIgnoreCase);

    private static bool IsTreeDecorationName(string name) =>
        name.StartsWith("Tree_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Pine_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Willow_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("DeadTree_", StringComparison.OrdinalIgnoreCase);

    private static void HideLegacyStonehavenWallMeshes(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is GeometryInstance3D geometry &&
                (child.Name.ToString().StartsWith("Wall_", StringComparison.OrdinalIgnoreCase) ||
                 IsNatureDecorationName(child.Name.ToString())))
            {
                geometry.Visible = false;
            }
            HideLegacyStonehavenWallMeshes(child);
        }
    }

    private void CreateWorldRegionRoots()
    {
        for (var physicalRow = 0; physicalRow < 3; physicalRow++)
        {
            for (var physicalColumn = 0; physicalColumn < 3; physicalColumn++)
            {
                var code = GetDisplayGridCode(physicalColumn, physicalRow);
                _worldRegionRoots[code] = new Node3D
                {
                    Name = $"Grid{code}-{WorldGridNames[physicalRow, physicalColumn].Replace(' ', '-')}"
                };
            }
        }
    }

    private void RefreshLoadedWorldRegions(Vector3 position, bool force = false)
    {
        var (physicalColumn, physicalRow) = GetPhysicalGridCell(position);
        var centerCode = GetDisplayGridCode(physicalColumn, physicalRow);
        if (!force && centerCode.Equals(_loadedRegionCenterCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _loadedRegionCenterCode = centerCode;
        var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (Math.Abs(column - physicalColumn) + Math.Abs(row - physicalRow) <= 1)
                {
                    desired.Add(GetDisplayGridCode(column, row));
                }
            }
        }

        foreach (var (code, root) in _worldRegionRoots)
        {
            if (desired.Contains(code))
            {
                if (root.GetParent() is null)
                {
                    AddChild(root);
                }
            }
            else if (root.GetParent() == this)
            {
                RemoveChild(root);
            }
        }
    }

    private void AddWorldNode(Node3D node, Vector3 position)
    {
        var code = GetWorldGrid(position).Code;
        if (_worldRegionRoots.TryGetValue(code, out var root))
        {
            root.AddChild(node);
            return;
        }

        AddChild(node);
    }

    private Node3D? InstantiateProductionEnvironmentAsset(string path, string name)
    {
        if (!_productionEnvironmentScenes.TryGetValue(path, out var packed))
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"Production environment asset was not found: {path}");
                return null;
            }

            packed = GD.Load<PackedScene>(path);
            if (packed is null)
            {
                GD.PushWarning($"Production environment asset could not be loaded: {path}");
                return null;
            }
            _productionEnvironmentScenes[path] = packed;
        }

        var instance = packed.Instantiate<Node3D>();
        instance.Name = name;
        return instance;
    }

    private bool AddProductionEnvironmentVisual(
        Node3D parent,
        string path,
        string name,
        Vector3 scale,
        float yaw = 0.0f,
        Vector3? localPosition = null)
    {
        var instance = InstantiateProductionEnvironmentAsset(path, name);
        if (instance is null)
        {
            return false;
        }

        instance.Position = localPosition ?? Vector3.Zero;
        instance.Rotation = new Vector3(0, yaw, 0);
        instance.Scale = scale;
        GroundProductionAsset(instance, (localPosition ?? Vector3.Zero).Y);
        parent.AddChild(instance);
        return true;
    }

    private static void GroundProductionAsset(Node3D instance, float groundY)
    {
        var lowest = FindLowestMeshPoint(instance, Transform3D.Identity);
        if (!float.IsFinite(lowest))
        {
            return;
        }
        instance.Position += Vector3.Up * (groundY - lowest);
    }

    private static float FindLowestMeshPoint(Node node, Transform3D inherited)
    {
        var transform = inherited;
        if (node is Node3D node3D)
        {
            transform *= node3D.Transform;
        }

        var lowest = float.PositiveInfinity;
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            var bounds = meshInstance.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0 : bounds.Size.X,
                    (corner & 2) == 0 ? 0 : bounds.Size.Y,
                    (corner & 4) == 0 ? 0 : bounds.Size.Z);
                lowest = MathF.Min(lowest, (transform * point).Y);
            }
        }

        foreach (var child in node.GetChildren())
        {
            lowest = MathF.Min(lowest, FindLowestMeshPoint(child, transform));
        }
        return lowest;
    }

    private Mesh? GetProductionEnvironmentMesh(string path)
    {
        if (_productionEnvironmentMeshes.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var instance = InstantiateProductionEnvironmentAsset(path, "MeshSource");
        if (instance is null)
        {
            return null;
        }

        var meshInstance = FindEnvironmentMeshInstance(instance);
        if (meshInstance?.Mesh is null)
        {
            instance.Free();
            GD.PushWarning($"Production environment asset contains no mesh: {path}");
            return null;
        }

        var mesh = meshInstance.Mesh;
        _productionEnvironmentMeshes[path] = mesh;
        instance.Free();
        return mesh;
    }

    private static MeshInstance3D? FindEnvironmentMeshInstance(Node node)
    {
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            return meshInstance;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindEnvironmentMeshInstance(child);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private void BuildValleyFloor()
    {
        for (var physicalRow = 0; physicalRow < 3; physicalRow++)
        {
            for (var physicalColumn = 0; physicalColumn < 3; physicalColumn++)
            {
                var center = new Vector3(
                    (physicalColumn - 1) * WorldGridSize,
                    -0.55f,
                    (physicalRow - 1) * WorldGridSize);
                var code = GetDisplayGridCode(physicalColumn, physicalRow);
                AddStaticBox($"Grid{code}Floor", center,
                    new Vector3(WorldGridSize, 1, WorldGridSize), new Color("2b3823"),
                    alwaysLoaded: true);
            }
        }

        if (!_stylizedEnvironmentLoaded)
        {
            for (var physicalColumn = 0; physicalColumn < 3; physicalColumn++)
            {
                for (var physicalRow = 0; physicalRow < 3; physicalRow++)
                {
                    var x = (physicalColumn - 1) * WorldGridSize;
                    var z = (physicalRow - 1) * WorldGridSize;
                    AddDecoration($"GridRoad-{physicalColumn}-{physicalRow}",
                        new BoxMesh { Size = new Vector3(5.2f, 0.04f, WorldGridSize) },
                        new Vector3(x, 0.02f, z), Vector3.Zero, new Color("624d35"));
                }

                var crossroadX = (physicalColumn - 1) * WorldGridSize;
                AddDecoration($"RealmCrossroad-{physicalColumn}",
                    new BoxMesh { Size = new Vector3(WorldGridSize, 0.04f, 5.2f) },
                    new Vector3(crossroadX, 0.018f, 12), Vector3.Zero, new Color("665139"));
                AddDecoration($"River-{physicalColumn}",
                    new BoxMesh { Size = new Vector3(WorldGridSize, 0.04f, 7.5f) },
                    new Vector3(crossroadX, 0.035f, 25), Vector3.Zero,
                    new Color(0.08f, 0.28f, 0.36f, 0.82f), transparency: true);
            }

            AddDecoration("VillageSquare", new CylinderMesh
            {
                TopRadius = 8.2f,
                BottomRadius = 8.2f,
                Height = 0.05f,
                RadialSegments = 24
            }, new Vector3(0, 0.025f, -13), Vector3.Zero, new Color("75654b"));
        }

        AddWorldBridge(-WorldGridSize, "West");
        AddWorldBridge(0, "Central");
        AddWorldBridge(WorldGridSize, "East");

        for (var index = -1; index <= 1; index++)
        {
            var offset = index * WorldGridSize;
            AddBoundaryHill($"WestRealmRidge-{index}", new Vector3(-145, 5, offset),
                new Vector3(10, 12, WorldGridSize));
            AddBoundaryHill($"EastRealmRidge-{index}", new Vector3(145, 5, offset),
                new Vector3(10, 12, WorldGridSize));
            AddBoundaryHill($"NorthRealmRidge-{index}", new Vector3(offset, 6, -145),
                new Vector3(WorldGridSize, 14, 10));
            AddBoundaryHill($"SouthRealmRidge-{index}", new Vector3(offset, 5, 145),
                new Vector3(WorldGridSize, 12, 10));
        }
    }

    private void AddWorldBridge(float x, string suffix)
    {
        AddStaticBox($"{suffix}Bridge", new Vector3(x, 0.28f, 25),
            new Vector3(6.5f, 0.5f, 9.5f), new Color("4e3421"));
        AddStaticRamp($"{suffix}BridgeSouthRamp", new Vector3(x, 0.13f, 19.25f),
            new Vector3(6.5f, 0.22f, 2.6f), -Mathf.DegToRad(13.0f), new Color("4e3421"));
        AddStaticRamp($"{suffix}BridgeNorthRamp", new Vector3(x, 0.13f, 30.75f),
            new Vector3(6.5f, 0.22f, 2.6f), Mathf.DegToRad(13.0f), new Color("4e3421"));
        AddInvisibleStaticBox($"{suffix}BridgeLeftRailCollision",
            new Vector3(x - 3.15f, 1.45f, 25), new Vector3(0.42f, 2.45f, 10.3f));
        AddInvisibleStaticBox($"{suffix}BridgeRightRailCollision",
            new Vector3(x + 3.15f, 1.45f, 25), new Vector3(0.42f, 2.45f, 10.3f));
        if (_stylizedEnvironmentLoaded)
        {
            return;
        }

        for (var z = 21; z <= 29; z += 2)
        {
            AddDecoration($"{suffix}BridgePlank{z}", new BoxMesh { Size = new Vector3(6.8f, 0.12f, 1.65f) },
                new Vector3(x, 0.58f, z), Vector3.Zero, new Color("76502f"));
        }
    }

    private void BuildStonehavenVillage()
    {
        AddStonehavenGate();
        AddStonehavenEastGate();
        AddHouse("Blacksmith", new Vector3(-11, 0, -13), new Color("59402a"));
        AddHouse("WayfarerInn", new Vector3(11, 0, -14), new Color("61422a"));
        AddHouse("Herbalist", new Vector3(-12, 0, -26), new Color("4b3926"));
        AddHouse("Storehouse", new Vector3(12, 0, -27), new Color("523823"));

        AddStaticBox("WellBase", new Vector3(0, 0.55f, -13), new Vector3(2.7f, 1.1f, 2.7f), new Color("55575a"));
        AddDecoration("WellOpening", new CylinderMesh
        {
            TopRadius = 1.05f,
            BottomRadius = 1.05f,
            Height = 0.22f,
            RadialSegments = 14
        }, new Vector3(0, 1.2f, -13), Vector3.Zero, new Color("181b1e"));
        AddDecoration("WellRoof", new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = 2.2f,
            Height = 1.0f,
            RadialSegments = 8
        }, new Vector3(0, 3.25f, -13), Vector3.Zero, new Color("4f251b"));
        AddDecoration("WellPostLeft", new BoxMesh { Size = new Vector3(0.18f, 2.4f, 0.18f) },
            new Vector3(-1.15f, 2.1f, -13), Vector3.Zero, new Color("4a2e1c"));
        AddDecoration("WellPostRight", new BoxMesh { Size = new Vector3(0.18f, 2.4f, 0.18f) },
            new Vector3(1.15f, 2.1f, -13), Vector3.Zero, new Color("4a2e1c"));

        AddLabel("StonehavenLabel", "STONEHAVEN", new Vector3(0, 5.8f, 1.5f), 58, Gold);
        AddLabel("BlacksmithLabel", "BLACKSMITH", new Vector3(-11, 3.25f, -9.9f), 30, Parchment);
        AddLabel("InnLabel", "WAYFARER INN", new Vector3(11, 3.25f, -10.9f), 30, Parchment);

        AddTree(new Vector3(-18, 0, -5), 1.15f);
        AddTree(new Vector3(18, 0, -6), 1.2f);
        AddTree(new Vector3(-20, 0, -33), 1.3f);
        AddTree(new Vector3(20, 0, -34), 1.2f);
    }

    private void BuildOutskirts()
    {
        var treePositions = new[]
        {
            new Vector3(-34, 0, 35), new Vector3(-27, 0, 29), new Vector3(-38, 0, 18),
            new Vector3(-31, 0, 8), new Vector3(-37, 0, -8), new Vector3(-32, 0, -22),
            new Vector3(-36, 0, -36), new Vector3(34, 0, 36), new Vector3(27, 0, 31),
            new Vector3(38, 0, 18), new Vector3(32, 0, 6), new Vector3(37, 0, -8),
            new Vector3(31, 0, -23), new Vector3(37, 0, -36), new Vector3(-17, 0, 37),
            new Vector3(17, 0, 38), new Vector3(-24, 0, -41), new Vector3(24, 0, -40)
        };
        for (var index = 0; index < treePositions.Length; index++)
        {
            AddTree(treePositions[index], 0.9f + (index % 4) * 0.12f);
        }

        AddRock(new Vector3(-8, 0, 34), new Vector3(2.4f, 1.6f, 1.8f));
        AddRock(new Vector3(10, 0, 37), new Vector3(1.8f, 1.2f, 2.2f));
        AddRock(new Vector3(-27, 0, 14), new Vector3(2.2f, 1.4f, 1.5f));
        AddRock(new Vector3(28, 0, 11), new Vector3(2.0f, 1.3f, 2.6f));
        AddRock(new Vector3(-24, 0, -35), new Vector3(2.7f, 1.8f, 2.0f));

        AddLabel("DarkwoodSign", "DARKWOOD TRAIL", new Vector3(-20, 2.4f, 17), 28, new Color("d4b56b"));
        AddDecoration("SignPost", new BoxMesh { Size = new Vector3(0.18f, 2.4f, 0.18f) },
            new Vector3(-20, 1.2f, 17), Vector3.Zero, new Color("432b19"));

        _darkwoodCamp = new Node3D
        {
            Name = "DarkwoodClanCamp",
            Position = DarkwoodCampCenter
        };
        AddWorldNode(_darkwoodCamp, DarkwoodCampCenter);
        ApplyDarkwoodCampStage(1);
    }

    private void BuildRequestedRegionalFeatures()
    {
        BuildMirrorwaterLake();
        BuildIronMiningDistrict();
        BuildStonehavenFarmlands();
        BuildCreatureTestingGrounds();
        BuildWillowmereDragonRoost();
    }

    private void BuildMirrorwaterLake()
    {
        ClearStylizedEnvironmentFootprint(MirrorwaterLakeCenter, new Vector2(39, 32), hideLegacyRoads: true);

        var shore = CreateMesh("MirrorwaterShore", new CylinderMesh
        {
            TopRadius = 1,
            BottomRadius = 1,
            Height = 0.16f,
            RadialSegments = 64
        }, MirrorwaterLakeCenter + new Vector3(0, 0.02f, 0), Vector3.Zero, new Color("655943"));
        shore.Scale = new Vector3(35, 1, 27);
        AddWorldNode(shore, MirrorwaterLakeCenter);

        var water = CreateMesh("MirrorwaterLake", new CylinderMesh
        {
            TopRadius = 1,
            BottomRadius = 1,
            Height = 0.08f,
            RadialSegments = 64
        }, MirrorwaterLakeCenter + new Vector3(0, 0.11f, 0), Vector3.Zero,
            new Color(0.06f, 0.27f, 0.38f, 0.88f), transparency: true, metallic: 0.15f);
        water.Scale = new Vector3(32, 1, 24);
        AddWorldNode(water, MirrorwaterLakeCenter);

        var deepWater = CreateMesh("MirrorwaterDeepWater", new CylinderMesh
        {
            TopRadius = 1,
            BottomRadius = 1,
            Height = 0.045f,
            RadialSegments = 64
        }, MirrorwaterLakeCenter + new Vector3(0, 0.135f, 0), Vector3.Zero,
            new Color(0.025f, 0.15f, 0.24f, 0.42f), transparency: true, metallic: 0.10f);
        deepWater.Scale = new Vector3(22, 1, 16);
        AddWorldNode(deepWater, MirrorwaterLakeCenter);
        for (var ripple = 0; ripple < 6; ripple++)
        {
            var ripplePosition = MirrorwaterLakeCenter + new Vector3(
                -17.0f + ripple * 6.1f,
                0.175f,
                -8.0f + ripple % 3 * 7.0f);
            var ring = CreateMesh($"MirrorwaterRipple{ripple + 1}", new TorusMesh
            {
                InnerRadius = 0.62f,
                OuterRadius = 0.69f,
                Rings = 24,
                RingSegments = 6
            }, ripplePosition, Vector3.Zero,
                new Color(0.55f, 0.78f, 0.82f, 0.36f), transparency: true);
            ring.Scale = new Vector3(1.0f + ripple * 0.08f, 1, 0.62f + ripple % 2 * 0.08f);
            AddWorldNode(ring, ripplePosition);
        }

        // Water is intentionally non-blocking in this playtest. The previous
        // invisible deep-water boxes trapped the player and collapsed the
        // third-person camera against the character's head.
        AddFeatureRoadPath(
            "MirrorwaterDockRoad",
            [
                new Vector3(68.0f, 0.07f, -20.0f),
                new Vector3(62.0f, 0.07f, -19.5f),
                new Vector3(56.0f, 0.07f, -16.0f),
                new Vector3(51.0f, 0.07f, -11.0f),
                new Vector3(45.0f, 0.07f, -7.5f),
                new Vector3(34.0f, 0.07f, -7.0f),
                new Vector3(27.0f, 0.07f, -7.0f)
            ],
            4.6f,
            new Color("70573a"));

        for (var index = 0; index < 14; index++)
        {
            var x = 70.0f + index * 2.0f;
            AddFeatureStaticBox($"MirrorwaterDockPlank{index}", new Vector3(x, 0.38f, -20.0f),
                new Vector3(1.82f, 0.32f, 5.0f), new Color(index % 2 == 0 ? "76502f" : "684329"));
        }
        foreach (var x in new[] { 70.0f, 78.0f, 86.0f, 94.0f })
        {
            AddFeatureStaticBox($"MirrorwaterDockPostNorth{x}", new Vector3(x, 0.25f, -22.75f),
                new Vector3(0.32f, 2.0f, 0.32f), new Color("49301e"));
            AddFeatureStaticBox($"MirrorwaterDockPostSouth{x}", new Vector3(x, 0.25f, -17.25f),
                new Vector3(0.32f, 2.0f, 0.32f), new Color("49301e"));
        }

        AddLabel("MirrorwaterLakeLabel", "A2  •  MIRRORWATER LAKE",
            MirrorwaterLakeCenter + new Vector3(0, 4.2f, -25), 42, new Color("82c5d3"));
        AddLabel("MirrorwaterDockLabel", "STONEHAVEN LAKE DOCK",
            new Vector3(82, 3.0f, -20.0f), 24, Gold);
        AddLabel("A2RoadSign", "A2  LAKE & DOCK", new Vector3(51, 2.6f, -7), 24, Gold);

        AddFeatureStaticBox("MirrorwaterFishingHut", new Vector3(66.5f, 1.35f, -27.0f),
            new Vector3(5.2f, 2.7f, 4.4f), new Color("68513a"));
        AddFeatureDecoration("MirrorwaterFishingHutRoof", new CylinderMesh
        {
            TopRadius = 0,
            BottomRadius = 3.7f,
            Height = 1.9f,
            RadialSegments = 4
        }, new Vector3(66.5f, 3.6f, -27.0f), new Vector3(0, Mathf.DegToRad(45), 0),
            new Color("45231a"));
        BuildMirrorwaterFishingBoat();
    }

    private void BuildMirrorwaterFishingBoat()
    {
        var center = new Vector3(87.5f, 0.34f, -24.3f);
        var yaw = Mathf.DegToRad(7.0f);
        AddFeatureStaticBox(
            "MirrorwaterFishingSkiffHull",
            center,
            new Vector3(4.6f, 0.62f, 1.65f),
            new Color("4b2d1c"));
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            AddFeatureDecoration(
                $"MirrorwaterFishingSkiffGunwale{side}",
                new BoxMesh { Size = new Vector3(4.8f, 0.22f, 0.18f) },
                center + new Vector3(0, 0.46f, side * 0.78f),
                new Vector3(0, yaw, side * Mathf.DegToRad(8.0f)),
                new Color("76502f"));
        }
        foreach (var x in new[] { -1.15f, 0.25f, 1.45f })
        {
            AddFeatureDecoration(
                $"MirrorwaterFishingSkiffSeat{x}",
                new BoxMesh { Size = new Vector3(0.28f, 0.14f, 1.32f) },
                center + new Vector3(x, 0.58f, 0),
                new Vector3(0, yaw, 0),
                new Color("8b6339"));
        }
        AddLabel(
            "MirrorwaterFishingSkiffLabel",
            "MIRRORWATER FISHING SKIFF",
            center + new Vector3(0, 2.0f, 0),
            19,
            new Color("d7be73"));
    }

    private void BuildCreatureTestingGrounds()
    {
        ClearStylizedEnvironmentFootprint(
            CreatureTestingGroundsCenter,
            new Vector2(39, 37),
            hideLegacyRoads: true);

        var yard = CreateMesh(
            "CreatureTestingGroundsSurface",
            new CylinderMesh
            {
                TopRadius = 1,
                BottomRadius = 1,
                Height = 0.08f,
                RadialSegments = 64
            },
            CreatureTestingGroundsCenter + new Vector3(0, 0.04f, 0),
            Vector3.Zero,
            new Color("594831"));
        yard.Scale = new Vector3(31.0f, 1, 26.0f);
        AddWorldNode(yard, CreatureTestingGroundsCenter);

        var spawnPositions = new[]
        {
            new Vector3(76, 0.10f, 68),
            new Vector3(92, 0.10f, 78),
            new Vector3(110, 0.10f, 72),
            new Vector3(84, 0.10f, 101),
            new Vector3(111, 0.10f, 105)
        };
        for (var index = 0; index < spawnPositions.Length; index++)
        {
            AddFeatureDecoration(
                $"TrainingCreatureSpawn{index + 1}",
                new CylinderMesh
                {
                    TopRadius = 1.55f,
                    BottomRadius = 1.55f,
                    Height = 0.06f,
                    RadialSegments = 24
                },
                spawnPositions[index],
                Vector3.Zero,
                index < 3 ? new Color("79623c") : new Color("6b5435"));
        }

        AddFeatureRoadPath(
            "CreatureGroundsRoad",
            [
                new Vector3(96, 0.08f, 49),
                new Vector3(96.8f, 0.08f, 61),
                new Vector3(95.5f, 0.08f, 73),
                new Vector3(96, 0.08f, 88)
            ],
            4.4f,
            new Color("70573a"));
        AddLabel(
            "CreatureTestingGroundsLabel",
            "A1  •  CREATURE TESTING GROUNDS",
            CreatureTestingGroundsCenter + new Vector3(0, 5.0f, -27.0f),
            40,
            new Color("d7be73"));
        AddLabel(
            "CreatureTestingGroundsInstruction",
            "TRAINING CREATURES RESPAWN INSIDE THE MARKED YARD",
            CreatureTestingGroundsCenter + new Vector3(0, 3.2f, -23.5f),
            21,
            Parchment);
    }

    private void BuildWillowmereDragonRoost()
    {
        ClearStylizedEnvironmentFootprint(
            WillowmereDragonRoostCenter,
            new Vector2(24.0f, 22.0f),
            hideLegacyRoads: false);
        HideStylizedEnvironmentInsideFootprint(
            WillowmereDragonRoostCenter,
            new Vector2(24.0f, 22.0f));

        var roost = CreateMesh(
            "WillowmereDragonRoostSurface",
            new CylinderMesh
            {
                TopRadius = 1,
                BottomRadius = 1,
                Height = 0.16f,
                RadialSegments = 48
            },
            WillowmereDragonRoostCenter + new Vector3(0, 0.08f, 0),
            Vector3.Zero,
            new Color("343330"));
        roost.Scale = new Vector3(18.5f, 1, 15.5f);
        AddWorldNode(roost, WillowmereDragonRoostCenter);

        AddFeatureRoadPath(
            "WillowmereDragonRoostPath",
            [
                new Vector3(-96.0f, 0.08f, 70.0f),
                new Vector3(-101.0f, 0.08f, 79.0f),
                new Vector3(-106.0f, 0.08f, 89.0f),
                WillowmereDragonRoostCenter + new Vector3(0, 0.08f, -12.0f)
            ],
            3.8f,
            new Color("5d4b3b"));

        for (var index = 0; index < 11; index++)
        {
            var angle = index * Mathf.Tau / 11.0f;
            var radius = index % 2 == 0 ? 17.2f : 18.8f;
            var boulder = CreateMesh(
                $"DragonRoostBoulder{index + 1}",
                new SphereMesh
                {
                    Radius = 1.0f,
                    Height = 2.0f,
                    RadialSegments = 10,
                    Rings = 5
                },
                WillowmereDragonRoostCenter + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.55f,
                    Mathf.Sin(angle) * radius),
                new Vector3(0.08f * index, angle, 0.06f * (index % 3)),
                index % 2 == 0 ? new Color("4a4a47") : new Color("3e403e"));
            boulder.Scale = new Vector3(
                1.3f + 0.15f * (index % 3),
                0.8f + 0.10f * (index % 2),
                1.1f + 0.12f * ((index + 1) % 3));
            AddWorldNode(boulder, boulder.Position);
        }

        AddLabel(
            "WillowmereDragonRoostLabel",
            "C1  -  WILLOWMERE DRAGON ROOST\nEMBERWING AND NIGHTVEIL  -  NON-HOSTILE",
            WillowmereDragonRoostCenter + new Vector3(0, 6.2f, -9.0f),
            30,
            new Color("d7be73"));
        AddLabel(
            "WillowmereDragonRoamingNotice",
            "RED AND BLACK DRAGONS ROAM FREELY THROUGH ALL NINE GRIDS",
            WillowmereDragonRoostCenter + new Vector3(0, 4.7f, -9.0f),
            20,
            new Color("d66a4a"));
        AddLabel(
            "WillowmereDragonCredit",
            "BGE DRAGON 2.0 BY 3DHAUPT  -  CC-BY",
            WillowmereDragonRoostCenter + new Vector3(0, 3.7f, -9.0f),
            18,
            Parchment);
    }

    private void SpawnRoamingDragons()
    {
        var dragonScene = GD.Load<PackedScene>(WillowmereDragonScenePath);
        if (dragonScene is null)
        {
            GD.PushWarning($"Unable to load the Willowmere dragon model: {WillowmereDragonScenePath}");
            return;
        }

        AddRoamingDragon(
            dragonScene,
            "EmberwingRedDragon",
            "Emberwing  -  Red Dragon",
            new Color("8f1815"),
            new Color("ef6652"),
            WillowmereDragonRoostCenter + new Vector3(-5.5f, 0.12f, 2.0f),
            seed: 41027,
            initialTravelMode: 0,
            initialAnchor: 0,
            reverseRoute: false);
        AddRoamingDragon(
            dragonScene,
            "NightveilBlackDragon",
            "Nightveil  -  Black Dragon",
            new Color("11131a"),
            new Color("a9b4ca"),
            WillowmereDragonRoostCenter + new Vector3(6.0f, 0.12f, 0.0f),
            seed: 73141,
            initialTravelMode: 1,
            initialAnchor: DragonRoamingAnchors.Length - 1,
            reverseRoute: true);
    }

    private void AddRoamingDragon(
        PackedScene dragonScene,
        string nodeName,
        string displayName,
        Color bodyColor,
        Color labelColor,
        Vector3 spawn,
        int seed,
        int initialTravelMode,
        int initialAnchor,
        bool reverseRoute)
    {
        var model = dragonScene.Instantiate<Node3D>();
        TuneDragonMaterials(model, bodyColor);
        var dragon = new RoamingDragon
        {
            Name = nodeName,
            Position = spawn,
            Rotation = new Vector3(0, Mathf.DegToRad(24.0f), 0)
        };
        dragon.Configure(
            _pathfinder,
            model,
            displayName,
            labelColor,
            DragonRoamingAnchors,
            seed,
            initialTravelMode,
            initialAnchor,
            reverseRoute,
            () => PlayerPosition,
            () => _roamingDragons
                .Where(candidate => IsInstanceValid(candidate) && candidate.IsInsideTree())
                .Select(candidate => candidate.GlobalPosition)
                .ToArray());

        // Roaming dragons stay directly under the valley instead of a streamed
        // grid root, allowing them to cross grid boundaries without vanishing.
        AddChild(dragon);
        _roamingDragons.Add(dragon);
    }

    private void HideStylizedEnvironmentInsideFootprint(Vector3 center, Vector2 halfExtents)
    {
        if (!IsInstanceValid(_stylizedEnvironmentRoot))
        {
            return;
        }

        HideStylizedEnvironmentInsideFootprint(
            _stylizedEnvironmentRoot!,
            center,
            halfExtents);
    }

    private static void HideStylizedEnvironmentInsideFootprint(
        Node node,
        Vector3 center,
        Vector2 halfExtents)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is GeometryInstance3D geometry &&
                MathF.Abs(geometry.GlobalPosition.X - center.X) <= halfExtents.X &&
                MathF.Abs(geometry.GlobalPosition.Z - center.Z) <= halfExtents.Y)
            {
                geometry.Visible = false;
            }
            HideStylizedEnvironmentInsideFootprint(child, center, halfExtents);
        }
    }

    private static void TuneDragonMaterials(Node node, Color bodyColor)
    {
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            for (var surface = 0; surface < meshInstance.Mesh.GetSurfaceCount(); surface++)
            {
                if (meshInstance.GetActiveMaterial(surface) is not StandardMaterial3D source)
                {
                    continue;
                }

                var material = (StandardMaterial3D)source.Duplicate();
                var luminance = bodyColor.R * 0.2126f +
                                bodyColor.G * 0.7152f +
                                bodyColor.B * 0.0722f;
                material.AlbedoColor = bodyColor;
                material.Roughness = luminance < 0.12f ? 0.62f : 0.72f;
                material.Metallic = luminance < 0.12f ? 0.18f : 0.04f;
                meshInstance.SetSurfaceOverrideMaterial(surface, material);
            }
        }

        foreach (var child in node.GetChildren())
        {
            TuneDragonMaterials(child, bodyColor);
        }
    }

    private void BuildIronMiningDistrict()
    {
        ClearStylizedEnvironmentFootprint(IronMineCenter, new Vector2(43, 42), hideLegacyRoads: true);

        var workYard = CreateMesh("IrondeepRockyWorkYard", new CylinderMesh
        {
            TopRadius = 1,
            BottomRadius = 1,
            Height = 0.10f,
            RadialSegments = 32
        }, IronMineCenter + new Vector3(0, 0.05f, 2), Vector3.Zero, new Color("514436"));
        workYard.Scale = new Vector3(28, 1, 23);
        AddWorldNode(workYard, IronMineCenter);
        AddFeatureRoadPath(
            "IrondeepRoad",
            [
                new Vector3(29.0f, 0.08f, -7.0f),
                new Vector3(36.0f, 0.08f, -20.0f),
                new Vector3(44.0f, 0.08f, -35.0f),
                new Vector3(56.0f, 0.08f, -48.0f),
                new Vector3(72.0f, 0.08f, -60.0f),
                new Vector3(88.0f, 0.08f, -71.0f),
                new Vector3(102.0f, 0.08f, -82.0f),
                new Vector3(104.0f, 0.08f, -88.0f)
            ],
            5.2f,
            new Color("63513e"));

        AddMineRockFormation("IrondeepRockWestOuter", new Vector3(82, 4.0f, -126),
            new Vector3(14, 8, 10), new Color("454743"));
        AddMineRockFormation("IrondeepRockWest", new Vector3(93, 5.0f, -130),
            new Vector3(12, 10, 11), new Color("3d403d"));
        AddMineRockFormation("IrondeepRockWestPortal", new Vector3(101, 3.7f, -128),
            new Vector3(7, 7.4f, 8), new Color("464844"));
        AddMineRockFormation("IrondeepRockEastPortal", new Vector3(115, 4.0f, -128),
            new Vector3(7, 8, 8), new Color("41433f"));
        AddMineRockFormation("IrondeepRockEast", new Vector3(124, 5.2f, -130),
            new Vector3(12, 10.4f, 11), new Color("393c39"));
        AddMineRockFormation("IrondeepRockEastOuter", new Vector3(136, 4.1f, -126),
            new Vector3(14, 8.2f, 10), new Color("424440"));

        AddFeatureDecoration("IrondeepMineMouth", new CylinderMesh
        {
            TopRadius = 4.7f,
            BottomRadius = 4.7f,
            Height = 0.35f,
            RadialSegments = 32
        }, new Vector3(108, 3.6f, -124.15f), new Vector3(Mathf.Pi / 2, 0, 0),
            new Color("07090a"));
        AddFeatureStaticBox("IrondeepMineLintel", new Vector3(108, 6.6f, -122.8f),
            new Vector3(11, 0.65f, 0.65f), new Color("5a3821"));

        foreach (var x in new[] { 103.5f, 112.5f })
        {
            AddFeatureStaticBox($"IrondeepSupport{x}", new Vector3(x, 3.25f, -122.8f),
                new Vector3(0.42f, 5.0f, 0.42f), new Color("5a3821"));
        }

        for (var index = 0; index < 8; index++)
        {
            var z = -116.0f + index * 7.5f;
            AddFeatureDecoration($"IrondeepRailLeft{index}", new BoxMesh { Size = new Vector3(0.16f, 0.08f, 7.0f) },
                new Vector3(106.5f, 0.16f, z), Vector3.Zero, new Color("343536"), metallic: 0.75f);
            AddFeatureDecoration($"IrondeepRailRight{index}", new BoxMesh { Size = new Vector3(0.16f, 0.08f, 7.0f) },
                new Vector3(109.5f, 0.16f, z), Vector3.Zero, new Color("343536"), metallic: 0.75f);
            AddFeatureDecoration($"IrondeepRailTie{index}", new BoxMesh { Size = new Vector3(4.2f, 0.12f, 0.35f) },
                new Vector3(108, 0.12f, z), Vector3.Zero, new Color("56361f"));
        }

        AddMineCart("IrondeepOreCart", new Vector3(108, 0.55f, -88));
        AddIrondeepOreSortingStation();
        AddFeatureStaticBox("IrondeepOreBin", new Vector3(126, 0.65f, -91),
            new Vector3(5.5f, 1.3f, 4.2f), new Color("493321"));

        foreach (var orePosition in new[]
                 {
                     new Vector3(87, 0.8f, -101), new Vector3(91, 0.6f, -112),
                     new Vector3(122, 0.9f, -97), new Vector3(128, 0.7f, -110)
                 })
        {
            var ore = CreateMesh("IronOreOutcrop", new SphereMesh
            {
                Radius = 1.0f,
                Height = 2.0f,
                RadialSegments = 10,
                Rings = 5
            }, orePosition, new Vector3(0.1f, orePosition.X * 0.03f, 0.08f),
                new Color("494743"), metallic: 0.45f);
            ore.Scale = new Vector3(1.8f, 0.8f, 1.35f);
            AddWorldNode(ore, orePosition);
        }
        foreach (var veinPosition in new[]
                 {
                     new Vector3(99.6f, 3.8f, -123.8f),
                     new Vector3(116.3f, 4.7f, -123.8f)
                 })
        {
            var vein = CreateMesh("IrondeepOreVein", new SphereMesh
            {
                Radius = 0.7f,
                Height = 1.4f,
                RadialSegments = 8,
                Rings = 4
            }, veinPosition, new Vector3(0.2f, 0.1f, 0.25f),
                new Color("8f4d31"), metallic: 0.7f);
            vein.Scale = new Vector3(1.2f, 0.5f, 0.24f);
            AddWorldNode(vein, veinPosition);
        }

        AddLabel("IrondeepLabel", "A3  •  IRONDEEP QUARRY & MINE",
            IronMineCenter + new Vector3(0, 7.0f, -19), 40, new Color("c2a36f"));
        AddLabel("A3RoadSign", "A3  QUARRY & IRON MINE", new Vector3(104, 2.8f, -51), 24, Gold);
    }

    private void AddIrondeepOreSortingStation()
    {
        var center = new Vector3(89, 0, -91);
        AddFeatureDecoration("IrondeepSortingStonePad", new BoxMesh { Size = new Vector3(7.4f, 0.16f, 5.4f) },
            center + new Vector3(0, 0.09f, 0), Vector3.Zero, new Color("5c5d59"));
        for (var bin = -1; bin <= 1; bin++)
        {
            var x = center.X + bin * 2.1f;
            AddFeatureStaticBox($"IrondeepSortingBin{bin}", new Vector3(x, 0.48f, center.Z + 1.25f),
                new Vector3(1.75f, 0.82f, 1.7f), new Color(bin == 0 ? "555754" : "62635f"));
            var ore = CreateMesh($"IrondeepSortedOre{bin}", new SphereMesh
            {
                Radius = 0.48f,
                Height = 0.72f,
                RadialSegments = 9,
                Rings = 5
            }, new Vector3(x, 1.05f, center.Z + 1.25f), new Vector3(0.1f, bin * 0.4f, 0.08f),
                bin == 0 ? new Color("8f4d31") : new Color("474946"), metallic: 0.45f);
            ore.Scale = new Vector3(1.1f, 0.7f, 0.9f);
            AddWorldNode(ore, ore.Position);
        }
        AddFeatureDecoration("IrondeepCrusherBase", new CylinderMesh
        {
            TopRadius = 0.95f,
            BottomRadius = 1.15f,
            Height = 1.2f,
            RadialSegments = 12
        }, center + new Vector3(0, 0.68f, -1.4f), Vector3.Zero, new Color("484a48"));
        AddFeatureDecoration("IrondeepCrusherWheel", new CylinderMesh
        {
            TopRadius = 0.78f,
            BottomRadius = 0.78f,
            Height = 0.22f,
            RadialSegments = 16
        }, center + new Vector3(0.9f, 1.35f, -1.4f), new Vector3(0, 0, Mathf.Pi * 0.5f),
            new Color("303334"), metallic: 0.72f);
    }

    private void AddMineRockFormation(string name, Vector3 position, Vector3 size, Color color)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0.06f, position.X * 0.037f, 0.04f),
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size * new Vector3(0.72f, 0.78f, 0.70f) }
        });
        var mesh = CreateMesh(name + "Mesh", new SphereMesh
        {
            Radius = 1,
            Height = 2,
            RadialSegments = 12,
            Rings = 6
        }, Vector3.Zero, Vector3.Zero, color);
        mesh.Scale = size * new Vector3(0.50f, 0.50f, 0.50f);
        body.AddChild(mesh);
        AddWorldNode(body, position);
        _pathObstacles.Add(WorldPathObstacle.FromRotatedBox(
            position,
            size * new Vector3(0.72f, 0.78f, 0.70f),
            body.Rotation.Y));
    }

    private void AddMineCart(string name, Vector3 position)
    {
        AddFeatureStaticBox(name + "Body", position + new Vector3(0, 0.45f, 0),
            new Vector3(3.0f, 1.1f, 2.1f), new Color("4a3020"));
        AddFeatureDecoration(name + "IronRim", new BoxMesh { Size = new Vector3(3.15f, 0.14f, 2.25f) },
            position + new Vector3(0, 1.02f, 0), Vector3.Zero, new Color("303233"), metallic: 0.75f);
        foreach (var x in new[] { -1.05f, 1.05f })
        {
            foreach (var z in new[] { -1.14f, 1.14f })
            {
                AddFeatureDecoration($"{name}Wheel{x}{z}", new CylinderMesh
                {
                    TopRadius = 0.38f,
                    BottomRadius = 0.38f,
                    Height = 0.18f,
                    RadialSegments = 12
                }, position + new Vector3(x, 0.24f, z), new Vector3(Mathf.Pi / 2, 0, 0),
                    new Color("252727"), metallic: 0.7f);
            }
        }
    }

    private void BuildStonehavenFarmlands()
    {
        ClearStylizedEnvironmentFootprint(
            StonehavenFarmlandCenter,
            new Vector2(45, 45),
            hideLegacyRoads: true);

        var plotIndex = 0;
        foreach (var z in new[] { 76.0f, 101.0f })
        {
            foreach (var x in new[] { -30.0f, -11.0f, 11.0f, 30.0f })
            {
                plotIndex++;
                var soilColor = plotIndex % 2 == 0 ? new Color("60442d") : new Color("563b28");
                AddFeatureDecoration($"FarmPlot{plotIndex}", new BoxMesh { Size = new Vector3(16, 0.10f, 19) },
                    new Vector3(x, 0.08f, z), Vector3.Zero, soilColor);
                for (var row = -3; row <= 3; row++)
                {
                    AddFeatureDecoration($"FarmPlot{plotIndex}CropRow{row}",
                        new BoxMesh { Size = new Vector3(14.5f, 0.12f, 0.62f) },
                        new Vector3(x, 0.17f, z + row * 2.15f), Vector3.Zero,
                        new Color("4b3322"));
                }
                AddFarmCropField(plotIndex, new Vector3(x, 0, z));
            }
        }

        AddRegionalHouse("WestFarmhouse", new Vector3(-29, 0, 128), new Color("6c563b"), Vector3.Right);
        AddRegionalHouse("EastFarmhouse", new Vector3(29, 0, 128), new Color("705239"), Vector3.Left);
        AddFeatureRoadPath(
            "FarmlandRoad",
            [
                new Vector3(0, 0.08f, 31),
                new Vector3(0.8f, 0.08f, 49),
                new Vector3(-0.7f, 0.08f, 68),
                new Vector3(0.6f, 0.08f, 88),
                new Vector3(-0.8f, 0.08f, 108),
                new Vector3(0, 0.08f, 126),
                new Vector3(0, 0.08f, 141)
            ],
            5.0f,
            new Color("70573a"));
        AddFeatureRoadPath(
            "WestFarmhouseLane",
            [new Vector3(0, 0.075f, 124), new Vector3(-13, 0.075f, 125), new Vector3(-25, 0.075f, 128)],
            3.2f,
            new Color("675036"));
        AddFeatureRoadPath(
            "EastFarmhouseLane",
            [new Vector3(0, 0.075f, 124), new Vector3(13, 0.075f, 125), new Vector3(25, 0.075f, 128)],
            3.2f,
            new Color("675036"));

        AddLabel("StonehavenFarmlandLabel", "B1  •  STONEHAVEN FARMLANDS",
            StonehavenFarmlandCenter + new Vector3(0, 5.0f, -34), 40, new Color("d7be73"));
        AddLabel("B1RoadSign", "B1  FARMS & HOMESTEADS", new Vector3(0, 2.8f, 51), 24, Gold);
    }

    private void AddFarmCropField(int plotIndex, Vector3 center)
    {
        var cropColor = (plotIndex % 3) switch
        {
            0 => new Color("b9a044"),
            1 => new Color("58763a"),
            _ => new Color("708a3f")
        };
        var stalkMesh = new CylinderMesh
        {
            TopRadius = 0.018f,
            BottomRadius = 0.030f,
            Height = 0.58f,
            RadialSegments = 6,
            Material = new StandardMaterial3D { AlbedoColor = cropColor.Darkened(0.15f), Roughness = 0.9f }
        };
        var crownMesh = new SphereMesh
        {
            Radius = 0.10f,
            Height = 0.22f,
            RadialSegments = 7,
            Rings = 4,
            Material = new StandardMaterial3D { AlbedoColor = cropColor, Roughness = 0.92f }
        };
        var stalks = new List<Transform3D>();
        var crowns = new List<Transform3D>();
        for (var row = -3; row <= 3; row++)
        {
            for (var plant = -6; plant <= 6; plant++)
            {
                var jitter = NatureHash01(plotIndex, row, plant + 6, 8120) - 0.5f;
                var height = 0.82f + NatureHash01(plotIndex, row, plant + 6, 8121) * 0.24f;
                var position = center + new Vector3(plant * 1.05f + jitter * 0.16f, 0.24f, row * 2.15f);
                stalks.Add(new Transform3D(
                    new Basis(Vector3.Up, jitter * 0.18f).Scaled(new Vector3(1, height, 1)),
                    position));
                crowns.Add(new Transform3D(
                    new Basis(Vector3.Up, jitter * 0.28f).Scaled(new Vector3(1.3f, height, 0.85f)),
                    position + new Vector3(0, 0.34f * height, 0)));
            }
        }
        AddFarmCropMultiMesh($"FarmPlot{plotIndex}Stalks", stalkMesh, stalks, center);
        AddFarmCropMultiMesh($"FarmPlot{plotIndex}Crops", crownMesh, crowns, center);
    }

    private void AddFarmCropMultiMesh(string name, Mesh mesh, List<Transform3D> transforms, Vector3 center)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var crops = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = 86.0f,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
        };
        AddWorldNode(crops, center);
    }

    private void AddRegionalHouse(string name, Vector3 position, Color wallColor, Vector3 front)
    {
        var yaw = MathF.Abs(front.X) > 0.5f
            ? front.X > 0 ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f
            : front.Z > 0 ? Mathf.Pi : 0.0f;
        var productionHouse = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        var houseCollisionSize = new Vector3(7.25f, 6.9f, 8.0f);
        productionHouse.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, houseCollisionSize.Y * 0.5f, 0),
            Shape = new BoxShape3D { Size = houseCollisionSize }
        });
        if (AddProductionEnvironmentVisual(
                productionHouse,
                MedievalFarmhouseScenePath,
                name + "MedievalHouse",
                Vector3.One))
        {
            AddWorldNode(productionHouse, position);
            _pathObstacles.Add(WorldPathObstacle.FromRotatedBox(
                position + new Vector3(0, houseCollisionSize.Y * 0.5f, 0),
                houseCollisionSize,
                yaw));
            return;
        }
        productionHouse.Free();

        AddFeatureStaticBox(name, position + new Vector3(0, 1.55f, 0),
            new Vector3(7.0f, 3.1f, 6.0f), wallColor);
        AddFeatureDecoration(name + "Roof", new CylinderMesh
        {
            TopRadius = 0,
            BottomRadius = 5.2f,
            Height = 2.6f,
            RadialSegments = 4
        }, position + new Vector3(0, 4.35f, 0), new Vector3(0, Mathf.DegToRad(45), 0),
            new Color("4a2019"));
        if (MathF.Abs(front.X) > 0.5f)
        {
            var faceX = front.X * 3.56f;
            AddFeatureDecoration(name + "Door", new BoxMesh { Size = new Vector3(0.12f, 2.25f, 1.35f) },
                position + new Vector3(faceX, 1.15f, 0), Vector3.Zero, new Color("2b1b12"));
            AddFeatureDecoration(name + "WindowLeft", new BoxMesh { Size = new Vector3(0.13f, 0.9f, 1.1f) },
                position + new Vector3(faceX, 1.75f, -2.0f), Vector3.Zero, new Color("d69b3d"));
            AddFeatureDecoration(name + "WindowRight", new BoxMesh { Size = new Vector3(0.13f, 0.9f, 1.1f) },
                position + new Vector3(faceX, 1.75f, 2.0f), Vector3.Zero, new Color("d69b3d"));
            return;
        }

        var faceZ = front.Z * 3.06f;
        AddFeatureDecoration(name + "Door", new BoxMesh { Size = new Vector3(1.35f, 2.25f, 0.12f) },
            position + new Vector3(0, 1.15f, faceZ), Vector3.Zero, new Color("2b1b12"));
        AddFeatureDecoration(name + "WindowLeft", new BoxMesh { Size = new Vector3(1.1f, 0.9f, 0.13f) },
            position + new Vector3(-2.0f, 1.75f, faceZ), Vector3.Zero, new Color("d69b3d"));
        AddFeatureDecoration(name + "WindowRight", new BoxMesh { Size = new Vector3(1.1f, 0.9f, 0.13f) },
            position + new Vector3(2.0f, 1.75f, faceZ), Vector3.Zero, new Color("d69b3d"));
    }

    private void AddFeatureStaticBox(string name, Vector3 position, Vector3 size, Color color)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 2 | 4 | 8
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(CreateMesh(name + "Mesh", new BoxMesh { Size = size }, Vector3.Zero, Vector3.Zero, color));
        AddWorldNode(body, position);
        if (position.Y + size.Y * 0.5f > 0.9f)
        {
            _pathObstacles.Add(WorldPathObstacle.FromBox(position, size));
        }
    }

    private void AddFeatureDecoration(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        bool transparency = false,
        float metallic = 0)
    {
        AddWorldNode(CreateMesh(name, mesh, position, rotation, color, transparency, metallic), position);
    }

    private void AddFeatureRoadPath(
        string name,
        IReadOnlyList<Vector3> points,
        float width,
        Color color)
    {
        for (var index = 0; index < points.Count - 1; index++)
        {
            var start = points[index];
            var end = points[index + 1];
            var direction = end - start;
            direction.Y = 0;
            var length = direction.Length();
            if (length <= 0.01f)
            {
                continue;
            }

            var midpoint = (start + end) * 0.5f;
            midpoint.Y = MathF.Max(start.Y, end.Y);
            AddFeatureDecoration(
                $"{name}Segment{index + 1}",
                new BoxMesh { Size = new Vector3(width, 0.08f, length + 0.65f) },
                midpoint,
                new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0),
                index % 2 == 0 ? color : color.Lightened(0.025f));
        }
    }

    private void BuildExpandedRegions()
    {
        for (var gridZ = -1; gridZ <= 1; gridZ++)
        {
            for (var gridX = -1; gridX <= 1; gridX++)
            {
                if (gridX == 0 && gridZ == 0)
                {
                    AddProductionGrassField(gridX, gridZ);
                    continue;
                }

                var treeCount = GetRegionTreeCount(gridX, gridZ);
                for (var index = 0; index < treeCount; index++)
                {
                    var position = GetOrganicNaturePosition(gridX, gridZ, index, treeCount, 100);
                    var scale = 0.78f + NatureHash01(gridX, gridZ, index, 190) * 0.62f;
                    AddTree(new Vector3(position.X, 0, position.Y), scale);
                }

                var rockCount = GetRegionRockCount(gridX, gridZ);
                for (var index = 0; index < rockCount; index++)
                {
                    var position = GetOrganicNaturePosition(gridX, gridZ, index, rockCount, 700);
                    AddRock(new Vector3(position.X, 0, position.Y), new Vector3(
                        1.25f + NatureHash01(gridX, gridZ, index, 790) * 1.55f,
                        0.75f + NatureHash01(gridX, gridZ, index, 791) * 1.05f,
                        1.05f + NatureHash01(gridX, gridZ, index, 792) * 1.45f));
                }

                var bushCount = GetRegionBushCount(gridX, gridZ);
                for (var index = 0; index < bushCount; index++)
                {
                    var position = GetOrganicNaturePosition(gridX, gridZ, index, bushCount, 1100);
                    AddBush(
                        new Vector3(position.X, 0, position.Y),
                        0.72f + NatureHash01(gridX, gridZ, index, 1190) * 0.42f);
                }
                AddProductionGrassField(gridX, gridZ);
            }
        }
    }

    private static int GetRegionTreeCount(int gridX, int gridZ) => (gridX, gridZ) switch
    {
        (-1, -1) => 20,
        (0, -1) => 8,
        (1, -1) => 17,
        (-1, 0) => 11,
        (1, 0) => 18,
        (-1, 1) => 16,
        (0, 1) => 9,
        (1, 1) => 4,
        _ => 0
    };

    private static int GetRegionRockCount(int gridX, int gridZ) => (gridX, gridZ) switch
    {
        (-1, -1) => 6,
        (0, -1) => 8,
        (1, -1) => 9,
        (-1, 0) => 5,
        (1, 0) => 6,
        (-1, 1) => 5,
        (0, 1) => 6,
        (1, 1) => 12,
        _ => 0
    };

    private static int GetRegionBushCount(int gridX, int gridZ) => (gridX, gridZ) switch
    {
        (-1, -1) => 3,
        (0, -1) => 2,
        (1, -1) => 2,
        (-1, 0) => 2,
        (1, 0) => 3,
        (-1, 1) => 3,
        (0, 1) => 2,
        (1, 1) => 1,
        _ => 0
    };

    private void AddProductionGrassField(int gridX, int gridZ)
    {
        var mesh = GetProductionEnvironmentMesh(MeadowGrassScenePath);
        if (mesh is null)
        {
            return;
        }

        var transforms = new List<Transform3D>();
        const int requestedCount = 30;
        for (var index = 0; index < requestedCount; index++)
        {
            var point = GetOrganicNaturePosition(gridX, gridZ, index, requestedCount, 1500);
            var position = new Vector3(point.X, 0.012f, point.Y);
            var lakeX = (position.X - MirrorwaterLakeCenter.X) / 38.0f;
            var lakeZ = (position.Z - MirrorwaterLakeCenter.Z) / 31.0f;
            if (lakeX * lakeX + lakeZ * lakeZ <= 1.0f ||
                IsInsideDarkwoodCampClearing(position.X, position.Z) ||
                IsNaturePlacementBlocked(position, 0.24f))
            {
                continue;
            }

            var scale = 0.74f + NatureHash01(gridX, gridZ, index, 1530) * 0.52f;
            var yaw = NatureHash01(gridX, gridZ, index, 1531) * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));
            transforms.Add(new Transform3D(basis, position));
        }

        if (transforms.Count == 0)
        {
            return;
        }

        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }

        var grassField = new MultiMeshInstance3D
        {
            Name = $"ProductionGrassField-{gridX}-{gridZ}",
            Multimesh = multiMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = 62.0f,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
        };
        AddWorldNode(
            grassField,
            new Vector3(gridX * WorldGridSize, 0, gridZ * WorldGridSize));
    }

    private void AddBush(Vector3 position, float scale)
    {
        var footprintRadius = MathF.Max(0.7f, scale * 0.85f);
        if (IsNaturePlacementBlocked(position, footprintRadius))
        {
            return;
        }

        var bush = InstantiateProductionEnvironmentAsset(
            WoodlandBushScenePath,
            "WoodlandBush");
        if (bush is null)
        {
            return;
        }

        bush.Position = position;
        bush.Rotation = new Vector3(0, Mathf.PosMod(position.X * 0.17f + position.Z * 0.11f, Mathf.Tau), 0);
        bush.Scale = Vector3.One * scale;
        GroundProductionAsset(bush, position.Y);
        AddWorldNode(bush, position);
        _natureFootprints.Add((new Vector2(position.X, position.Z), footprintRadius));
    }

    private static float NatureHash01(int gridX, int gridZ, int index, int salt)
    {
        unchecked
        {
            var value = ((uint)index + 1u) * 374761393u
                        + ((uint)gridX + 2u) * 668265263u
                        + ((uint)gridZ + 2u) * 2246822519u
                        + ((uint)salt + 1u) * 3266489917u;
            value ^= value >> 13;
            value *= 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216.0f;
        }
    }

    private static Vector2 GetOrganicNaturePosition(
        int gridX,
        int gridZ,
        int index,
        int count,
        int salt)
    {
        var clusterCount = Math.Clamp((count + 5) / 6, 1, 4);
        var localX = 0.0f;
        var localZ = 0.0f;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var candidate = index + attempt * count;
            var cluster = candidate % clusterCount;
            var clusterAngle = Mathf.Tau * NatureHash01(gridX, gridZ, cluster, salt + 1);
            var clusterRadius = 11.0f + 19.0f * NatureHash01(gridX, gridZ, cluster, salt + 2);
            var centerX = Mathf.Cos(clusterAngle) * clusterRadius;
            var centerZ = Mathf.Sin(clusterAngle) * clusterRadius;
            var ring = candidate / clusterCount;
            var angle = Mathf.Tau * NatureHash01(gridX, gridZ, candidate, salt + 3);
            var radius = (2.3f + MathF.Sqrt(ring + 1.0f) * 3.6f)
                         * (0.72f + 0.55f * NatureHash01(gridX, gridZ, candidate, salt + 4));
            localX = Math.Clamp(centerX + Mathf.Cos(angle) * radius, -42.0f, 42.0f);
            localZ = Math.Clamp(centerZ + Mathf.Sin(angle) * radius, -42.0f, 42.0f);
            var worldX = gridX * WorldGridSize + localX;
            var worldZ = gridZ * WorldGridSize + localZ;
            if (MathF.Abs(localX) > 7.0f && MathF.Abs(worldZ - 25.0f) > 6.2f &&
                MathF.Abs(worldZ - 12.0f) > 4.7f &&
                !IsInsideDarkwoodCampClearing(worldX, worldZ))
            {
                break;
            }
        }

        var adjustedWorldZ = gridZ * WorldGridSize + localZ;
        if (MathF.Abs(localX) <= 7.0f)
        {
            localX = NatureHash01(gridX, gridZ, index, salt + 8) >= 0.5f ? 7.6f : -7.6f;
        }
        if (MathF.Abs(adjustedWorldZ - 25.0f) <= 6.2f)
        {
            localZ += adjustedWorldZ >= 25.0f ? 7.0f : -7.0f;
        }
        adjustedWorldZ = gridZ * WorldGridSize + localZ;
        if (MathF.Abs(adjustedWorldZ - 12.0f) <= 4.7f)
        {
            localZ += adjustedWorldZ >= 12.0f ? 5.4f : -5.4f;
        }
        var adjustedWorldX = gridX * WorldGridSize + localX;
        adjustedWorldZ = gridZ * WorldGridSize + localZ;
        if (IsInsideDarkwoodCampClearing(adjustedWorldX, adjustedWorldZ))
        {
            var away = new Vector2(adjustedWorldX - DarkwoodCampCenter.X, adjustedWorldZ - DarkwoodCampCenter.Z);
            if (away.LengthSquared() < 0.001f)
            {
                away = Vector2.Left;
            }
            away = away.Normalized() * DarkwoodCampClearingRadius;
            localX = Math.Clamp(DarkwoodCampCenter.X + away.X - gridX * WorldGridSize, -42.0f, 42.0f);
            localZ = Math.Clamp(DarkwoodCampCenter.Z + away.Y - gridZ * WorldGridSize, -42.0f, 42.0f);
        }
        return new Vector2(gridX * WorldGridSize + localX, gridZ * WorldGridSize + localZ);
    }

    private static bool IsInsideDarkwoodCampClearing(float worldX, float worldZ)
    {
        var deltaX = worldX - DarkwoodCampCenter.X;
        var deltaZ = worldZ - DarkwoodCampCenter.Z;
        return deltaX * deltaX + deltaZ * deltaZ < DarkwoodCampClearingRadius * DarkwoodCampClearingRadius;
    }

    private void BuildWorldPathfinder()
    {
        _pathfinder = new WorldPathfinder(
            new Vector2(-PlayableWorldLimit, -PlayableWorldLimit),
            new Vector2(PlayableWorldLimit, PlayableWorldLimit),
            1.0f,
            _pathObstacles,
            1.6f);
    }

    private void ApplyDarkwoodCampStage(int stage)
    {
        if (!IsInstanceValid(_darkwoodCamp))
        {
            return;
        }

        stage = Math.Clamp(stage, 1, 3);
        if (_campStage == stage && _darkwoodCamp.GetChildCount() > 0)
        {
            return;
        }
        _campStage = stage;
        foreach (var child in _darkwoodCamp.GetChildren())
        {
            child.QueueFree();
        }

        var cloth = stage == 1 ? new Color("60442e") : new Color("743426");
        AddCampPath();
        AddAFrameCampTent("ChiefShelter", new Vector3(-1.0f, 0, -9.0f), 5.4f, 6.2f, 3.2f,
            cloth, 0, "GORVAK'S COMMAND SHELTER");
        AddAFrameCampTent("RaiderShelterEast", new Vector3(6.0f, 0, -5.0f), 4.1f, 5.0f, 2.55f,
            new Color("4f3c2b"), Mathf.DegToRad(-24), "RAIDER SLEEPING SHELTER");
        AddAFrameCampTent("RaiderShelterNorthEast", new Vector3(5.0f, 0, 1.0f), 4.0f, 4.8f, 2.45f,
            new Color("57402b"), Mathf.DegToRad(10), "RAIDER SLEEPING SHELTER");
        AddCampFire(new Vector3(-0.8f, 0, 0.6f));

        if (stage >= 2)
        {
            AddAFrameCampTent("RaiderShelterUpperEast", new Vector3(5.0f, 0, 7.0f), 4.0f, 4.8f, 2.45f,
                new Color("503a28"), Mathf.DegToRad(32), "HUNTERS' SLEEPING SHELTER");
            AddAFrameCampTent("RaiderShelterUpperWest", new Vector3(-3.0f, 0, 8.0f), 3.7f, 4.5f, 2.3f,
                new Color("473627"), Mathf.DegToRad(-18), "WORKERS' SLEEPING SHELTER");
            AddCampMesh("HunterLodge", new BoxMesh { Size = new Vector3(5.5f, 2.6f, 4.0f) },
                new Vector3(-9.5f, 1.3f, -5.5f), Vector3.Zero, new Color("51341f"));
            AddCampMesh("HunterLodgeRoof", new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = 4.2f,
                Height = 2.0f,
                RadialSegments = 4
            }, new Vector3(-9.5f, 3.45f, -5.5f), new Vector3(0, Mathf.DegToRad(45), 0), new Color("341812"));
            AddCampLabel("HunterLodgeLabel", "HUNTER LODGE\nFood • Recruitment", new Vector3(-9.5f, 5.3f, -5.5f));
        }

        if (stage >= 3)
        {
            AddCampMesh("Watchtower", new BoxMesh { Size = new Vector3(3.2f, 6.8f, 3.2f) },
                new Vector3(12, 3.4f, -10), Vector3.Zero, new Color("3f2a18"));
            AddCampMesh("WatchtowerRoof", new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = 2.9f,
                Height = 2.2f,
                RadialSegments = 4
            }, new Vector3(12, 7.7f, -10), new Vector3(0, Mathf.DegToRad(45), 0), new Color("5d1e19"));
            AddCampMesh("IronWorkshop", new BoxMesh { Size = new Vector3(5.0f, 3.2f, 4.4f) },
                new Vector3(11, 1.6f, 6.0f), Vector3.Zero, new Color("353638"), 0.65f);
            AddCampMesh("IronWorkshopRoof", new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = 3.8f,
                Height = 1.7f,
                RadialSegments = 4
            }, new Vector3(11, 4.0f, 6.0f), new Vector3(0, Mathf.DegToRad(45), 0), new Color("3b1714"));
            AddCampMesh("ForgeChimney", new CylinderMesh
            {
                TopRadius = 0.35f,
                BottomRadius = 0.48f,
                Height = 3.2f,
                RadialSegments = 8
            }, new Vector3(12.4f, 5.0f, 6.5f), Vector3.Zero, new Color("29292a"), 0.35f);
            AddCampLabel("WatchtowerLabel", "DARKWOOD WATCHTOWER\nSight • Defense", new Vector3(12, 9.5f, -10));
            AddCampLabel("IronWorkshopLabel", "IRON WORKSHOP\nWeapons • Armor", new Vector3(11, 6.0f, 6.0f));
        }

        _darkwoodCamp.AddChild(new Label3D
        {
            Name = "CampStageLabel",
            Text = stage switch
            {
                1 => "DARKWOOD ENCAMPMENT",
                2 => "DARKWOOD ESTABLISHED CAMP",
                _ => "DARKWOOD FORTIFIED CAMP"
            },
            Position = new Vector3(0, stage >= 3 ? 10.0f : 5.0f, -1),
            FontSize = 34,
            Modulate = new Color("e6bb61"),
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = false
        });
    }

    private void AddCampPath()
    {
        AddCampMesh("GatePath", new BoxMesh { Size = new Vector3(3.0f, 0.05f, 22.0f) },
            new Vector3(2.2f, 0.03f, 5.0f), Vector3.Zero, new Color("4b3928"));
        AddCampMesh("LodgePath", new BoxMesh { Size = new Vector3(13.0f, 0.045f, 2.2f) },
            new Vector3(-4.3f, 0.025f, -3.0f), new Vector3(0, Mathf.DegToRad(-9), 0), new Color("493625"));
    }

    private void AddAFrameCampTent(
        string name,
        Vector3 position,
        float width,
        float length,
        float height,
        Color cloth,
        float rotationY,
        string labelText)
    {
        var tent = new Node3D { Name = name, Position = position, Rotation = new Vector3(0, rotationY, 0) };
        _darkwoodCamp.AddChild(tent);
        var slope = MathF.Sqrt(width * width * 0.25f + height * height);
        var pitch = MathF.Atan2(height, width * 0.5f);
        var material = new StandardMaterial3D { AlbedoColor = cloth, Roughness = 0.96f };
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            tent.AddChild(new MeshInstance3D
            {
                Name = side < 0 ? "LeftCanvas" : "RightCanvas",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(slope, 0.10f, length),
                    Material = material
                },
                Position = new Vector3(side * width * 0.25f, height * 0.5f + 0.12f, 0),
                Rotation = new Vector3(0, 0, side < 0 ? pitch : -pitch)
            });
        }

        var poleMaterial = new StandardMaterial3D { AlbedoColor = new Color("49301d"), Roughness = 0.94f };
        AddCampTentPole(tent, new Vector3(0, 0.08f, -length * 0.52f), new Vector3(0, height + 0.28f, -length * 0.52f), 0.10f, poleMaterial, "RearPole");
        AddCampTentPole(tent, new Vector3(0, 0.08f, length * 0.52f), new Vector3(0, height + 0.28f, length * 0.52f), 0.10f, poleMaterial, "FrontPole");
        AddCampTentPole(tent, new Vector3(0, height + 0.20f, -length * 0.56f), new Vector3(0, height + 0.20f, length * 0.56f), 0.09f, poleMaterial, "RidgePole");
        tent.AddChild(CreateMesh("Bedroll", new BoxMesh { Size = new Vector3(width * 0.48f, 0.12f, length * 0.52f) },
            new Vector3(0, 0.09f, -0.35f), Vector3.Zero, new Color("2e2a22")));
        tent.AddChild(new Label3D
        {
            Name = "PurposeLabel",
            Text = labelText,
            Position = new Vector3(0, height + 0.75f, length * 0.45f),
            FontSize = 18,
            Modulate = new Color("d8b86f"),
            OutlineSize = 6,
            OutlineModulate = new Color(0, 0, 0, 0.92f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = false
        });
    }

    private static void AddCampTentPole(
        Node3D parent,
        Vector3 start,
        Vector3 end,
        float radius,
        Material material,
        string name)
    {
        var direction = end - start;
        var length = direction.Length();
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = radius * 0.82f,
                BottomRadius = radius,
                Height = length,
                RadialSegments = 8,
                Material = material
            },
            Position = (start + end) * 0.5f,
            Quaternion = new Quaternion(Vector3.Up, direction / length)
        });
    }

    private void AddCampFire(Vector3 position)
    {
        for (var stone = 0; stone < 10; stone++)
        {
            var angle = stone * Mathf.Tau / 10.0f;
            var pebble = CreateMesh($"FireRingStone{stone}", new SphereMesh
            {
                Radius = 0.22f,
                Height = 0.34f,
                RadialSegments = 8,
                Rings = 4
            }, position + new Vector3(Mathf.Cos(angle) * 0.78f, 0.16f, Mathf.Sin(angle) * 0.78f),
                new Vector3(0, angle, 0), stone % 2 == 0 ? new Color("55504a") : new Color("696159"));
            pebble.Scale = new Vector3(1.25f, 0.75f, 0.95f);
            _darkwoodCamp.AddChild(pebble);
        }
        AddCampMesh("CookLogA", new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.13f, Height = 1.15f, RadialSegments = 8 },
            position + new Vector3(0, 0.25f, 0), new Vector3(0, 0, Mathf.DegToRad(90)), new Color("3b2417"));
        AddCampMesh("CookLogB", new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.13f, Height = 1.15f, RadialSegments = 8 },
            position + new Vector3(0, 0.27f, 0), new Vector3(Mathf.DegToRad(90), 0, 0), new Color("3b2417"));
        AddCampMesh("CookFlameOuter", new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.38f, Height = 1.1f, RadialSegments = 9 },
            position + new Vector3(0, 0.76f, 0), Vector3.Zero, new Color("de5622"));
        AddCampMesh("CookFlameInner", new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.20f, Height = 0.72f, RadialSegments = 8 },
            position + new Vector3(0.06f, 0.70f, 0), Vector3.Zero, new Color("f4b43b"));
        AddCampLabel("CookfireLabel", "CLAN COOKFIRE\nMeals • Council", position + new Vector3(0, 2.0f, 0));
    }

    private void AddCampMesh(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        float metallic = 0.0f)
    {
        _darkwoodCamp.AddChild(CreateMesh(name, mesh, position, rotation, color, metallic: metallic));
    }

    private void AddCampLabel(string name, string text, Vector3 position)
    {
        _darkwoodCamp.AddChild(new Label3D
        {
            Name = name,
            Text = text,
            Position = position,
            FontSize = 24,
            Modulate = new Color("efcf82"),
            OutlineSize = 7,
            OutlineModulate = new Color(0, 0, 0, 0.92f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = false
        });
    }

    private void AddStonehavenGate()
    {
        AddStaticBox("GateLeftTower", new Vector3(-4.5f, 2.5f, 3.5f), new Vector3(3.3f, 5, 3.3f), new Color("4b4e50"));
        AddStaticBox("GateRightTower", new Vector3(4.5f, 2.5f, 3.5f), new Vector3(3.3f, 5, 3.3f), new Color("4b4e50"));
        AddDecoration("GateBeam", new BoxMesh { Size = new Vector3(6.0f, 1.0f, 1.1f) },
            new Vector3(0, 5.0f, 3.5f), Vector3.Zero, new Color("3c3f42"));
        AddDecoration("GateRoofLeft", new CylinderMesh
        {
            TopRadius = 0,
            BottomRadius = 2.8f,
            Height = 2.0f,
            RadialSegments = 6
        }, new Vector3(-4.5f, 6.0f, 3.5f), Vector3.Zero, new Color("5a251d"));
        AddDecoration("GateRoofRight", new CylinderMesh
        {
            TopRadius = 0,
            BottomRadius = 2.8f,
            Height = 2.0f,
            RadialSegments = 6
        }, new Vector3(4.5f, 6.0f, 3.5f), Vector3.Zero, new Color("5a251d"));
        AddDecoration("GateBanner", new BoxMesh { Size = new Vector3(1.4f, 1.9f, 0.08f) },
            new Vector3(0, 4.1f, 2.9f), Vector3.Zero, Red);
        AddDecoration("GateCrest", new SphereMesh { Radius = 0.34f, Height = 0.68f, RadialSegments = 12, Rings = 6 },
            new Vector3(0, 4.25f, 2.82f), Vector3.Zero, Gold, metallic: 0.55f);
    }

    private void AddStonehavenEastGate()
    {
        foreach (var z in new[] { -3.5f, -10.5f })
        {
            AddFeatureStaticBox($"EastGateTower{z}", new Vector3(29.0f, 2.5f, z),
                new Vector3(3.3f, 5.0f, 2.2f), new Color("565a59"));
            AddFeatureDecoration($"EastGateRoof{z}", new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = 2.35f,
                Height = 1.7f,
                RadialSegments = 6
            }, new Vector3(29.0f, 5.85f, z), Vector3.Zero, new Color("51251f"));
        }
        AddFeatureDecoration("EastGateBeam", new BoxMesh { Size = new Vector3(1.1f, 0.9f, 5.0f) },
            new Vector3(29.0f, 4.9f, -7.0f), Vector3.Zero, new Color("464a4a"));
        AddFeatureDecoration("EastGateBanner", new BoxMesh { Size = new Vector3(0.08f, 1.55f, 1.05f) },
            new Vector3(28.38f, 3.9f, -7.0f), Vector3.Zero, Red);
    }

    private void AddHouse(string name, Vector3 position, Color wallColor)
    {
        AddStaticBox(name, position + new Vector3(0, 1.55f, 0), new Vector3(7.0f, 3.1f, 6.0f), wallColor);
        AddDecoration(name + "Roof", new CylinderMesh
        {
            TopRadius = 0,
            BottomRadius = 5.2f,
            Height = 2.6f,
            RadialSegments = 4
        }, position + new Vector3(0, 4.35f, 0), new Vector3(0, Mathf.DegToRad(45), 0), new Color("4a2019"));
        AddDecoration(name + "Door", new BoxMesh { Size = new Vector3(1.35f, 2.25f, 0.12f) },
            position + new Vector3(0, 1.15f, 3.06f), Vector3.Zero, new Color("2b1b12"));
        AddDecoration(name + "WindowLeft", new BoxMesh { Size = new Vector3(1.1f, 0.9f, 0.13f) },
            position + new Vector3(-2.0f, 1.75f, 3.07f), Vector3.Zero, new Color("d69b3d"), metallic: 0.1f);
        AddDecoration(name + "WindowRight", new BoxMesh { Size = new Vector3(1.1f, 0.9f, 0.13f) },
            position + new Vector3(2.0f, 1.75f, 3.07f), Vector3.Zero, new Color("d69b3d"), metallic: 0.1f);
    }

    private void AddTree(Vector3 position, float scale)
    {
        var footprintRadius = MathF.Max(0.8f, scale * 0.95f);
        if (IsNaturePlacementBlocked(position, footprintRadius))
        {
            return;
        }

        var body = new StaticBody3D
        {
            Name = "Tree",
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 2
        };
        AddWorldNode(body, position);
        var navigationDiameter = 0.84f * scale;
        _pathObstacles.Add(WorldPathObstacle.FromBox(
            position,
            new Vector3(navigationDiameter, 3.4f * scale, navigationDiameter)));
        body.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 1.7f * scale, 0),
            Shape = new CylinderShape3D { Radius = 0.42f * scale, Height = 3.4f * scale }
        });
        var resourceLabel = CreateNaturalResourceLabel(
            "TREE\nH  CHOP",
            new Vector3(0, 7.7f * scale, 0));
        body.AddChild(resourceLabel);
        _naturalResourceTargets.Add(new NaturalResourceTarget(body, resourceLabel, "Wood", "wild tree"));
        _natureFootprints.Add((new Vector2(position.X, position.Z), footprintRadius));
        var treeVariant = Mathf.Abs(Mathf.RoundToInt(position.X * 7.0f + position.Z * 11.0f)) %
                          ProductionTreeScenePaths.Length;
        var treeYaw = Mathf.PosMod(position.X * 0.071f + position.Z * 0.043f, Mathf.Tau);
        if (AddProductionEnvironmentVisual(
                body,
                ProductionTreeScenePaths[treeVariant],
                $"ProductionTree{treeVariant + 1}",
                Vector3.One * scale,
                treeYaw))
        {
            return;
        }

        body.AddChild(CreateMesh("Trunk", new CylinderMesh
        {
            TopRadius = 0.23f * scale,
            BottomRadius = 0.52f * scale,
            Height = 4.7f * scale,
            RadialSegments = 11
        }, new Vector3(0, 2.35f * scale, 0), Vector3.Zero, new Color("4d321f")));

        var wood = new Color("5d3e25");
        var phase = Mathf.PosMod(position.X * 0.071f + position.Z * 0.043f, Mathf.Tau);

        var leafColors = new[] { new Color("1f4028"), new Color("2f592f"), new Color("3d6a36"), new Color("294d2a") };
        for (var branchIndex = 0; branchIndex < 7; branchIndex++)
        {
            var angle = phase + branchIndex * Mathf.Tau / 7.0f + (branchIndex % 2 == 0 ? 0.08f : -0.11f);
            var reach = (1.35f + (branchIndex % 3) * 0.24f) * scale;
            var start = new Vector3(0, (2.55f + (branchIndex % 3) * 0.35f) * scale, 0);
            var end = new Vector3(
                Mathf.Cos(angle) * reach,
                (4.15f + (branchIndex % 4) * 0.37f) * scale,
                Mathf.Sin(angle) * reach);
            AddTreeBranch(body, start, end, (0.19f - (branchIndex % 2) * 0.02f) * scale, wood, $"Branch{branchIndex}");

            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            for (var clusterIndex = 0; clusterIndex < 3; clusterIndex++)
            {
                var side = (clusterIndex - 1) * 0.46f * scale;
                var cluster = CreateMesh($"LeafCluster{branchIndex}_{clusterIndex}", new SphereMesh
                {
                    Radius = 0.62f * scale,
                    Height = 1.24f * scale,
                    RadialSegments = 11,
                    Rings = 6
                }, end + tangent * side + new Vector3(0, (0.16f - clusterIndex * 0.14f) * scale, 0),
                    new Vector3(branchIndex * 0.12f, clusterIndex * 0.21f, angle),
                    leafColors[(branchIndex + clusterIndex) % leafColors.Length]);
                cluster.Scale = new Vector3(1.0f, 0.78f + clusterIndex * 0.06f, 0.82f + (branchIndex % 3) * 0.07f);
                body.AddChild(cluster);
            }
        }

        for (var crownIndex = 0; crownIndex < 4; crownIndex++)
        {
            var angle = phase + crownIndex * Mathf.Tau / 4.0f;
            var crown = CreateMesh($"TopLeaf{crownIndex}", new SphereMesh
            {
                Radius = 0.68f * scale,
                Height = 1.36f * scale,
                RadialSegments = 11,
                Rings = 6
            }, new Vector3(Mathf.Cos(angle) * 0.48f * scale, (5.45f + (crownIndex % 2) * 0.28f) * scale,
                Mathf.Sin(angle) * 0.48f * scale), new Vector3(0.1f * crownIndex, 0.17f * crownIndex, angle),
                leafColors[(crownIndex + 1) % leafColors.Length]);
            crown.Scale = new Vector3(0.9f, 0.82f, 1.0f);
            body.AddChild(crown);
        }
    }

    private static void AddTreeBranch(
        Node3D parent,
        Vector3 start,
        Vector3 end,
        float radius,
        Color color,
        string name)
    {
        var direction = end - start;
        var length = direction.Length();
        if (length <= 0.001f)
        {
            return;
        }

        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.94f };
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = radius * 0.48f,
                BottomRadius = radius,
                Height = length,
                RadialSegments = 8,
                Material = material
            },
            Position = (start + end) * 0.5f,
            Quaternion = new Quaternion(Vector3.Up, direction / length)
        });
    }

    private void AddRock(Vector3 position, Vector3 size)
    {
        var footprintRadius = MathF.Max(size.X, size.Z) * 0.56f;
        if (IsNaturePlacementBlocked(position, footprintRadius))
        {
            return;
        }

        var yaw = Mathf.PosMod(position.X * 0.07f + position.Z * 0.09f, Mathf.Tau);
        var body = new StaticBody3D
        {
            Name = "Rock",
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            CollisionLayer = 1,
            CollisionMask = 2
        };
        AddWorldNode(body, position);
        _pathObstacles.Add(WorldPathObstacle.FromRotatedBox(
            position,
            size * 0.82f,
            yaw));
        body.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, size.Y * 0.42f, 0),
            Shape = new BoxShape3D { Size = size * 0.82f }
        });
        var resourceLabel = CreateNaturalResourceLabel(
            "STONE\nH  MINE",
            new Vector3(0, size.Y + 1.0f, 0));
        body.AddChild(resourceLabel);
        _naturalResourceTargets.Add(new NaturalResourceTarget(body, resourceLabel, "Stone", "stone outcrop"));
        _natureFootprints.Add((new Vector2(position.X, position.Z), footprintRadius));

        var rockVariant = Mathf.Abs(Mathf.RoundToInt(position.X * 13.0f + position.Z * 5.0f)) %
                          ProductionRockScenePaths.Length;
        var sourceDimensions = ProductionRockDimensions[rockVariant];
        if (AddProductionEnvironmentVisual(
                body,
                ProductionRockScenePaths[rockVariant],
                $"ProductionRock{rockVariant + 1}",
                new Vector3(
                    size.X / sourceDimensions.X,
                    size.Y / sourceDimensions.Y,
                    size.Z / sourceDimensions.Z)))
        {
            return;
        }

        var mesh = CreateMesh("RockMesh", new SphereMesh
        {
            Radius = 1.0f,
            Height = 2.0f,
            RadialSegments = 8,
            Rings = 4
        }, new Vector3(0, size.Y * 0.42f, 0), Vector3.Zero, new Color("4a4b47"));
        mesh.Scale = size * 0.52f;
        body.AddChild(mesh);
    }

    private bool IsNaturePlacementBlocked(Vector3 position, float radius)
    {
        if (IsInsideReservedConstructionFootprint(position))
        {
            return true;
        }
        var center = new Vector2(position.X, position.Z);
        return _natureFootprints.Any(existing =>
            existing.Center.DistanceTo(center) < existing.Radius + radius + 0.45f);
    }

    private static bool IsInsideReservedConstructionFootprint(Vector3 position)
    {
        var insideLumberYard =
            MathF.Abs(position.X - StonehavenLumberYardCenter.X) <= StonehavenLumberYardClearance.X &&
            MathF.Abs(position.Z - StonehavenLumberYardCenter.Y) <= StonehavenLumberYardClearance.Y;
        var lakeX = (position.X - MirrorwaterLakeCenter.X) / 39.0f;
        var lakeZ = (position.Z - MirrorwaterLakeCenter.Z) / 32.0f;
        var insideLakeDistrict = lakeX * lakeX + lakeZ * lakeZ <= 1.0f;
        var insideMineDistrict =
            MathF.Abs(position.X - IronMineCenter.X) <= 43.0f &&
            MathF.Abs(position.Z - IronMineCenter.Z) <= 42.0f;
        var insideFarmland =
            MathF.Abs(position.X - StonehavenFarmlandCenter.X) <= 45.0f &&
            MathF.Abs(position.Z - StonehavenFarmlandCenter.Z) <= 45.0f;
        var dragonRoostX = (position.X - WillowmereDragonRoostCenter.X) / 24.0f;
        var dragonRoostZ = (position.Z - WillowmereDragonRoostCenter.Z) / 22.0f;
        var insideDragonRoost = dragonRoostX * dragonRoostX + dragonRoostZ * dragonRoostZ <= 1.0f;
        var insideStonehaven =
            MathF.Abs(position.X) <= 34.0f &&
            position.Z is >= -42.0f and <= 8.0f;
        var insideMainRoad = DistanceToRoad(position,
            new Vector2(0, -142), new Vector2(0, 142)) <= 4.4f;
        var insideLakeRoad =
            DistanceToRoad(position, new Vector2(27, -7), new Vector2(45, -7.5f)) <= 4.0f ||
            DistanceToRoad(position, new Vector2(45, -7.5f), new Vector2(56, -16)) <= 4.0f ||
            DistanceToRoad(position, new Vector2(56, -16), new Vector2(68, -20)) <= 4.0f;
        var insideMineRoad =
            DistanceToRoad(position, new Vector2(28, -18), new Vector2(52, -43)) <= 4.0f ||
            DistanceToRoad(position, new Vector2(52, -43), new Vector2(82, -66)) <= 4.0f ||
            DistanceToRoad(position, new Vector2(82, -66), new Vector2(104, -82)) <= 4.0f;
        return insideLumberYard || insideLakeDistrict || insideMineDistrict || insideFarmland ||
               insideDragonRoost || insideStonehaven || insideMainRoad || insideLakeRoad || insideMineRoad;
    }

    private static float DistanceToRoad(Vector3 position, Vector2 start, Vector2 end)
    {
        var point = new Vector2(position.X, position.Z);
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return point.DistanceTo(start);
        }
        var amount = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return point.DistanceTo(start + segment * amount);
    }

    private static Label3D CreateNaturalResourceLabel(string text, Vector3 position) => new()
    {
        Text = text,
        Position = position,
        FontSize = 22,
        Modulate = new Color("d8a94b"),
        OutlineSize = 7,
        OutlineModulate = new Color(0, 0, 0, 0.94f),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        Visible = false
    };

    private void AddBoundaryHill(string name, Vector3 position, Vector3 size)
    {
        AddStaticBox(name, position, size, new Color("273323"));
        if (_stylizedEnvironmentLoaded)
        {
            return;
        }

        var ridge = CreateMesh(name + "Crown", new SphereMesh
        {
            Radius = 1.0f,
            Height = 2.0f,
            RadialSegments = 16,
            Rings = 6
        }, position + new Vector3(0, size.Y * 0.45f, 0), Vector3.Zero, new Color("33432c"));
        ridge.Scale = new Vector3(size.X * 0.65f, size.Y * 0.75f, size.Z * 0.65f);
        AddWorldNode(ridge, position);
    }

    private void AddWallSegment(Vector3 position, Vector3 size)
    {
        AddStaticBox("StonehavenWall", position, size, new Color("55585a"));
        for (var x = -size.X / 2.0f + 0.45f; x < size.X / 2.0f; x += 0.9f)
        {
            AddDecoration("WallCrenel", new BoxMesh { Size = new Vector3(0.55f, 0.55f, 0.9f) },
                position + new Vector3(x, size.Y / 2.0f + 0.28f, 0), Vector3.Zero, new Color("626568"));
        }
    }

    private void AddStaticBox(
        string name,
        Vector3 position,
        Vector3 size,
        Color color,
        bool alwaysLoaded = false)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 2
        };
        if (alwaysLoaded)
        {
            AddChild(body);
        }
        else
        {
            AddWorldNode(body, position);
        }
        if (position.Y + size.Y * 0.5f > 0.9f)
        {
            _pathObstacles.Add(WorldPathObstacle.FromBox(position, size));
        }
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (!_stylizedEnvironmentLoaded)
        {
            body.AddChild(CreateMesh(name + "Mesh", new BoxMesh { Size = size }, Vector3.Zero, Vector3.Zero, color));
        }
    }

    private void AddInvisibleStaticBox(string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 2
        };
        AddWorldNode(body, position);
        _pathObstacles.Add(WorldPathObstacle.FromBox(position, size));
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
    }

    private void AddStaticRamp(string name, Vector3 position, Vector3 size, float rotationX, Color color)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(rotationX, 0, 0),
            CollisionLayer = 1,
            CollisionMask = 2
        };
        AddWorldNode(body, position);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (!_stylizedEnvironmentLoaded)
        {
            body.AddChild(CreateMesh(name + "Mesh", new BoxMesh { Size = size }, Vector3.Zero, Vector3.Zero, color));
        }
    }

    private void AddDecoration(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        bool transparency = false,
        float metallic = 0.0f)
    {
        if (_stylizedEnvironmentLoaded)
        {
            return;
        }

        AddWorldNode(CreateMesh(name, mesh, position, rotation, color, transparency, metallic), position);
    }

    private static MeshInstance3D CreateMesh(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        bool transparency = false,
        float metallic = 0.0f)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = metallic > 0.5f ? 0.3f : 0.86f,
            Metallic = metallic
        };
        if (transparency)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }

        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material,
            CastShadow = transparency
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : GeometryInstance3D.ShadowCastingSetting.On
        };
    }

    private Label3D AddLabel(string name, string text, Vector3 position, int fontSize, Color color)
    {
        var label = new Label3D
        {
            Name = name,
            Text = text,
            Position = position,
            FontSize = fontSize,
            Modulate = color,
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            Visible = false
        };
        AddWorldNode(label, position);
        return label;
    }

    private void BuildInterface()
    {
        var canvas = new CanvasLayer { Name = "Phase6HUD", Layer = 20 };
        AddChild(canvas);
        var root = new Control
        {
            Name = "HudRoot",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        canvas.AddChild(root);

        var topPanel = CreatePanel();
        topPanel.Name = "CharacterBar";
        topPanel.AnchorLeft = 0;
        topPanel.AnchorRight = 1;
        topPanel.OffsetLeft = 18;
        topPanel.OffsetTop = 16;
        topPanel.OffsetRight = -18;
        topPanel.OffsetBottom = 138;
        topPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(topPanel);

        var topMargin = CreateMargin(14, 9, 14, 9);
        topPanel.AddChild(topMargin);
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 14);
        topMargin.AddChild(topRow);

        var portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(112, 64),
            Texture = GD.Load<Texture2D>(_characterName.Equals("Elara", StringComparison.OrdinalIgnoreCase)
                ? "res://Assets/Characters/elara.png"
                : "res://Assets/Characters/alden.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topRow.AddChild(portrait);

        var identity = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        topRow.AddChild(identity);
        _identityLabel = CreateLabel($"{_characterName.ToUpperInvariant()}  •  LEVEL {_level} {_archetype.ToUpperInvariant()}", 22, Gold);
        identity.AddChild(_identityLabel);
        identity.AddChild(CreateLabel(_region.ToUpperInvariant(), 15, new Color("b94735")));
        _healthLabel = CreateLabel($"HEALTH  {_health}/{_maximumHealth}", 11, new Color("d7c7a5"));
        identity.AddChild(_healthLabel);
        _healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(360, 10),
            MinValue = 0,
            MaxValue = Math.Max(1, _maximumHealth),
            Value = _health,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _healthBar.AddThemeStyleboxOverride("background", CreateFlatStyle(new Color("241a18"), new Color("60412d"), 3));
        _healthBar.AddThemeStyleboxOverride("fill", CreateFlatStyle(new Color("8e2119"), new Color("d8a94b"), 3));
        identity.AddChild(_healthBar);
        _experienceLabel = CreateLabel($"EXPERIENCE  {_experience}/{_level * 100L}", 11, new Color("d7c7a5"));
        identity.AddChild(_experienceLabel);
        _experienceBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(360, 8),
            MinValue = 0,
            MaxValue = Math.Max(1, _level * 100L),
            Value = _experience,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _experienceBar.AddThemeStyleboxOverride("background", CreateFlatStyle(new Color("171b22"), new Color("4c5360"), 3));
        _experienceBar.AddThemeStyleboxOverride("fill", CreateFlatStyle(new Color("9d6b20"), new Color("edc05a"), 3));
        identity.AddChild(_experienceBar);

        var statusStack = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(300, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        topRow.AddChild(statusStack);
        _coordinates = CreateLabel(string.Empty, 15, Parchment);
        _coordinates.HorizontalAlignment = HorizontalAlignment.Right;
        statusStack.AddChild(_coordinates);
        _saveStatus = CreateLabel("Position restored • Autosave every 10 seconds", 13, new Color("e5bd62"));
        _saveStatus.HorizontalAlignment = HorizontalAlignment.Right;
        statusStack.AddChild(_saveStatus);

        _worldHudLabel = CreateLabel("WORLD DAY 1  •  DARKWOOD: ENCAMPMENT", 13, new Color("d35a45"));
        _worldHudLabel.HorizontalAlignment = HorizontalAlignment.Right;
        statusStack.AddChild(_worldHudLabel);
        _developmentHudLabel = CreateLabel("STONEHAVEN STORES  •  WOOD 0  •  STONE 0", 12, new Color("e5bd62"));
        _developmentHudLabel.HorizontalAlignment = HorizontalAlignment.Right;
        statusStack.AddChild(_developmentHudLabel);
        _carriedResourcesHudLabel = CreateLabel("PACK 0/80  •  WOOD 0  •  STONE 0  •  GOLD 0", 12, new Color("e5bd62"));
        _carriedResourcesHudLabel.HorizontalAlignment = HorizontalAlignment.Right;
        statusStack.AddChild(_carriedResourcesHudLabel);
        _raidHudLabel = CreateLabel("RAID ACTIVE", 12, new Color("f0644c"));
        _raidHudLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _raidHudLabel.Visible = false;
        statusStack.AddChild(_raidHudLabel);

        var helpPanel = CreatePanel();
        helpPanel.AnchorLeft = 0;
        helpPanel.AnchorTop = 1;
        helpPanel.AnchorBottom = 1;
        helpPanel.OffsetLeft = 18;
        helpPanel.OffsetTop = -92;
        helpPanel.OffsetRight = 1080;
        helpPanel.OffsetBottom = -18;
        helpPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(helpPanel);
        var helpMargin = CreateMargin(12, 8, 12, 8);
        helpPanel.AddChild(helpMargin);
        var adminHelp = _isAdministrator ? "     F8  Admin Labels" : string.Empty;
        var helpText = CreateLabel(
            "WASD / ARROWS  Move     MOUSE  Look     SCROLL  Zoom     SHIFT  Sprint     SPACE  Jump     U  Unstuck\n" +
            "LEFT CLICK / F  Attack     H  Chop / Mine     B  Deposit Materials     R  Talk     TAB  Targets     " +
            $"I  Inventory     J  Living World{adminHelp}     F10  Release Mouse     F12  Screenshot     ESC  Menu",
            14,
            Parchment);
        helpText.VerticalAlignment = VerticalAlignment.Center;
        helpMargin.AddChild(helpText);

        var title = CreateLabel("STONEHAVEN VALLEY", 28, Gold);
        title.AnchorLeft = 0.5f;
        title.AnchorRight = 0.5f;
        title.OffsetLeft = -190;
        title.OffsetRight = 190;
        title.OffsetTop = 150;
        title.OffsetBottom = 192;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        title.AddThemeConstantOverride("shadow_offset_x", 2);
        title.AddThemeConstantOverride("shadow_offset_y", 2);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(title);

        _targetLabel = CreateLabel("+", 20, new Color("e7d4aa"));
        _targetLabel.AnchorLeft = 0.5f;
        _targetLabel.AnchorTop = 0.5f;
        _targetLabel.AnchorRight = 0.5f;
        _targetLabel.AnchorBottom = 0.5f;
        _targetLabel.OffsetLeft = -170;
        _targetLabel.OffsetTop = -24;
        _targetLabel.OffsetRight = 170;
        _targetLabel.OffsetBottom = 78;
        _targetLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _targetLabel.VerticalAlignment = VerticalAlignment.Center;
        _targetLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        _targetLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _targetLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        root.AddChild(_targetLabel);

        var combatPanel = CreatePanel();
        combatPanel.AnchorLeft = 1;
        combatPanel.AnchorTop = 1;
        combatPanel.AnchorRight = 1;
        combatPanel.AnchorBottom = 1;
        combatPanel.OffsetLeft = -505;
        combatPanel.OffsetTop = -92;
        combatPanel.OffsetRight = -18;
        combatPanel.OffsetBottom = -18;
        combatPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(combatPanel);
        var combatMargin = CreateMargin(12, 8, 12, 8);
        combatPanel.AddChild(combatMargin);
        _combatStatus = CreateLabel("Loading persistent creatures...", 14, new Color("efc866"));
        _combatStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _combatStatus.VerticalAlignment = VerticalAlignment.Center;
        combatMargin.AddChild(_combatStatus);

        _skillLabel = CreateLabel("Q / E  Skills loading...", 14, new Color("f1cf74"));
        _skillLabel.AnchorLeft = 0.5f;
        _skillLabel.AnchorRight = 0.5f;
        _skillLabel.OffsetLeft = -330;
        _skillLabel.OffsetRight = 330;
        _skillLabel.OffsetTop = 198;
        _skillLabel.OffsetBottom = 228;
        _skillLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _skillLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        _skillLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _skillLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        root.AddChild(_skillLabel);

        BuildMenuOverlay(root);
        BuildInventoryOverlay(root);
        BuildWorldOverlay(root);
    }

    private void BuildMenuOverlay(Control root)
    {
        _menuOverlay = new ColorRect
        {
            Name = "RealmMenuOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Color = new Color(0.005f, 0.005f, 0.008f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        root.AddChild(_menuOverlay);

        var center = new CenterContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        _menuOverlay.AddChild(center);
        var panel = CreatePanel();
        panel.CustomMinimumSize = new Vector2(440, 0);
        center.AddChild(panel);
        var margin = CreateMargin(30, 26, 30, 26);
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        var heading = CreateLabel("REALM MENU", 29, Gold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(heading);
        var subheading = CreateLabel($"{_characterName} in {_region}", 15, new Color("c75a48"));
        subheading.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(subheading);

        var resume = CreateButton("Resume Journey");
        resume.Pressed += () => SetMenuOpen(false);
        layout.AddChild(resume);
        var inventory = CreateButton("Inventory and Equipment");
        inventory.Pressed += () =>
        {
            SetMenuOpen(false);
            SetInventoryOpen(true);
        };
        layout.AddChild(inventory);
        var livingWorld = CreateButton("Living World and Chronicles");
        livingWorld.Pressed += () =>
        {
            SetMenuOpen(false);
            SetWorldOpen(true);
            WorldStateRequested?.Invoke();
            RaidStateRequested?.Invoke();
        };
        layout.AddChild(livingWorld);
        var feedback = CreateButton("Report Bug / Request Feature  [F9]");
        feedback.Pressed += OpenFeedbackPortal;
        layout.AddChild(feedback);
        _menuSaveButton = CreateButton("Save Position");
        _menuSaveButton.Pressed += () => SaveRequested?.Invoke(PlayerPosition);
        layout.AddChild(_menuSaveButton);
        _menuReturnButton = CreateButton("Character Selection");
        _menuReturnButton.Pressed += () => ReturnRequested?.Invoke(PlayerPosition);
        layout.AddChild(_menuReturnButton);
        _menuLogoutButton = CreateButton("Save and Log Out");
        _menuLogoutButton.Pressed += () => LogoutRequested?.Invoke(PlayerPosition);
        layout.AddChild(_menuLogoutButton);
        var note = CreateLabel("Movement and creature positions autosave while you explore. Combat progress is saved immediately.", 13, new Color("aaa294"));
        note.HorizontalAlignment = HorizontalAlignment.Center;
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(note);
    }

    private void BuildInventoryOverlay(Control root)
    {
        _inventoryOverlay = new ColorRect
        {
            Name = "InventoryOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Color = new Color(0.005f, 0.005f, 0.008f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        root.AddChild(_inventoryOverlay);
        var center = new CenterContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        _inventoryOverlay.AddChild(center);
        var panel = CreatePanel();
        panel.CustomMinimumSize = new Vector2(760, 560);
        center.AddChild(panel);
        var margin = CreateMargin(28, 22, 28, 22);
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 10);
        margin.AddChild(layout);
        var heading = CreateLabel("INVENTORY & EQUIPMENT", 28, Gold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(heading);
        _inventoryStats = CreateLabel("Loading equipment statistics...", 15, new Color("d35a45"));
        _inventoryStats.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_inventoryStats);
        var buyers = CreateLabel(
            "BRANN buys weapons and armor  •  ELOWEN buys tonics  •  OREN buys materials and trophies\n" +
            "Stand beside the named buyer to sell. Construction projects consume carried timber and stone.",
            12,
            new Color("d7c7a5"));
        buyers.HorizontalAlignment = HorizontalAlignment.Center;
        buyers.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(buyers);
        var divider = new HSeparator();
        layout.AddChild(divider);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 390),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);
        _inventoryRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _inventoryRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_inventoryRows);
        var close = CreateButton("Close Inventory  [I]");
        close.Pressed += () => SetInventoryOpen(false);
        layout.AddChild(close);
    }

    private void BuildWorldOverlay(Control root)
    {
        _worldOverlay = new ColorRect
        {
            Name = "LivingWorldOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Color = new Color(0.005f, 0.005f, 0.008f, 0.86f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        root.AddChild(_worldOverlay);
        var center = new CenterContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        _worldOverlay.AddChild(center);
        var panel = CreatePanel();
        panel.CustomMinimumSize = new Vector2(940, 680);
        center.AddChild(panel);
        var margin = CreateMargin(30, 22, 30, 22);
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 10);
        margin.AddChild(layout);

        var heading = CreateLabel("THE LIVING WORLD", 30, Gold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(heading);
        _worldSummary = CreateLabel("Loading the Darkwood Clan...", 16, new Color("d35a45"));
        _worldSummary.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_worldSummary);
        layout.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 470),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(content);
        _worldDetails = CreateLabel("Persistent faction state is loading...", 14, Parchment);
        _worldDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_worldDetails);
        var readinessHeading = CreateLabel("CAMPAIGN READINESS", 22, Gold);
        content.AddChild(readinessHeading);
        _darkwoodReadiness = CreateLabel("Loading Darkwood campaign readiness...", 15, Parchment);
        _darkwoodReadiness.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_darkwoodReadiness);
        _counterattackReadiness = CreateLabel("Loading Stonehaven campaign readiness...", 15, Parchment);
        _counterattackReadiness.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_counterattackReadiness);
        var raidHeading = CreateLabel("ACTIVE CAMPAIGN / LAST BATTLE", 22, new Color("d35a45"));
        content.AddChild(raidHeading);
        _raidDetails = CreateLabel("Loading Stonehaven's raid state...", 14, Parchment);
        _raidDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_raidDetails);
        var chronicleHeading = CreateLabel("WORLD CHRONICLE", 22, Gold);
        content.AddChild(chronicleHeading);
        _chronicleText = CreateLabel("No chronicles have been recorded yet.", 14, new Color("d7c7a5"));
        _chronicleText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_chronicleText);

        var actions = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        actions.AddThemeConstantOverride("h_separation", 14);
        actions.AddThemeConstantOverride("v_separation", 8);
        layout.AddChild(actions);
        _startRaidButton = CreateButton("Authorize Darkwood Raid");
        _startRaidButton.Visible = false;
        _startRaidButton.Pressed += () =>
        {
            SetRaidStartBusy(true);
            RaidStartRequested?.Invoke();
        };
        actions.AddChild(_startRaidButton);
        _startCounterattackButton = CreateButton("Authorize Stonehaven Counterattack");
        _startCounterattackButton.Visible = false;
        _startCounterattackButton.Pressed += () =>
        {
            SetCounterattackStartBusy(true);
            CounterattackStartRequested?.Invoke();
        };
        actions.AddChild(_startCounterattackButton);
        _advanceWorldButton = CreateButton("Advance 24 World Hours  [Playtest]");
        _advanceWorldButton.Visible = false;
        _advanceWorldButton.Pressed += () =>
        {
            SetWorldAdvanceBusy(true);
            WorldAdvanceRequested?.Invoke(24);
        };
        actions.AddChild(_advanceWorldButton);
        _resetWorldButton = CreateButton("Reset Living World  [Playtest]");
        _resetWorldButton.Visible = false;
        _resetWorldButton.Pressed += () =>
        {
            if (_resetConfirmationSeconds <= 0)
            {
                _resetConfirmationSeconds = 5.0f;
                _resetWorldButton.Text = "Click Again to Confirm Reset";
                return;
            }
            _resetConfirmationSeconds = 0;
            SetWorldResetBusy(true);
            WorldResetRequested?.Invoke();
        };
        actions.AddChild(_resetWorldButton);
        _refreshWorldButton = CreateButton("Refresh World State");
        _refreshWorldButton.Visible = false;
        _refreshWorldButton.Pressed += () => WorldStateRequested?.Invoke();
        actions.AddChild(_refreshWorldButton);
        var close = CreateButton("Close Chronicles  [J]");
        close.Pressed += () => SetWorldOpen(false);
        actions.AddChild(close);
    }

    private static string StructurePurpose(string structureName) => structureName switch
    {
        "Hide Tents" => "houses the clan",
        "Crude Stockpile" => "stores early resources",
        "Timber Palisade" => "protects the camp",
        "Hunter Lodge" => "supplies food and recruits",
        "Darkwood Watchtower" => "extends sight and defense",
        "Iron Workshop" => "improves weapons and armor",
        _ => "supports clan growth"
    };

    private static string FormatRaidStatus(string status) => status switch
    {
        "DefendersWon" => "Stonehaven held",
        "AttackersWon" => "Darkwood breached Stonehaven",
        "Cancelled" => "Cancelled",
        _ => status
    };

    private static string FormatDarkwoodRaidPhase(string phase) => phase switch
    {
        "Assembling" => "assembling at Darkwood",
        "Marching" => "marching on Stonehaven",
        "FightingDefenders" => "fighting Stonehaven's defenders",
        "AttackingStructures" => "destroying Stonehaven's structures",
        "Resolved" => "resolved",
        _ => phase
    };

    private static string DarkwoodRaidInstruction(string phase) => phase switch
    {
        "Assembling" => "The selected Darkwood fighters are gathering at their camp. The campaign remains persisted on the server.",
        "Marching" => "The raiders are crossing the connected grids toward Stonehaven. They cannot vanish or time out.",
        "FightingDefenders" => "The raid must defeat Stonehaven's living guards and militia before any structure can be damaged.",
        "AttackingStructures" => "Surviving raiders are striking walls, gates, farms, and buildings by their persistent hit points.",
        _ => "The campaign continues until one force is defeated or its settlement objective is destroyed."
    };

    private static bool IsActiveCounterattackStatus(string status) => status is
        "Assembling" or "Marching" or "FightingGoblins" or "AttackingCamp";

    private static string FormatCounterattackStatus(string status) => status switch
    {
        "Assembling" => "assembling at Stonehaven's gate",
        "Marching" => "marching to Darkwood",
        "FightingGoblins" => "fighting Darkwood's goblins",
        "AttackingCamp" => "destroying the Darkwood camp",
        "StonehavenVictory" => "Stonehaven reduced the camp",
        "DarkwoodVictory" => "Darkwood held the camp",
        "Cancelled" => "cancelled",
        _ => status
    };

    private static string CounterattackInstruction(string status) => status switch
    {
        "Assembling" => "Twenty named residents are assembling under Guard Captain Mira at Stonehaven's gate.",
        "Marching" => "The formation is marching across the valley toward Darkwood.",
        "FightingGoblins" => "The soldiers must defeat every living goblin before they can damage the camp.",
        "AttackingCamp" => "The surviving soldiers are tearing down the level 3 camp. At zero camp strength, Darkwood loses one level.",
        _ => "The result is recorded in the World Chronicle."
    };

    private void SetMenuOpen(bool open)
    {
        if (open)
        {
            CloseSecondaryOverlays();
        }
        _menuOpen = open;
        _menuOverlay.Visible = open;
        ApplyOverlayPauseState();
    }

    private void SetInventoryOpen(bool open)
    {
        if (open)
        {
            _menuOpen = false;
            _menuOverlay.Visible = false;
            if (_worldOpen)
            {
                _worldOpen = false;
                _worldOverlay.Visible = false;
            }
        }
        _inventoryOpen = open;
        _inventoryOverlay.Visible = open;
        ApplyOverlayPauseState();
    }

    private void SetWorldOpen(bool open)
    {
        if (open)
        {
            _menuOpen = false;
            _menuOverlay.Visible = false;
            if (_inventoryOpen)
            {
                _inventoryOpen = false;
                _inventoryOverlay.Visible = false;
            }
        }
        _worldOpen = open;
        _worldOverlay.Visible = open;
        ApplyOverlayPauseState();
    }

    private void CloseSecondaryOverlays()
    {
        _inventoryOpen = false;
        _inventoryOverlay.Visible = false;
        _worldOpen = false;
        _worldOverlay.Visible = false;
    }

    private void ApplyOverlayPauseState()
    {
        var paused = _menuOpen || _inventoryOpen || _worldOpen;
        _player.InputEnabled = !paused;
        _player.CombatEnabled = !paused && _knockoutProtectionSeconds <= 0;
        foreach (var creature in _creatures.Values)
        {
            creature.AiEnabled = !paused;
            creature.PlayerTargetable = _knockoutProtectionSeconds <= 0;
        }
        foreach (var resident in _residents.Values)
        {
            resident.AiEnabled = !paused;
        }
        Input.MouseMode = paused || _mouseReleasedForSharing
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }

    private static Color RarityColor(string rarity) => rarity.ToLowerInvariant() switch
    {
        "rare" => new Color("68a9ef"),
        "uncommon" => new Color("72c77a"),
        "epic" => new Color("c37cf0"),
        _ => Parchment
    };

    private static PanelContainer CreatePanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", CreateFlatStyle(new Color(0.03f, 0.03f, 0.042f, 0.95f), new Color("9a6d2e"), 7));
        return panel;
    }

    private static StyleBoxFlat CreateFlatStyle(Color background, Color border, int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };
    }

    private static MarginContainer CreateMargin(int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    private static Label CreateLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 44),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color("f2cf7a"));
        button.AddThemeStyleboxOverride("normal", CreateFlatStyle(new Color("43120f"), new Color("a97931"), 5));
        button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color("6b1d16"), new Color("e0b454"), 5));
        button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color("2e0d0b"), new Color("f0c869"), 5));
        return button;
    }

    private Vector3 SanitizeSpawn(Vector3 savedPosition)
    {
        if (!savedPosition.IsFinite() ||
            Mathf.Abs(savedPosition.X) > PlayableWorldLimit ||
            Mathf.Abs(savedPosition.Z) > PlayableWorldLimit ||
            savedPosition.Y is < -1 or > 18)
        {
            return _characterName.Equals("Elara", StringComparison.OrdinalIgnoreCase)
                ? new Vector3(2, 0.08f, 8)
                : new Vector3(-2, 0.08f, 8);
        }

        return new Vector3(savedPosition.X, Mathf.Max(savedPosition.Y, 0.08f), savedPosition.Z);
    }

    private static Vector3 SanitizeCreatureSavePosition(Vector3 position)
    {
        if (!position.IsFinite())
        {
            return Vector3.Zero;
        }

        return new Vector3(
            Mathf.Clamp(position.X, -139.0f, 139.0f),
            Mathf.Clamp(position.Y, 0.08f, 18.0f),
            Mathf.Clamp(position.Z, -139.0f, 139.0f));
    }

    private static (string Code, string Name) GetWorldGrid(Vector3 position)
    {
        var (physicalColumn, physicalRow) = GetPhysicalGridCell(position);
        var code = GetDisplayGridCode(physicalColumn, physicalRow);
        return (code, WorldGridNames[physicalRow, physicalColumn]);
    }

    private static (int PhysicalColumn, int PhysicalRow) GetPhysicalGridCell(Vector3 position) => (
        Math.Clamp(Mathf.FloorToInt((position.X + WorldHalfExtent) / WorldGridSize), 0, 2),
        Math.Clamp(Mathf.FloorToInt((position.Z + WorldHalfExtent) / WorldGridSize), 0, 2));

    private static string GetDisplayGridCode(int physicalColumn, int physicalRow)
    {
        var displayColumn = 2 - physicalColumn;
        var displayRow = 2 - physicalRow;
        return $"{(char)('A' + displayColumn)}{displayRow + 1}";
    }

    private sealed record NaturalResourceTarget(
        Node3D Body,
        Label3D Label,
        string Kind,
        string Name);
}

public sealed record WorldCreatureData(
    Guid Id,
    string SpeciesKey,
    string SpeciesName,
    string Name,
    string? Title,
    string? Role,
    int Level,
    int Health,
    int MaximumHealth,
    int Attack,
    int Defense,
    float MovementSpeed,
    float DetectionRadius,
    float AttackRange,
    int Aggression,
    string Status,
    Vector3 Position,
    Vector3 SpawnPosition,
    DateTimeOffset? RespawnAt,
    bool IsBoss,
    bool IsRaidAttacker = false);

public sealed record WorldCreaturePosition(Guid Id, Vector3 Position);

public sealed record WorldResidentData(
    Guid Id,
    string Name,
    string Role,
    int Health,
    int MaximumHealth,
    string Status,
    bool CanFight,
    IReadOnlyCollection<string> Skills,
    string PrimarySkill,
    int SkillLevel,
    string Trait,
    long Experience,
    bool IsMajor,
    string MemorySummary,
    string Activity,
    Vector3 Position,
    Vector3 HomePosition,
    Vector3 WorkPosition,
    Vector3 SafePosition,
    string Dialogue,
    int WorldHour,
    int WorldDay);

public sealed record WorldRaidStateData(
    bool HasRaid,
    bool Active,
    bool CanStartPlaytest,
    bool DarkwoodRaidReady,
    bool StonehavenCounterattackReady,
    bool AdministratorOnline,
    bool IsAdministrator,
    bool CanStartDarkwoodRaid,
    bool CanStartCounterattack,
    WorldRaidData? Raid,
    WorldCounterattackData? Counterattack);

public sealed record WorldRaidData(
    Guid Id,
    string Status,
    string Phase,
    int PhaseRound,
    int WorldDay,
    int InitialAttackerStrength,
    int AttackerStrength,
    int InitialDefenderStrength,
    int DefenderStrength,
    int InitialStructureStrength,
    int StructureStrength,
    int PlayerContribution,
    int SettlementDamage,
    int ResidentCasualties,
    int ResidentInjuries,
    string? OutcomeSummary,
    IReadOnlyCollection<WorldRaidAttackerData> Attackers);

public sealed record WorldRaidAttackerData(
    Guid CreatureId,
    string Name,
    int Level,
    int Health,
    int MaximumHealth,
    string Status,
    bool IsDefeated,
    bool DefeatedByPlayer);

public sealed record WorldCounterattackData(
    Guid Id,
    string Status,
    int WorldDay,
    int InitialSoldierCount,
    int SoldiersRemaining,
    int InitialGoblinCount,
    int GoblinsRemaining,
    int CampLevelBefore,
    int CampLevelAfter,
    int InitialCampStrength,
    int CampStrength,
    int StonehavenCasualties,
    int DarkwoodCasualties,
    string? OutcomeSummary,
    IReadOnlyCollection<WorldCounterattackMemberData> Members);

public sealed record WorldCounterattackMemberData(
    Guid ResidentId,
    string Name,
    string Role,
    int Health,
    int MaximumHealth,
    string Status,
    bool IsDefeated);

public sealed record WorldInventoryData(
    int Attack,
    int Defense,
    int Gold,
    int UsedCapacity,
    int CarryCapacity,
    IReadOnlyCollection<WorldInventoryItem> Items);

public sealed record WorldInventoryItem(
    Guid Id,
    string Key,
    string Name,
    string Kind,
    string Rarity,
    string? EquipmentSlot,
    int AttackBonus,
    int DefenseBonus,
    int HealingAmount,
    int UnitWeight,
    int TotalWeight,
    string BuyerName,
    int Quantity,
    bool IsEquipped);

public sealed record WorldSkillData(
    string Key,
    string Name,
    string Description,
    string Hotkey,
    double CooldownSeconds,
    bool IsOffensive,
    float Range);

public sealed record WorldStateData(
    long SimulatedHours,
    int WorldDay,
    string SimulationSpeed,
    bool CanAccelerate,
    WorldFactionData Faction,
    WorldSettlementData Settlement,
    WorldSurvivalData Survival,
    WorldIronEconomyData IronEconomy,
    WorldFactionBanksData Banks,
    WorldSettlementRecoveriesData Recovery,
    IReadOnlyCollection<DestructibleStructureData> Structures,
    WorldEventReadinessData EventReadiness,
    WorldEventQueueData Events,
    IReadOnlyCollection<WorldHistoryData> RecentHistory);

public sealed record WorldFactionData(
    string Name,
    int Population,
    int PopulationCapacity,
    int DevelopmentStage,
    string StageName,
    int TerritorySize,
    int Aggression,
    int Morale,
    int TechnologyLevel,
    int MilitaryStrength,
    IReadOnlyCollection<WorldResourceData> Resources,
    IReadOnlyCollection<WorldStructureData> Structures,
    WorldLeaderData Leader);

public sealed record WorldResourceData(string Kind, long Amount, long Capacity);
public sealed record WorldStructureData(string Name, int Level, int Health);
public sealed record WorldLeaderData(
    string Name,
    string Title,
    int Level,
    int Leadership,
    int Health,
    int MaximumHealth,
    int Attack,
    int Defense,
    string Status);
public sealed record WorldSettlementData(
    string Name,
    int Population,
    int LivingResidents,
    int CombatReadyResidents,
    int HousingCapacity,
    int Food,
    int Wood,
    int Stone,
    int Iron,
    int DefenseRating,
    int GuardStrength,
    WorldSettlementLeaderData Leader);
public sealed record WorldSettlementLeaderData(
    string Name,
    string Title,
    string Role,
    int Health,
    int MaximumHealth,
    string Status,
    string PrimarySkill,
    int SkillLevel,
    string Trait,
    bool IsMajor,
    string MemorySummary);
public sealed record WorldSurvivalData(
    WorldFoodEconomyData Stonehaven,
    WorldFoodEconomyData Darkwood,
    WorldWildlifeData Wildlife);
public sealed record WorldFoodEconomyData(
    int Population,
    int FoodStored,
    int Farmers,
    int Fishers,
    int Hunters,
    int FarmerProductionPerHour,
    int FisherProductionPerHour,
    int HunterProductionPerHour,
    int FoodProducedPerHour,
    int FoodConsumedPerHour,
    int NetFoodPerHour,
    bool IsShortage,
    int HoursOfFoodRemaining,
    string RecommendedRecruitmentRole);
public sealed record WorldWildlifeData(int Total, int Available, int Respawning);
public sealed record WorldIronEconomyData(
    WorldIronSourceData Source,
    WorldIronOperationData Stonehaven,
    WorldIronOperationData Darkwood,
    WorldMineGuardData StonehavenMineGuards);
public sealed record WorldIronSourceData(
    string Grid,
    string Name,
    int Remaining,
    int Capacity,
    int MineHealth,
    int MineMaximumHealth,
    bool Operational);
public sealed record WorldIronOperationData(
    string Owner,
    string MinerName,
    string Status,
    float PositionX,
    float PositionY,
    float PositionZ,
    int CargoIron,
    int TotalIronDelivered,
    int TripsCompleted,
    long StoredIron,
    int WeaponTier,
    int? NextWeaponTierCost,
    int ArmorTier,
    int? NextArmorTierCost);
public sealed record WorldMineGuardData(
    int Count,
    int GoldPerGuardPerWorldDay,
    int CurrentDailyCost,
    int TreasuryGold,
    IReadOnlyCollection<string> Names);
public sealed record WorldFactionBanksData(
    WorldFactionBankData Stonehaven,
    WorldFactionBankData Darkwood);
public sealed record WorldSettlementRecoveriesData(
    WorldSettlementRecoveryData Stonehaven,
    WorldSettlementRecoveryData Darkwood);
public sealed record WorldSettlementRecoveryData(
    string Owner,
    string Name,
    string Status,
    int FoundingPopulation,
    DateTimeOffset? DefeatedAtCentral,
    DateTimeOffset? RecoveryEligibleAtCentral,
    int RecoverySecondsRemaining,
    DateTimeOffset? RebuildingStartedAtCentral,
    DateTimeOffset? LastProgressedAtCentral,
    DateTimeOffset? RecoveredAtCentral,
    string? CurrentStructureKey,
    int RebuildCycles,
    int FunctionalStructuresRestored,
    int FunctionalStructuresTotal,
    int DefensesRestored,
    int DefensesTotal,
    int StructureHealth,
    int StructureMaximumHealth);
public sealed record WorldFactionBankData(
    string Owner,
    string Name,
    int BankGold,
    int FactionGold,
    IReadOnlyCollection<WorldBankInventoryData> Inventory,
    IReadOnlyCollection<WorldBankTransactionData> RecentTransactions);
public sealed record WorldBankInventoryData(
    string Kind,
    int BankQuantity,
    int BankBuyPrice,
    int BankSellPrice,
    long FactionStored,
    int TargetReserve,
    long Shortage,
    DateTimeOffset? LastPurchasedCentral,
    DateTimeOffset? LastSoldCentral);
public sealed record WorldBankTransactionData(
    string Type,
    string Kind,
    int Quantity,
    int UnitPrice,
    int TotalGold,
    int BankGoldAfter,
    int FactionGoldAfter,
    string Description,
    DateTimeOffset OccurredAtCentral);
public sealed record WorldEventReadinessData(
    WorldTriggerReadinessData DarkwoodRaid,
    WorldTriggerReadinessData StonehavenCounterattack);
public sealed record WorldTriggerReadinessData(
    string Name,
    int Current,
    int Required,
    bool Ready,
    bool Active,
    bool AdministratorOnline,
    string Explanation);
public sealed record WorldEventQueueData(int Pending, int Completed, int Failed);
public sealed record WorldHistoryData(string Title, string Description, DateTimeOffset OccurredAtCentral);

public sealed record DevelopmentStateData(
    IReadOnlyCollection<ResourceNodeData> Nodes,
    IReadOnlyCollection<ConstructionProjectData> Projects,
    IReadOnlyCollection<DestructibleStructureData> Structures,
    IReadOnlyCollection<ResourceContributionData> RecentContributions,
    int SettlementWood,
    int SettlementStone);

public sealed record ResourceNodeData(
    Guid Id,
    string Key,
    string Name,
    string Kind,
    string Owner,
    Vector3 Position,
    int Remaining,
    int Capacity,
    int YieldPerHarvest,
    DateTimeOffset? RespawnAt);

public sealed record ConstructionProjectData(
    Guid Id,
    string Key,
    string Name,
    string Owner,
    int WoodRequired,
    int StoneRequired,
    int WoodContributed,
    int StoneContributed,
    int CurrentLevel,
    int MaximumLevel,
    float Progress,
    string Stage,
    Vector3 Position,
    DateTimeOffset? CompletedAt);

public sealed record DestructibleStructureData(
    Guid Id,
    string Key,
    string Name,
    string Owner,
    string Kind,
    int Health,
    int MaximumHealth,
    int Armor,
    bool IsBuilt,
    bool BlocksMovement,
    string Status,
    int ProjectLevel,
    Vector3 Position,
    DateTimeOffset? LastDamagedAt,
    DateTimeOffset? DestroyedAt);

public sealed record ResourceContributionData(
    string ContributorName,
    string Kind,
    int Amount,
    string Source,
    DateTimeOffset OccurredAt);
