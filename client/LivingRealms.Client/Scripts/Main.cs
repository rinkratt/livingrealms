using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Godot;
using NetHttpClient = System.Net.Http.HttpClient;
using NetHttpMethod = System.Net.Http.HttpMethod;
using NetHttpRequestMessage = System.Net.Http.HttpRequestMessage;
using NetStringContent = System.Net.Http.StringContent;

namespace LivingRealms.Client;

public partial class Main : Control
{
    private const string ClientVersion = "0.9.11";
    private const string UpdateManifestUrl = "https://living-realms.com/downloads/windows-version.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NetHttpClient _apiClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };
    private ColorRect _background = null!;
    private Control _page = null!;
    private PanelContainer _authPanel = null!;
    private PanelContainer _realmPanel = null!;
    private LineEdit _email = null!;
    private LineEdit _password = null!;
    private Button _passwordVisibilityButton = null!;
    private Button _loginButton = null!;
    private Button _registerButton = null!;
    private Label _authStatus = null!;
    private Label _welcome = null!;
    private TextureButton _aldenButton = null!;
    private TextureButton _elaraButton = null!;
    private Label _characterDetails = null!;
    private Label _realmStatus = null!;
    private SpinBox _positionX = null!;
    private SpinBox _positionY = null!;
    private SpinBox _positionZ = null!;
    private Button _savePositionButton = null!;
    private Button _logoutButton = null!;
    private string _apiBaseUrl = string.Empty;
    private string? _token;
    private CharacterResponse? _alden;
    private CharacterResponse? _elara;
    private CharacterResponse? _selectedCharacter;
    private StonehavenValley? _world;
    private readonly SemaphoreSlim _worldApiGate = new(1, 1);
    private int _playerCombatRequestInFlight;
    private int _creatureCombatRequestInFlight;
    private DateTimeOffset _creatureCombatCooldownUntil;
    private DateTimeOffset _combatBackoffUntil;
    private bool _quitting;
    private bool _isAdministrator;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false;
        _apiClient.DefaultRequestHeaders.UserAgent.ParseAdd($"LivingRealms/{ClientVersion}");
        _background = GetNode<ColorRect>("Background");
        _page = GetNode<Control>("Page");
        _authPanel = GetNode<PanelContainer>("Page/Layout/Content/AuthPanel");
        _realmPanel = GetNode<PanelContainer>("Page/Layout/Content/RealmPanel");
        _email = GetNode<LineEdit>("Page/Layout/Content/AuthPanel/Fields/Email");
        _password = GetNode<LineEdit>("Page/Layout/Content/AuthPanel/Fields/PasswordRow/Password");
        _passwordVisibilityButton = GetNode<Button>("Page/Layout/Content/AuthPanel/Fields/PasswordRow/TogglePassword");
        _loginButton = GetNode<Button>("Page/Layout/Content/AuthPanel/Fields/Actions/Login");
        _registerButton = GetNode<Button>("Page/Layout/Content/AuthPanel/Fields/Actions/Register");
        _authStatus = GetNode<Label>("Page/Layout/Content/AuthPanel/Fields/Status");
        SetAuthStatus(_authStatus.Text, isError: false);
        _welcome = GetNode<Label>("Page/Layout/Content/RealmPanel/Layout/AccountRow/Welcome");
        _logoutButton = GetNode<Button>("Page/Layout/Content/RealmPanel/Layout/AccountRow/Logout");
        _aldenButton = GetNode<TextureButton>("Page/Layout/Content/RealmPanel/Layout/CharacterRow/AldenCard/AldenButton");
        _elaraButton = GetNode<TextureButton>("Page/Layout/Content/RealmPanel/Layout/CharacterRow/ElaraCard/ElaraButton");
        _characterDetails = GetNode<Label>("Page/Layout/Content/RealmPanel/Layout/CharacterDetails");
        _positionX = GetNode<SpinBox>("Page/Layout/Content/RealmPanel/Layout/PositionRow/X");
        _positionY = GetNode<SpinBox>("Page/Layout/Content/RealmPanel/Layout/PositionRow/Y");
        _positionZ = GetNode<SpinBox>("Page/Layout/Content/RealmPanel/Layout/PositionRow/Z");
        _savePositionButton = GetNode<Button>("Page/Layout/Content/RealmPanel/Layout/PositionRow/SavePosition");
        _realmStatus = GetNode<Label>("Page/Layout/Content/RealmPanel/Layout/Status");

        var configuredUrl = OS.GetEnvironment("LIVING_REALMS_API_URL");
        _apiBaseUrl = string.IsNullOrWhiteSpace(configuredUrl)
            ? "https://living-realms.com/game-api"
            : configuredUrl.TrimEnd('/');
        GetNode<Label>("Page/Layout/Content/AuthPanel/Fields/ApiAddress").Text = $"API: {_apiBaseUrl}";

        _loginButton.Pressed += OnLoginPressed;
        _registerButton.Pressed += OnRegisterPressed;
        _logoutButton.Pressed += OnLogoutPressed;
        _aldenButton.Pressed += OnAldenPressed;
        _elaraButton.Pressed += OnElaraPressed;
        _savePositionButton.Pressed += OnSavePositionPressed;
        _password.TextSubmitted += OnPasswordSubmitted;
        _passwordVisibilityButton.Toggled += OnPasswordVisibilityToggled;

        GD.Print($"Living Realms client {ClientVersion} initialized.");
        CheckForClientUpdateAsync();
    }

    private async void CheckForClientUpdateAsync()
    {
        if (OS.HasFeature("editor"))
        {
            SetAuthStatus($"Development build {ClientVersion}. Automatic installation is disabled in the editor.", false);
            return;
        }

        SetAuthBusy(true, $"Build {ClientVersion} • Checking for updates...");
        try
        {
            using var client = new NetHttpClient
            {
                Timeout = TimeSpan.FromMinutes(15)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"LivingRealms/{ClientVersion}");

            var manifestJson = await client.GetStringAsync($"{UpdateManifestUrl}?client={Uri.EscapeDataString(ClientVersion)}&t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            var manifest = JsonSerializer.Deserialize<ClientUpdateManifest>(manifestJson, JsonOptions)
                ?? throw new InvalidOperationException("The update manifest was empty.");
            if (!Version.TryParse(ClientVersion, out var installedVersion) ||
                !Version.TryParse(manifest.Version, out var availableVersion))
            {
                throw new InvalidOperationException("The update manifest contained an invalid version.");
            }

            if (availableVersion <= installedVersion)
            {
                SetAuthBusy(false, $"Build {ClientVersion} is current. Sign in or create a player account.");
                return;
            }

            if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var packageUri) ||
                packageUri.Scheme != Uri.UriSchemeHttps ||
                !packageUri.Host.Equals("living-realms.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The update package address was not trusted.");
            }
            if (string.IsNullOrWhiteSpace(manifest.Sha256) || manifest.Sha256.Length != 64)
            {
                throw new InvalidOperationException("The update package checksum was invalid.");
            }

            SetAuthBusy(true, $"Build {manifest.Version} is available • Downloading and verifying the update...");
            var packageBytes = await client.GetByteArrayAsync(packageUri);
            if (manifest.SizeBytes > 0 && packageBytes.LongLength != manifest.SizeBytes)
            {
                throw new InvalidOperationException("The downloaded update size did not match the manifest.");
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(packageBytes));
            if (!actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The downloaded update failed checksum verification.");
            }

            var updateDirectory = ProjectSettings.GlobalizePath("user://updates");
            Directory.CreateDirectory(updateDirectory);
            var packagePath = Path.Combine(updateDirectory, $"LivingRealms-{manifest.Version}.zip");
            await File.WriteAllBytesAsync(packagePath, packageBytes);

            var executablePath = OS.GetExecutablePath();
            var installDirectory = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException("The game installation folder could not be found.");
            var bundledUpdaterPath = Path.Combine(installDirectory, "LivingRealms.Updater.ps1");
            if (!File.Exists(bundledUpdaterPath))
            {
                throw new FileNotFoundException("The automatic updater helper is missing.", bundledUpdaterPath);
            }

            var cachedUpdaterPath = Path.Combine(updateDirectory, "LivingRealms.Updater.ps1");
            File.Copy(bundledUpdaterPath, cachedUpdaterPath, true);
            var logPath = Path.Combine(updateDirectory, "update.log");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                WorkingDirectory = installDirectory
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(cachedUpdaterPath);
            startInfo.ArgumentList.Add("-PackagePath");
            startInfo.ArgumentList.Add(packagePath);
            startInfo.ArgumentList.Add("-InstallDirectory");
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add("-WaitForProcessId");
            startInfo.ArgumentList.Add(OS.GetProcessId().ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-ExecutableName");
            startInfo.ArgumentList.Add(Path.GetFileName(executablePath));
            startInfo.ArgumentList.Add("-LogPath");
            startInfo.ArgumentList.Add(logPath);

            SetAuthStatus($"Build {manifest.Version} verified. Living Realms will restart automatically.", false);
            var updater = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("The updater could not be started.");
            updater.Dispose();
            _quitting = true;
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Living Realms update check failed: {exception.Message}");
            SetAuthBusy(false, $"Build {ClientVersion} • Update check unavailable. You may continue to sign in.");
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest || _quitting)
        {
            return;
        }

        _quitting = true;
        if (_world is not null && _selectedCharacter is not null)
        {
            SaveWorldAndQuitAsync();
            return;
        }

        GetTree().Quit();
    }

    private async void OnLoginPressed()
    {
        await AuthenticateAsync(register: false);
    }

    private async void OnRegisterPressed()
    {
        await AuthenticateAsync(register: true);
    }

    private async void OnPasswordSubmitted(string _)
    {
        await AuthenticateAsync(register: false);
    }

    private void OnPasswordVisibilityToggled(bool isVisible)
    {
        _password.Secret = !isVisible;
        _passwordVisibilityButton.TooltipText = isVisible ? "Hide password" : "Show password";
    }

    private async void OnAldenPressed()
    {
        await SelectCharacterAsync(_alden);
    }

    private async void OnElaraPressed()
    {
        await SelectCharacterAsync(_elara);
    }

    private async void OnSavePositionPressed()
    {
        if (_selectedCharacter is null)
        {
            return;
        }

        SetRealmBusy(true, "Saving position...");
        var payload = JsonSerializer.Serialize(new PositionRequest(
            (float)_positionX.Value,
            (float)_positionY.Value,
            (float)_positionZ.Value));
        var response = await SendAsync(
            $"/api/v1/characters/{_selectedCharacter.Id:D}/position",
            Godot.HttpClient.Method.Put,
            payload,
            authenticated: true);
        SetRealmBusy(false, string.Empty);

        if (!response.IsSuccess)
        {
            _realmStatus.Text = ReadError(response);
            return;
        }

        var character = JsonSerializer.Deserialize<CharacterResponse>(response.Body, JsonOptions);
        if (character is null)
        {
            _realmStatus.Text = "The server returned an unreadable character response.";
            return;
        }

        RememberCharacter(character);
        ShowCharacter(character);
        _realmStatus.Text = "Position saved. It will be restored the next time this character is loaded.";
    }

    private async void OnLogoutPressed()
    {
        SetRealmBusy(true, "Closing session...");
        _ = await SendAsync("/api/v1/auth/logout", Godot.HttpClient.Method.Post, string.Empty, authenticated: true);
        ResetToLogin("Session closed.");
    }

    private async void OnWorldSaveRequested(Vector3 position)
    {
        await SaveWorldPositionAsync(position);
    }

    private async void OnWorldReturnRequested(Vector3 position)
    {
        if (!await SaveWorldPositionAsync(position))
        {
            return;
        }

        ExitWorldToCharacterSelection();
        _realmStatus.Text = $"{_selectedCharacter?.Name}'s position was saved. Choose a character to continue.";
    }

    private async void OnWorldLogoutRequested(Vector3 position)
    {
        if (!await SaveWorldPositionAsync(position))
        {
            return;
        }

        _world?.SetSaving(true);
        _world?.SetSaveStatus("Closing the session...", false);
        _ = await SendAsync("/api/v1/auth/logout", Godot.HttpClient.Method.Post, string.Empty, authenticated: true);
        DestroyWorld();
        ResetToLogin("Position saved and session closed.");
    }

    private async void OnWorldPlayerAttackRequested(
        Guid creatureId,
        Vector3 playerPosition,
        Vector3 creaturePosition)
    {
        if (DateTimeOffset.UtcNow < _combatBackoffUntil ||
            Interlocked.Exchange(ref _playerCombatRequestInFlight, 1) == 1)
        {
            return;
        }
        try
        {
            await ResolveCombatAsync(
                "/api/v1/combat/player-attack",
                creatureId,
                playerPosition,
                creaturePosition,
                playerAttack: true);
        }
        finally
        {
            Interlocked.Exchange(ref _playerCombatRequestInFlight, 0);
        }
    }

    private async void OnWorldCreatureAttackRequested(
        Guid creatureId,
        Vector3 playerPosition,
        Vector3 creaturePosition)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _combatBackoffUntil ||
            now < _creatureCombatCooldownUntil ||
            Interlocked.Exchange(ref _creatureCombatRequestInFlight, 1) == 1)
        {
            return;
        }
        _creatureCombatCooldownUntil = now.AddMilliseconds(800);
        try
        {
            await ResolveCombatAsync(
                "/api/v1/combat/creature-attack",
                creatureId,
                playerPosition,
                creaturePosition,
                playerAttack: false);
        }
        finally
        {
            Interlocked.Exchange(ref _creatureCombatRequestInFlight, 0);
        }
    }

    private async void OnSettlementDefenseAttackRequested(
        Guid residentId,
        Guid creatureId,
        Vector3 residentPosition,
        Vector3 creaturePosition)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }

        await _worldApiGate.WaitAsync();
        try
        {
            residentPosition = SanitizeNetworkPosition(residentPosition);
            creaturePosition = SanitizeNetworkPosition(creaturePosition);
            var payload = JsonSerializer.Serialize(new SettlementDefenseAttackRequest(
                residentId,
                creatureId,
                new PositionRequest(residentPosition.X, residentPosition.Y, residentPosition.Z),
                new PositionRequest(creaturePosition.X, creaturePosition.Y, creaturePosition.Z)));
            var response = await SendAsync(
                "/api/v1/combat/settlement-defense-attack",
                Godot.HttpClient.Method.Post,
                payload,
                authenticated: true);
            if (!response.IsSuccess)
            {
                return;
            }

            var result = JsonSerializer.Deserialize<SettlementDefenseResponse>(response.Body, JsonOptions);
            if (result is null)
            {
                return;
            }

            world.ApplyCreatureState(ToWorldCreature(result.Creature), flashDamage: true);
            if (result.CreatureDefeated)
            {
                world.SetCombatStatus(result.Message, false);
            }
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async void OnWorldCreatureRefreshRequested()
    {
        await LoadWorldCreaturesAsync();
        await LoadWorldResidentsAsync();
        await LoadRaidStateAsync();
    }

    private async void OnWorldSkillRequested(
        string skillKey,
        Guid? creatureId,
        Vector3 playerPosition,
        Vector3? creaturePosition)
    {
        await UseSkillAsync(skillKey, creatureId, playerPosition, creaturePosition);
    }

    private async void OnWorldInventoryActionRequested(Guid entryId, string action, Vector3 playerPosition)
    {
        await ChangeInventoryAsync(entryId, action, playerPosition);
    }

    private async void OnWorldStateRequested()
    {
        await LoadWorldStateAsync();
    }

    private async void OnDevelopmentStateRequested()
    {
        await LoadDevelopmentStateAsync();
    }

    private async void OnResourceHarvestRequested(Guid nodeId, Vector3 playerPosition)
    {
        await ChangeDevelopmentAsync(
            "/api/v1/development/harvest",
            new HarvestRequest(nodeId, new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z)),
            showMessage: true);
    }

    private async void OnNaturalResourceHarvestRequested(
        string kind,
        Vector3 resourcePosition,
        Vector3 playerPosition)
    {
        await ChangeDevelopmentAsync(
            "/api/v1/development/harvest-natural",
            new NaturalHarvestRequest(
                kind,
                new PositionRequest(resourcePosition.X, resourcePosition.Y, resourcePosition.Z),
                new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z)),
            showMessage: true);
    }

    private async void OnProjectContributionRequested(Guid projectId, Vector3 playerPosition)
    {
        await ChangeDevelopmentAsync(
            "/api/v1/development/contribute",
            new ContributeRequest(projectId, new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z)),
            showMessage: true);
    }

    private async void OnNpcWorkRequested(string workerKey, Guid nodeId)
    {
        await ChangeDevelopmentAsync(
            "/api/v1/development/npc-work",
            new NpcWorkRequest(workerKey, nodeId),
            showMessage: false);
    }

    private async void OnWorldAdvanceRequested(int hours)
    {
        await AdvanceWorldAsync(hours);
    }

    private async void OnWorldResetRequested()
    {
        await ResetWorldAsync();
    }

    private async void OnRaidStateRequested()
    {
        await LoadRaidStateAsync();
    }

    private async void OnRaidStartRequested()
    {
        await ChangeRaidAsync("/api/v1/world/raid/start", starting: true);
    }

    private async void OnCounterattackStartRequested()
    {
        await ChangeRaidAsync(
            "/api/v1/world/raid/counterattack/start",
            starting: true,
            counterattack: true);
    }

    private async void OnRaidAdvanceRequested()
    {
        await ChangeRaidAsync("/api/v1/world/raid/advance", starting: false);
    }

    private async Task AuthenticateAsync(bool register)
    {
        if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrEmpty(_password.Text))
        {
            SetAuthStatus("Enter both an email address and password.", isError: true);
            return;
        }

        SetAuthBusy(true, register ? "Creating account..." : "Signing in...");
        var payload = JsonSerializer.Serialize(new CredentialsRequest(_email.Text.Trim(), _password.Text));
        var endpoint = register ? "/api/v1/accounts/register" : "/api/v1/auth/login";
        var response = await SendAsync(endpoint, Godot.HttpClient.Method.Post, payload, authenticated: false);
        SetAuthBusy(false, string.Empty);

        if (!response.IsSuccess)
        {
            SetAuthStatus(ReadError(response), isError: true);
            return;
        }

        var authentication = JsonSerializer.Deserialize<AuthenticationResponse>(response.Body, JsonOptions);
        if (authentication is null || string.IsNullOrWhiteSpace(authentication.Token))
        {
            SetAuthStatus("The server returned an unreadable authentication response.", isError: true);
            return;
        }

        _token = authentication.Token;
        _isAdministrator = authentication.Account.IsAdministrator;
        _password.Clear();
        _passwordVisibilityButton.ButtonPressed = false;
        _alden = authentication.Characters.FirstOrDefault(character => character.Name == "Alden");
        _elara = authentication.Characters.FirstOrDefault(character => character.Name == "Elara");
        _selectedCharacter = null;
        _welcome.Text = $"Welcome, {authentication.Account.Email}";
        _aldenButton.Disabled = _alden is null;
        _elaraButton.Disabled = _elara is null;
        _savePositionButton.Disabled = true;
        _characterDetails.Text = "Select Alden or Elara to restore their saved location.";
        _realmStatus.Text = register
            ? "Account created. Alden and Elara are ready in Stonehaven Valley."
            : "Login accepted. Choose a character.";
        _authPanel.Visible = false;
        _realmPanel.Visible = true;
    }

    private async Task SelectCharacterAsync(CharacterResponse? character)
    {
        if (character is null)
        {
            _realmStatus.Text = "That character is not available.";
            return;
        }

        SetRealmBusy(true, $"Loading {character.Name}...");
        var response = await SendAsync(
            $"/api/v1/characters/{character.Id:D}/select",
            Godot.HttpClient.Method.Post,
            string.Empty,
            authenticated: true);
        SetRealmBusy(false, string.Empty);

        if (!response.IsSuccess)
        {
            _realmStatus.Text = ReadError(response);
            return;
        }

        var selected = JsonSerializer.Deserialize<CharacterResponse>(response.Body, JsonOptions);
        if (selected is null)
        {
            _realmStatus.Text = "The server returned an unreadable character response.";
            return;
        }

        RememberCharacter(selected);
        ShowCharacter(selected);
        _realmStatus.Text = $"{selected.Name} loaded from the saved position.";
        EnterWorld(selected);
    }

    private void EnterWorld(CharacterResponse character)
    {
        DestroyWorld();
        var scene = GD.Load<PackedScene>("res://Scenes/StonehavenValley.tscn");
        if (scene is null)
        {
            _realmStatus.Text = "Stonehaven Valley could not be loaded.";
            return;
        }

        var world = scene.Instantiate<StonehavenValley>();
        world.Configure(
            character.Name,
            character.Archetype,
            character.Level,
            character.Experience,
            character.Health,
            character.MaximumHealth,
            character.Region,
            new Vector3(character.Position.X, character.Position.Y, character.Position.Z),
            _isAdministrator);
        world.SaveRequested += OnWorldSaveRequested;
        world.ReturnRequested += OnWorldReturnRequested;
        world.LogoutRequested += OnWorldLogoutRequested;
        world.PlayerAttackRequested += OnWorldPlayerAttackRequested;
        world.CreatureAttackRequested += OnWorldCreatureAttackRequested;
        world.SettlementDefenseAttackRequested += OnSettlementDefenseAttackRequested;
        world.CreatureRefreshRequested += OnWorldCreatureRefreshRequested;
        world.SkillRequested += OnWorldSkillRequested;
        world.InventoryActionRequested += OnWorldInventoryActionRequested;
        world.WorldStateRequested += OnWorldStateRequested;
        world.DevelopmentStateRequested += OnDevelopmentStateRequested;
        world.ResourceHarvestRequested += OnResourceHarvestRequested;
        world.NaturalResourceHarvestRequested += OnNaturalResourceHarvestRequested;
        world.ProjectContributionRequested += OnProjectContributionRequested;
        world.NpcWorkRequested += OnNpcWorkRequested;
        world.WorldAdvanceRequested += OnWorldAdvanceRequested;
        world.WorldResetRequested += OnWorldResetRequested;
        world.RaidStateRequested += OnRaidStateRequested;
        world.RaidStartRequested += OnRaidStartRequested;
        world.CounterattackStartRequested += OnCounterattackStartRequested;
        world.RaidAdvanceRequested += OnRaidAdvanceRequested;
        _world = world;
        _background.Visible = false;
        _page.Visible = false;
        AddChild(world);
        _ = InitializeWorldAsync(world);
    }

    private async Task InitializeWorldAsync(StonehavenValley world)
    {
        var creaturesLoaded = false;
        for (var attempt = 1; attempt <= 3 && ReferenceEquals(_world, world); attempt++)
        {
            creaturesLoaded = await LoadWorldCreaturesAsync(world);
            if (creaturesLoaded)
            {
                break;
            }

            if (attempt < 3)
            {
                world.SetCombatStatus(
                    $"Creature roster did not arrive. Retrying ({attempt}/3)...",
                    true);
                await ToSignal(GetTree().CreateTimer(1.25), Godot.Timer.SignalName.Timeout);
            }
        }
        if (!ReferenceEquals(_world, world))
        {
            return;
        }
        if (!creaturesLoaded)
        {
            world.SetCombatStatus(
                "Creatures could not be loaded after three attempts. The automatic world refresh will keep retrying.",
                true);
        }
        await LoadWorldResidentsAsync();
        if (!ReferenceEquals(_world, world))
        {
            return;
        }
        await LoadInventoryAsync();
        await LoadSkillsAsync();
        await LoadWorldStateAsync();
        await LoadDevelopmentStateAsync();
        await LoadRaidStateAsync();
    }

    private async Task LoadDevelopmentStateAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            await LoadDevelopmentStateCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task LoadDevelopmentStateCoreAsync(StonehavenValley world)
    {
        var response = await SendAsync(
            "/api/v1/development/state",
            Godot.HttpClient.Method.Get,
            string.Empty,
            authenticated: true);
        if (!response.IsSuccess)
        {
            return;
        }
        var state = JsonSerializer.Deserialize<DevelopmentStateResponse>(response.Body, JsonOptions);
        if (state is not null)
        {
            world.SetDevelopmentState(ToDevelopmentState(state));
        }
    }

    private async Task ChangeDevelopmentAsync(string endpoint, object request, bool showMessage)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            var response = await SendAsync(
                endpoint,
                Godot.HttpClient.Method.Post,
                JsonSerializer.Serialize(request),
                authenticated: true);
            if (!response.IsSuccess)
            {
                if (showMessage && response.StatusCode != 429)
                {
                    world.SetCombatStatus(ReadError(response), true);
                }
                return;
            }
            var result = JsonSerializer.Deserialize<DevelopmentActionResponse>(response.Body, JsonOptions);
            if (result?.State is null)
            {
                return;
            }
            world.SetDevelopmentState(ToDevelopmentState(result.State));
            await LoadInventoryCoreAsync(world);
            if (showMessage)
            {
                world.SetCombatStatus(result.Message, false);
            }
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task<bool> SaveWorldPositionAsync(Vector3 position)
    {
        if (_selectedCharacter is null || _world is null)
        {
            return false;
        }

        var world = _world;
        await _worldApiGate.WaitAsync();
        world.SetSaving(true);
        try
        {
            var payload = JsonSerializer.Serialize(new PositionRequest(position.X, position.Y, position.Z));
            var response = await SendAsync(
                $"/api/v1/characters/{_selectedCharacter.Id:D}/position",
                Godot.HttpClient.Method.Put,
                payload,
                authenticated: true);

            if (!response.IsSuccess)
            {
                world.SetSaveStatus(ReadError(response), true);
                return false;
            }

            var character = JsonSerializer.Deserialize<CharacterResponse>(response.Body, JsonOptions);
            if (character is null)
            {
                world.SetSaveStatus("The server returned an unreadable save response.", true);
                return false;
            }

            RememberCharacter(character);
            ShowCharacter(character);
            var creaturesSaved = await SaveCreaturePositionsCoreAsync(world);
            world.SetSaveStatus(
                creaturesSaved
                    ? "Player and creature positions saved to the Living Realms server."
                    : "Player position saved; creature movement will retry on the next autosave.",
                !creaturesSaved);
            return true;
        }
        finally
        {
            _world?.SetSaving(false);
            _worldApiGate.Release();
        }
    }

    private async Task<bool> SaveCreaturePositionsCoreAsync(StonehavenValley world)
    {
        var positions = world.CreaturePositions;
        if (positions.Count == 0)
        {
            return true;
        }

        foreach (var batch in positions.Chunk(32))
        {
            var payload = JsonSerializer.Serialize(new CreaturePositionsRequest(
                batch.Select(position => new CreaturePositionRequest(
                    position.Id,
                    position.Position.X,
                    position.Position.Y,
                    position.Position.Z)).ToArray()));
            var response = await SendAsync(
                "/api/v1/regions/stonehaven-valley/creatures/positions",
                Godot.HttpClient.Method.Put,
                payload,
                authenticated: true);
            if (!response.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> LoadWorldCreaturesAsync(StonehavenValley? expectedWorld = null)
    {
        var world = expectedWorld ?? _world;
        if (world is null || !ReferenceEquals(_world, world))
        {
            return false;
        }

        await _worldApiGate.WaitAsync();
        try
        {
            return ReferenceEquals(_world, world) &&
                   await LoadWorldCreaturesCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task<bool> LoadWorldCreaturesCoreAsync(
        StonehavenValley world,
        bool synchronizePositions = false,
        bool requireFullyRestored = false)
    {
        var response = await SendAsync(
            "/api/v1/regions/stonehaven-valley/creatures",
            Godot.HttpClient.Method.Get,
            string.Empty,
            authenticated: true);
        if (!response.IsSuccess)
        {
            world.SetCombatStatus(ReadError(response), true);
            return false;
        }

        var creatures = JsonSerializer.Deserialize<CreatureResponse[]>(response.Body, JsonOptions);
        if (creatures is null || creatures.Length == 0)
        {
            world.SetCombatStatus(
                creatures is null
                    ? "The server returned an unreadable creature roster."
                    : "The server returned an empty creature roster.",
                true);
            return false;
        }

        world.LoadCreatures(
            creatures.Select(ToWorldCreature),
            removeMissing: true,
            synchronizePositions: synchronizePositions);
        if (requireFullyRestored && creatures.Any(creature =>
                !creature.Status.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
                creature.Health != creature.MaximumHealth ||
                PositionDistance(creature.Position, creature.SpawnPosition) > 0.1f))
        {
            world.SetCombatStatus(
                "The world reset completed, but the server did not fully restore every creature.",
                true);
            return false;
        }

        world.SetCombatStatus(
            $"Loaded {creatures.Length} persistent creatures, including the A1 training yard and Darkwood camp.",
            false);
        return true;
    }

    private async Task LoadWorldResidentsAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }

        await _worldApiGate.WaitAsync();
        try
        {
            await LoadWorldResidentsCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task<bool> LoadWorldResidentsCoreAsync(
        StonehavenValley world,
        bool requireFullyRestored = false)
    {
        var response = await SendAsync(
            "/api/v1/regions/stonehaven-valley/residents",
            Godot.HttpClient.Method.Get,
            string.Empty,
            authenticated: true);
        if (!response.IsSuccess)
        {
            world.SetCombatStatus(ReadError(response), true);
            return false;
        }

        var residents = JsonSerializer.Deserialize<ResidentResponse[]>(response.Body, JsonOptions);
        if (residents is null)
        {
            world.SetCombatStatus("The server returned an unreadable Stonehaven resident roster.", true);
            return false;
        }

        world.LoadResidents(
            residents.Select(ToWorldResident),
            synchronizePositions: requireFullyRestored);
        var restoredLivingResidents = residents.Count(resident =>
            resident.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
            resident.Health == resident.MaximumHealth);
        if (requireFullyRestored &&
            (restoredLivingResidents != 8 || residents.Any(resident =>
                (!resident.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                 !resident.Status.Equals("Missing", StringComparison.OrdinalIgnoreCase)) ||
                (resident.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                 resident.Health != resident.MaximumHealth))))
        {
            world.SetCombatStatus(
                "The world reset completed, but Stonehaven did not return to exactly 8 healthy starting residents.",
                true);
            return false;
        }

        return true;
    }

    private async Task ResolveCombatAsync(
        string endpoint,
        Guid creatureId,
        Vector3 playerPosition,
        Vector3 creaturePosition,
        bool playerAttack)
    {
        var world = _world;
        if (world is null || _selectedCharacter is null)
        {
            return;
        }

        await _worldApiGate.WaitAsync();
        try
        {
            playerPosition = SanitizeNetworkPosition(playerPosition);
            creaturePosition = SanitizeNetworkPosition(creaturePosition);
            var payload = JsonSerializer.Serialize(new CombatRequest(
                creatureId,
                new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z),
                new PositionRequest(creaturePosition.X, creaturePosition.Y, creaturePosition.Z)));
            var response = await SendAsync(
                endpoint,
                Godot.HttpClient.Method.Post,
                payload,
                authenticated: true);
            if (!response.IsSuccess)
            {
                if (response.StatusCode == 429)
                {
                    _combatBackoffUntil = DateTimeOffset.UtcNow.AddSeconds(2);
                    world.SetCombatStatus("Combat synchronization is catching up. Attacks will resume automatically.", false);
                }
                else
                {
                    world.SetCombatStatus(ReadError(response), true);
                }
                if (response.StatusCode is 404 or 409)
                {
                    // Another client, a raid resolution, or a world reset may
                    // have changed or removed this target. Refresh immediately
                    // so both player characters stop attacking a stale node.
                    await LoadWorldCreaturesCoreAsync(world);
                }
                return;
            }

            var combat = JsonSerializer.Deserialize<CombatResponse>(response.Body, JsonOptions);
            if (combat is null)
            {
                world.SetCombatStatus("The server returned an unreadable combat response.", true);
                return;
            }

            RememberCharacter(combat.Character);
            ShowCharacter(combat.Character);
            world.ApplyCreatureState(
                ToWorldCreature(combat.Creature),
                flashDamage: playerAttack,
                synchronizePosition: combat.CharacterKnockedOut);
            world.ApplyCharacterState(
                combat.Character.Level,
                combat.Character.Experience,
                combat.Character.Health,
                combat.Character.MaximumHealth,
                new Vector3(
                    combat.Character.Position.X,
                    combat.Character.Position.Y,
                    combat.Character.Position.Z),
                combat.CharacterKnockedOut,
                combat.Message);
            if (combat.Loot.Length > 0)
            {
                await LoadInventoryCoreAsync(world);
            }
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task LoadInventoryAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            await LoadInventoryCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task LoadInventoryCoreAsync(StonehavenValley world)
    {
        var response = await SendAsync("/api/v1/inventory", Godot.HttpClient.Method.Get, string.Empty, authenticated: true);
        if (!response.IsSuccess)
        {
            world.SetCombatStatus(ReadError(response), true);
            return;
        }
        var inventory = JsonSerializer.Deserialize<InventoryResponse>(response.Body, JsonOptions);
        if (inventory is null)
        {
            world.SetCombatStatus("The server returned an unreadable inventory.", true);
            return;
        }
        world.SetInventory(ToWorldInventory(inventory));
    }

    private async Task LoadSkillsAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            var response = await SendAsync("/api/v1/skills", Godot.HttpClient.Method.Get, string.Empty, authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }
            var skills = JsonSerializer.Deserialize<SkillResponse[]>(response.Body, JsonOptions);
            if (skills is null)
            {
                world.SetCombatStatus("The server returned unreadable skills.", true);
                return;
            }
            world.SetSkills(skills.Select(skill => new WorldSkillData(
                skill.Key, skill.Name, skill.Description, skill.Hotkey, skill.CooldownSeconds,
                skill.IsOffensive, skill.Range)));
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task ChangeInventoryAsync(Guid entryId, string action, Vector3 playerPosition)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            var body = action == "sell"
                ? JsonSerializer.Serialize(new SellItemRequest(
                    new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z)))
                : string.Empty;
            var response = await SendAsync(
                $"/api/v1/inventory/{entryId:D}/{action}",
                Godot.HttpClient.Method.Post,
                body,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }

            if (action == "use")
            {
                var used = JsonSerializer.Deserialize<ItemUseResponse>(response.Body, JsonOptions);
                if (used is null)
                {
                    world.SetCombatStatus("The server returned an unreadable item result.", true);
                    return;
                }
                RememberCharacter(used.Character);
                world.ApplyCharacterState(
                    used.Character.Level, used.Character.Experience, used.Character.Health,
                    used.Character.MaximumHealth,
                    new Vector3(used.Character.Position.X, used.Character.Position.Y, used.Character.Position.Z),
                    false,
                    used.Message);
                world.SetInventory(ToWorldInventory(used.Inventory));
            }
            else if (action == "sell")
            {
                var sale = JsonSerializer.Deserialize<ItemSaleResponse>(response.Body, JsonOptions);
                if (sale is null)
                {
                    world.SetCombatStatus("The server returned an unreadable sale result.", true);
                    return;
                }
                world.SetInventory(ToWorldInventory(sale.Inventory));
                world.SetCombatStatus(sale.Message, false);
            }
            else
            {
                var inventory = JsonSerializer.Deserialize<InventoryResponse>(response.Body, JsonOptions);
                if (inventory is null)
                {
                    world.SetCombatStatus("The server returned an unreadable equipment result.", true);
                    return;
                }
                world.SetInventory(ToWorldInventory(inventory));
                world.SetCombatStatus(action == "equip" ? "Equipment updated." : "Item unequipped.", false);
            }
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task UseSkillAsync(
        string skillKey,
        Guid? creatureId,
        Vector3 playerPosition,
        Vector3? creaturePosition)
    {
        var world = _world;
        if (world is null || _selectedCharacter is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            playerPosition = SanitizeNetworkPosition(playerPosition);
            if (creaturePosition is not null)
            {
                creaturePosition = SanitizeNetworkPosition(creaturePosition.Value);
            }
            var creature = creaturePosition is null
                ? null
                : new PositionRequest(creaturePosition.Value.X, creaturePosition.Value.Y, creaturePosition.Value.Z);
            var payload = JsonSerializer.Serialize(new SkillUseRequest(
                skillKey,
                creatureId,
                new PositionRequest(playerPosition.X, playerPosition.Y, playerPosition.Z),
                creature));
            var response = await SendAsync(
                "/api/v1/combat/player-skill",
                Godot.HttpClient.Method.Post,
                payload,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), response.StatusCode != 429);
                return;
            }
            var result = JsonSerializer.Deserialize<SkillUseResponse>(response.Body, JsonOptions);
            if (result is null)
            {
                world.SetCombatStatus("The server returned an unreadable skill result.", true);
                return;
            }
            RememberCharacter(result.Character);
            ShowCharacter(result.Character);
            if (result.Creature is not null)
            {
                world.ApplyCreatureState(ToWorldCreature(result.Creature), flashDamage: true);
            }
            world.ApplyCharacterState(
                result.Character.Level, result.Character.Experience, result.Character.Health,
                result.Character.MaximumHealth,
                new Vector3(result.Character.Position.X, result.Character.Position.Y, result.Character.Position.Z),
                false,
                result.Message);
            if (result.Loot.Length > 0)
            {
                await LoadInventoryCoreAsync(world);
            }
            await LoadSkillsCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task LoadSkillsCoreAsync(StonehavenValley world)
    {
        var response = await SendAsync("/api/v1/skills", Godot.HttpClient.Method.Get, string.Empty, authenticated: true);
        if (!response.IsSuccess)
        {
            return;
        }
        var skills = JsonSerializer.Deserialize<SkillResponse[]>(response.Body, JsonOptions);
        if (skills is not null)
        {
            world.SetSkills(skills.Select(skill => new WorldSkillData(
                skill.Key, skill.Name, skill.Description, skill.Hotkey, skill.CooldownSeconds,
                skill.IsOffensive, skill.Range)));
        }
    }

    private async Task LoadRaidStateAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            await LoadRaidStateCoreAsync(world);
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task<bool> LoadRaidStateCoreAsync(
        StonehavenValley world,
        bool requireNoRaid = false)
    {
        var response = await SendAsync(
            "/api/v1/world/raid",
            Godot.HttpClient.Method.Get,
            string.Empty,
            authenticated: true);
        if (!response.IsSuccess)
        {
            world.SetCombatStatus(ReadError(response), true);
            return false;
        }
        var state = JsonSerializer.Deserialize<RaidStateResponse>(response.Body, JsonOptions);
        if (state is null)
        {
            world.SetCombatStatus("The server returned an unreadable raid state.", true);
            return false;
        }
        world.SetRaidState(ToWorldRaidState(state));
        if (requireNoRaid && state.HasRaid)
        {
            world.SetCombatStatus("The world reset completed, but the raid was not cleared.", true);
            return false;
        }

        return true;
    }

    private async Task ChangeRaidAsync(string endpoint, bool starting, bool counterattack = false)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }

        var changed = false;
        await _worldApiGate.WaitAsync();
        try
        {
            if (starting)
            {
                if (counterattack)
                {
                    world.SetCounterattackStartBusy(true);
                }
                else
                {
                    world.SetRaidStartBusy(true);
                }
            }
            var response = await SendAsync(
                endpoint,
                Godot.HttpClient.Method.Post,
                string.Empty,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }
            var state = JsonSerializer.Deserialize<RaidStateResponse>(response.Body, JsonOptions);
            if (state is null)
            {
                world.SetCombatStatus("The server returned an unreadable raid result.", true);
                return;
            }
            world.SetRaidState(ToWorldRaidState(state));
            if (starting)
            {
                world.SetCombatStatus(
                    counterattack
                        ? "Stonehaven's authorized force is assembling for the march on Darkwood."
                        : "The Darkwood war horn sounded. Fifteen authorized raiders are assembling at the clan camp.",
                    false);
            }
            else if (!state.Active && state.Raid?.OutcomeSummary is not null)
            {
                world.SetCombatStatus(state.Raid.OutcomeSummary, false);
            }
            changed = true;
        }
        finally
        {
            if (starting)
            {
                if (counterattack)
                {
                    world.SetCounterattackStartBusy(false);
                }
                else
                {
                    world.SetRaidStartBusy(false);
                }
            }
            _worldApiGate.Release();
        }

        if (changed)
        {
            await LoadWorldCreaturesAsync();
            await LoadWorldResidentsAsync();
            await LoadWorldStateAsync();
        }
    }

    private async Task LoadWorldStateAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            var response = await SendAsync(
                "/api/v1/world/state",
                Godot.HttpClient.Method.Get,
                string.Empty,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }
            var state = JsonSerializer.Deserialize<WorldStateResponse>(response.Body, JsonOptions);
            if (state is null)
            {
                world.SetCombatStatus("The server returned an unreadable living-world state.", true);
                return;
            }
            world.SetWorldState(ToWorldState(state));
        }
        finally
        {
            _worldApiGate.Release();
        }
    }

    private async Task AdvanceWorldAsync(int hours)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            world.SetWorldAdvanceBusy(true);
            var payload = JsonSerializer.Serialize(new AdvanceWorldRequest(hours));
            var response = await SendAsync(
                "/api/v1/world/advance",
                Godot.HttpClient.Method.Post,
                payload,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }
            var advanced = JsonSerializer.Deserialize<AdvanceWorldResponse>(response.Body, JsonOptions);
            if (advanced?.World is null)
            {
                world.SetCombatStatus("The server returned an unreadable world-advance result.", true);
                return;
            }
            world.SetWorldState(ToWorldState(advanced.World));
            world.SetCombatStatus(
                $"The Living World advanced {hours} hours. Darkwood is now a {advanced.World.Faction.StageName}.",
                false);
            await LoadRaidStateCoreAsync(world);
            await LoadWorldCreaturesCoreAsync(world);
            await LoadWorldResidentsCoreAsync(world);
        }
        finally
        {
            world.SetWorldAdvanceBusy(false);
            _worldApiGate.Release();
        }
    }

    private async Task ResetWorldAsync()
    {
        var world = _world;
        if (world is null)
        {
            return;
        }
        await _worldApiGate.WaitAsync();
        try
        {
            world.SetWorldResetBusy(true);
            var response = await SendAsync(
                "/api/v1/world/reset",
                Godot.HttpClient.Method.Post,
                string.Empty,
                authenticated: true);
            if (!response.IsSuccess)
            {
                world.SetCombatStatus(ReadError(response), true);
                return;
            }
            var reset = JsonSerializer.Deserialize<WorldStateResponse>(response.Body, JsonOptions);
            if (reset is null)
            {
                world.SetCombatStatus("The server returned an unreadable world-reset result.", true);
                return;
            }
            world.SetWorldState(ToWorldState(reset));
            await LoadDevelopmentStateCoreAsync(world);
            var raidCleared = await LoadRaidStateCoreAsync(world, requireNoRaid: true);
            var creaturesRestored = await LoadWorldCreaturesCoreAsync(
                world,
                synchronizePositions: true,
                requireFullyRestored: true);
            var residentsRestored = await LoadWorldResidentsCoreAsync(world, requireFullyRestored: true);
            if (raidCleared && creaturesRestored && residentsRestored)
            {
                world.SetCombatStatus(
                    "World reset complete. Stonehaven returned to 8 residents and Darkwood returned to 7 goblins; player progress was kept.",
                    false);
            }
            else
            {
                world.SetCombatStatus(
                    "The server reset was saved, but the client could not verify every restored world object. Close and reopen the play test, then try once more.",
                    true);
            }
        }
        finally
        {
            world.SetWorldResetBusy(false);
            _worldApiGate.Release();
        }
    }

    private static WorldInventoryData ToWorldInventory(InventoryResponse inventory) => new(
        inventory.Attack,
        inventory.Defense,
        inventory.Gold,
        inventory.UsedCapacity,
        inventory.CarryCapacity,
        inventory.Items.Select(item => new WorldInventoryItem(
            item.Id, item.Key, item.Name, item.Kind, item.Rarity, item.EquipmentSlot,
            item.AttackBonus, item.DefenseBonus, item.HealingAmount,
            item.UnitWeight, item.TotalWeight, item.BuyerName, item.Quantity, item.IsEquipped)).ToArray());

    private static float PositionDistance(PositionResponse first, PositionResponse second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        var z = first.Z - second.Z;
        return MathF.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static WorldCreatureData ToWorldCreature(CreatureResponse creature)
    {
        return new WorldCreatureData(
            creature.Id,
            creature.SpeciesKey,
            creature.SpeciesName,
            creature.Name,
            creature.Title,
            creature.Role,
            creature.Level,
            creature.Health,
            creature.MaximumHealth,
            creature.Attack,
            creature.Defense,
            creature.MovementSpeed,
            creature.DetectionRadius,
            creature.AttackRange,
            creature.Aggression,
            creature.Status,
            new Vector3(creature.Position.X, creature.Position.Y, creature.Position.Z),
            new Vector3(creature.SpawnPosition.X, creature.SpawnPosition.Y, creature.SpawnPosition.Z),
            creature.RespawnAt,
            creature.IsBoss,
            creature.Role?.Equals("Raid Attacker", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static WorldResidentData ToWorldResident(ResidentResponse resident) => new(
        resident.Id,
        resident.Name,
        resident.Role,
        resident.Health,
        resident.MaximumHealth,
        resident.Status,
        resident.CanFight,
        resident.Skills,
        resident.PrimarySkill,
        resident.SkillLevel,
        resident.Trait,
        resident.Experience,
        resident.IsMajor,
        resident.MemorySummary,
        resident.Activity,
        new Vector3(resident.Position.X, resident.Position.Y, resident.Position.Z),
        new Vector3(resident.HomePosition.X, resident.HomePosition.Y, resident.HomePosition.Z),
        new Vector3(resident.WorkPosition.X, resident.WorkPosition.Y, resident.WorkPosition.Z),
        new Vector3(resident.SafePosition.X, resident.SafePosition.Y, resident.SafePosition.Z),
        resident.Dialogue,
        resident.WorldHour,
        resident.WorldDay);

    private static WorldRaidStateData ToWorldRaidState(RaidStateResponse state) => new(
        state.HasRaid,
        state.Active,
        state.CanStartPlaytest,
        state.DarkwoodRaidReady,
        state.StonehavenCounterattackReady,
        state.AdministratorOnline,
        state.CanStartDarkwoodRaid,
        state.CanStartCounterattack,
        state.Raid is null
            ? null
            : new WorldRaidData(
                state.Raid.Id,
                state.Raid.Status,
                state.Raid.WorldDay,
                state.Raid.InitialAttackerStrength,
                state.Raid.AttackerStrength,
                state.Raid.InitialDefenderStrength,
                state.Raid.DefenderStrength,
                state.Raid.PlayerContribution,
                state.Raid.SettlementDamage,
                state.Raid.ResidentCasualties,
                state.Raid.ResidentInjuries,
                state.Raid.OutcomeSummary,
                state.Raid.Attackers.Select(attacker => new WorldRaidAttackerData(
                    attacker.CreatureId,
                    attacker.Name,
                    attacker.Level,
                    attacker.Health,
                    attacker.MaximumHealth,
                    attacker.Status,
                    attacker.IsDefeated,
                    attacker.DefeatedByPlayer)).ToArray()),
        state.Counterattack is null
            ? null
            : new WorldCounterattackData(
                state.Counterattack.Id,
                state.Counterattack.Status,
                state.Counterattack.WorldDay,
                state.Counterattack.InitialSoldierCount,
                state.Counterattack.SoldiersRemaining,
                state.Counterattack.InitialGoblinCount,
                state.Counterattack.GoblinsRemaining,
                state.Counterattack.CampLevelBefore,
                state.Counterattack.CampLevelAfter,
                state.Counterattack.InitialCampStrength,
                state.Counterattack.CampStrength,
                state.Counterattack.StonehavenCasualties,
                state.Counterattack.DarkwoodCasualties,
                state.Counterattack.OutcomeSummary,
                state.Counterattack.Members.Select(member => new WorldCounterattackMemberData(
                    member.ResidentId,
                    member.Name,
                    member.Role,
                    member.Health,
                    member.MaximumHealth,
                    member.Status,
                    member.IsDefeated)).ToArray()));

    private static WorldStateData ToWorldState(WorldStateResponse state) => new(
        state.SimulatedHours,
        state.WorldDay,
        state.SimulationSpeed,
        state.CanAccelerate,
        new WorldFactionData(
            state.Faction.Name,
            state.Faction.Population,
            state.Faction.PopulationCapacity,
            state.Faction.DevelopmentStage,
            state.Faction.StageName,
            state.Faction.TerritorySize,
            state.Faction.Aggression,
            state.Faction.Morale,
            state.Faction.TechnologyLevel,
            state.Faction.MilitaryStrength,
            state.Faction.Resources.Select(resource =>
                new WorldResourceData(resource.Kind, resource.Amount, resource.Capacity)).ToArray(),
            state.Faction.Structures.Select(structure =>
                new WorldStructureData(structure.Name, structure.Level, structure.Health)).ToArray(),
            new WorldLeaderData(
                state.Faction.Leader.Name,
                state.Faction.Leader.Title,
                state.Faction.Leader.Level,
                state.Faction.Leader.Leadership,
                state.Faction.Leader.Health,
                state.Faction.Leader.MaximumHealth,
                state.Faction.Leader.Attack,
                state.Faction.Leader.Defense,
                state.Faction.Leader.Status)),
        new WorldSettlementData(
            state.Settlement.Name,
            state.Settlement.Population,
            state.Settlement.LivingResidents,
            state.Settlement.CombatReadyResidents,
            state.Settlement.HousingCapacity,
            state.Settlement.Food,
            state.Settlement.Wood,
            state.Settlement.Stone,
            state.Settlement.Iron,
            state.Settlement.DefenseRating,
            state.Settlement.GuardStrength,
            new WorldSettlementLeaderData(
                state.Settlement.Leader.Name,
                state.Settlement.Leader.Title,
                state.Settlement.Leader.Role,
                state.Settlement.Leader.Health,
                state.Settlement.Leader.MaximumHealth,
                state.Settlement.Leader.Status,
                state.Settlement.Leader.PrimarySkill,
                state.Settlement.Leader.SkillLevel,
                state.Settlement.Leader.Trait,
                state.Settlement.Leader.IsMajor,
                state.Settlement.Leader.MemorySummary)),
        new WorldEventReadinessData(
            new WorldTriggerReadinessData(
                state.EventReadiness.DarkwoodRaid.Name,
                state.EventReadiness.DarkwoodRaid.Current,
                state.EventReadiness.DarkwoodRaid.Required,
                state.EventReadiness.DarkwoodRaid.Ready,
                state.EventReadiness.DarkwoodRaid.Active,
                state.EventReadiness.DarkwoodRaid.AdministratorOnline,
                state.EventReadiness.DarkwoodRaid.Explanation),
            new WorldTriggerReadinessData(
                state.EventReadiness.StonehavenCounterattack.Name,
                state.EventReadiness.StonehavenCounterattack.Current,
                state.EventReadiness.StonehavenCounterattack.Required,
                state.EventReadiness.StonehavenCounterattack.Ready,
                state.EventReadiness.StonehavenCounterattack.Active,
                state.EventReadiness.StonehavenCounterattack.AdministratorOnline,
                state.EventReadiness.StonehavenCounterattack.Explanation)),
        new WorldEventQueueData(state.Events.Pending, state.Events.Completed, state.Events.Failed),
        state.RecentHistory.Select(entry =>
            new WorldHistoryData(entry.Title, entry.Description, entry.OccurredAtCentral)).ToArray());

    private static DevelopmentStateData ToDevelopmentState(DevelopmentStateResponse state) => new(
        state.Nodes.Select(node => new ResourceNodeData(
            node.Id,
            node.Key,
            node.Name,
            node.Kind,
            node.Owner,
            new Vector3(node.Position.X, node.Position.Y, node.Position.Z),
            node.Remaining,
            node.Capacity,
            node.YieldPerHarvest,
            node.RespawnAt)).ToArray(),
        state.Projects.Select(project => new ConstructionProjectData(
            project.Id,
            project.Key,
            project.Name,
            project.Owner,
            project.WoodRequired,
            project.StoneRequired,
            project.WoodContributed,
            project.StoneContributed,
            project.CurrentLevel,
            project.MaximumLevel,
            project.Progress,
            project.Stage,
            new Vector3(project.Position.X, project.Position.Y, project.Position.Z),
            project.CompletedAt)).ToArray(),
        state.RecentContributions.Select(contribution => new ResourceContributionData(
            contribution.ContributorName,
            contribution.Kind,
            contribution.Amount,
            contribution.Source,
            contribution.OccurredAt)).ToArray(),
        state.SettlementStores.Wood,
        state.SettlementStores.Stone);

    private async void SaveWorldAndQuitAsync()
    {
        if (_world is not null)
        {
            _world.SetSaveStatus("Saving before closing...", false);
            _ = await SaveWorldPositionAsync(_world.PlayerPosition);
        }

        GetTree().Quit();
    }

    private void ExitWorldToCharacterSelection()
    {
        DestroyWorld();
        _background.Visible = true;
        _page.Visible = true;
        _authPanel.Visible = false;
        _realmPanel.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void DestroyWorld()
    {
        if (_world is null)
        {
            return;
        }

        _world.SaveRequested -= OnWorldSaveRequested;
        _world.ReturnRequested -= OnWorldReturnRequested;
        _world.LogoutRequested -= OnWorldLogoutRequested;
        _world.PlayerAttackRequested -= OnWorldPlayerAttackRequested;
        _world.CreatureAttackRequested -= OnWorldCreatureAttackRequested;
        _world.SettlementDefenseAttackRequested -= OnSettlementDefenseAttackRequested;
        _world.CreatureRefreshRequested -= OnWorldCreatureRefreshRequested;
        _world.SkillRequested -= OnWorldSkillRequested;
        _world.InventoryActionRequested -= OnWorldInventoryActionRequested;
        _world.WorldStateRequested -= OnWorldStateRequested;
        _world.DevelopmentStateRequested -= OnDevelopmentStateRequested;
        _world.ResourceHarvestRequested -= OnResourceHarvestRequested;
        _world.NaturalResourceHarvestRequested -= OnNaturalResourceHarvestRequested;
        _world.ProjectContributionRequested -= OnProjectContributionRequested;
        _world.NpcWorkRequested -= OnNpcWorkRequested;
        _world.WorldAdvanceRequested -= OnWorldAdvanceRequested;
        _world.WorldResetRequested -= OnWorldResetRequested;
        _world.RaidStateRequested -= OnRaidStateRequested;
        _world.RaidStartRequested -= OnRaidStartRequested;
        _world.CounterattackStartRequested -= OnCounterattackStartRequested;
        _world.RaidAdvanceRequested -= OnRaidAdvanceRequested;
        _world.QueueFree();
        _world = null;
    }

    private async Task<ApiResponse> SendAsync(
        string path,
        Godot.HttpClient.Method method,
        string body,
        bool authenticated)
    {
        try
        {
            using var request = new NetHttpRequestMessage(
                ToNetHttpMethod(method),
                _apiBaseUrl + path);
            if (authenticated && !string.IsNullOrWhiteSpace(_token))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }
            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new NetStringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await _apiClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            return new ApiResponse((long)response.StatusCode, responseBody);
        }
        catch (TaskCanceledException)
        {
            return new ApiResponse(0, "The Living Realms server request timed out.");
        }
        catch (System.Net.Http.HttpRequestException exception)
        {
            return new ApiResponse(0, $"The Living Realms server could not be reached: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return new ApiResponse(0, $"The Living Realms request was invalid: {exception.Message}");
        }
    }

    private static NetHttpMethod ToNetHttpMethod(Godot.HttpClient.Method method) => method switch
    {
        Godot.HttpClient.Method.Get => NetHttpMethod.Get,
        Godot.HttpClient.Method.Post => NetHttpMethod.Post,
        Godot.HttpClient.Method.Put => NetHttpMethod.Put,
        Godot.HttpClient.Method.Delete => NetHttpMethod.Delete,
        Godot.HttpClient.Method.Patch => NetHttpMethod.Patch,
        Godot.HttpClient.Method.Head => NetHttpMethod.Head,
        _ => throw new InvalidOperationException($"HTTP method {method} is not supported.")
    };

    private static Vector3 SanitizeNetworkPosition(Vector3 position)
    {
        if (!position.IsFinite())
        {
            return Vector3.Zero;
        }

        return new Vector3(
            Mathf.Clamp(position.X, -139.0f, 139.0f),
            Mathf.Clamp(position.Y, -1.0f, 18.0f),
            Mathf.Clamp(position.Z, -139.0f, 139.0f));
    }

    private void ShowCharacter(CharacterResponse character)
    {
        _selectedCharacter = character;
        _characterDetails.Text =
            $"{character.Name} — Level {character.Level} {character.Archetype} — {character.Region} — " +
            $"Health {character.Health}/{character.MaximumHealth} — XP {character.Experience}/{character.Level * 100L}";
        _positionX.Value = character.Position.X;
        _positionY.Value = character.Position.Y;
        _positionZ.Value = character.Position.Z;
        _savePositionButton.Disabled = false;
    }

    private void RememberCharacter(CharacterResponse character)
    {
        if (character.Name == "Alden")
        {
            _alden = character;
        }
        else if (character.Name == "Elara")
        {
            _elara = character;
        }
    }

    private void SetAuthBusy(bool busy, string status)
    {
        _loginButton.Disabled = busy;
        _registerButton.Disabled = busy;
        _email.Editable = !busy;
        _password.Editable = !busy;
        _passwordVisibilityButton.Disabled = busy;
        SetAuthStatus(status, isError: false);
    }

    private void SetAuthStatus(string status, bool isError)
    {
        _authStatus.Text = status;
        _authStatus.AddThemeColorOverride(
            "font_color",
            isError ? new Color("d94c3b") : new Color("d8a94b"));
    }

    private void SetRealmBusy(bool busy, string status)
    {
        _aldenButton.Disabled = busy || _alden is null;
        _elaraButton.Disabled = busy || _elara is null;
        _savePositionButton.Disabled = busy || _selectedCharacter is null;
        _logoutButton.Disabled = busy;
        _realmStatus.Text = status;
    }

    private void ResetToLogin(string status)
    {
        DestroyWorld();
        _token = null;
        _isAdministrator = false;
        _alden = null;
        _elara = null;
        _selectedCharacter = null;
        _background.Visible = true;
        _page.Visible = true;
        _realmPanel.Visible = false;
        _authPanel.Visible = true;
        SetRealmBusy(false, string.Empty);
        SetAuthBusy(false, status);
    }

    private static string ReadError(ApiResponse response)
    {
        if (response.StatusCode == 0)
        {
            return response.Body;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? $"Request failed with status {response.StatusCode}.";
            }
            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? $"Request failed with status {response.StatusCode}.";
            }
            if (root.TryGetProperty("errors", out var errors))
            {
                return string.Join(" ", errors.EnumerateObject()
                    .SelectMany(property => property.Value.EnumerateArray())
                    .Select(value => value.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }
        }
        catch (JsonException)
        {
            // The fallback below is intentionally human-readable for non-JSON proxy errors.
        }

        return response.StatusCode == 401
            ? "Email or password was not accepted."
            : $"Request failed with status {response.StatusCode}.";
    }

    private sealed record CredentialsRequest(string Email, string Password);
    private sealed record PositionRequest(float X, float Y, float Z);
    private sealed record CreaturePositionRequest(Guid Id, float X, float Y, float Z);
    private sealed record CreaturePositionsRequest(IReadOnlyCollection<CreaturePositionRequest> Creatures);
    private sealed record CombatRequest(
        Guid CreatureId,
        PositionRequest PlayerPosition,
        PositionRequest CreaturePosition);
    private sealed record SettlementDefenseAttackRequest(
        Guid ResidentId,
        Guid CreatureId,
        PositionRequest ResidentPosition,
        PositionRequest CreaturePosition);
    private sealed record SkillUseRequest(
        string SkillKey,
        Guid? CreatureId,
        PositionRequest PlayerPosition,
        PositionRequest? CreaturePosition);
    private sealed record AdvanceWorldRequest(int Hours);
    private sealed record HarvestRequest(Guid NodeId, PositionRequest PlayerPosition);
    private sealed record NaturalHarvestRequest(
        string Kind, PositionRequest ResourcePosition, PositionRequest PlayerPosition);
    private sealed record ContributeRequest(Guid ProjectId, PositionRequest PlayerPosition);
    private sealed record SellItemRequest(PositionRequest PlayerPosition);
    private sealed record NpcWorkRequest(string WorkerKey, Guid NodeId);
    private sealed record ApiResponse(long StatusCode, string Body)
    {
        public bool IsSuccess => StatusCode is >= 200 and < 300;
    }

    private sealed record ClientUpdateManifest(
        string Version,
        string MinimumVersion,
        string DownloadUrl,
        string Sha256,
        long SizeBytes,
        string PublishedAt,
        string[] Notes);

    private sealed class AuthenticationResponse
    {
        public string Token { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; init; }
        public AccountResponse Account { get; init; } = new();
        public CharacterResponse[] Characters { get; init; } = [];
    }

    private sealed class AccountResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public bool IsAdministrator { get; init; }
    }

    private sealed class CharacterResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Archetype { get; init; } = string.Empty;
        public int Level { get; init; }
        public long Experience { get; init; }
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public string Region { get; init; } = string.Empty;
        public PositionResponse Position { get; init; } = new();
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private sealed class PositionResponse
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
    }

    private sealed class CreatureResponse
    {
        public Guid Id { get; init; }
        public string SpeciesKey { get; init; } = string.Empty;
        public string SpeciesName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Title { get; init; }
        public string? Role { get; init; }
        public int Level { get; init; }
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public int Attack { get; init; }
        public int Defense { get; init; }
        public float MovementSpeed { get; init; }
        public float DetectionRadius { get; init; }
        public float AttackRange { get; init; }
        public int Aggression { get; init; }
        public string Status { get; init; } = string.Empty;
        public PositionResponse Position { get; init; } = new();
        public PositionResponse SpawnPosition { get; init; } = new();
        public DateTimeOffset? RespawnAt { get; init; }
        public bool IsBoss { get; init; }
    }

    private sealed class ResidentResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool CanFight { get; init; }
        public string[] Skills { get; init; } = [];
        public string PrimarySkill { get; init; } = string.Empty;
        public int SkillLevel { get; init; }
        public string Trait { get; init; } = string.Empty;
        public long Experience { get; init; }
        public bool IsMajor { get; init; }
        public string MemorySummary { get; init; } = string.Empty;
        public string Activity { get; init; } = string.Empty;
        public PositionResponse Position { get; init; } = new();
        public PositionResponse HomePosition { get; init; } = new();
        public PositionResponse WorkPosition { get; init; } = new();
        public PositionResponse SafePosition { get; init; } = new();
        public string Dialogue { get; init; } = string.Empty;
        public int WorldHour { get; init; }
        public int WorldDay { get; init; }
        public DateTimeOffset ServerTimeCentral { get; init; }
    }

    private sealed class RaidStateResponse
    {
        public bool HasRaid { get; init; }
        public bool Active { get; init; }
        public bool CanStartPlaytest { get; init; }
        public bool DarkwoodRaidReady { get; init; }
        public bool StonehavenCounterattackReady { get; init; }
        public bool AdministratorOnline { get; init; }
        public bool CanStartDarkwoodRaid { get; init; }
        public bool CanStartCounterattack { get; init; }
        public RaidResponse? Raid { get; init; }
        public StonehavenCounterattackResponse? Counterattack { get; init; }
        public DateTimeOffset ServerTimeCentral { get; init; }
    }

    private sealed class RaidResponse
    {
        public Guid Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public int WorldDay { get; init; }
        public int InitialAttackerStrength { get; init; }
        public int AttackerStrength { get; init; }
        public int InitialDefenderStrength { get; init; }
        public int DefenderStrength { get; init; }
        public int PlayerContribution { get; init; }
        public int SettlementDamage { get; init; }
        public int ResidentCasualties { get; init; }
        public int ResidentInjuries { get; init; }
        public string? OutcomeSummary { get; init; }
        public RaidAttackerResponse[] Attackers { get; init; } = [];
    }

    private sealed class RaidAttackerResponse
    {
        public Guid CreatureId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Level { get; init; }
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool IsDefeated { get; init; }
        public bool DefeatedByPlayer { get; init; }
    }

    private sealed class StonehavenCounterattackResponse
    {
        public Guid Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public int WorldDay { get; init; }
        public int InitialSoldierCount { get; init; }
        public int SoldiersRemaining { get; init; }
        public int InitialGoblinCount { get; init; }
        public int GoblinsRemaining { get; init; }
        public int CampLevelBefore { get; init; }
        public int CampLevelAfter { get; init; }
        public int InitialCampStrength { get; init; }
        public int CampStrength { get; init; }
        public int StonehavenCasualties { get; init; }
        public int DarkwoodCasualties { get; init; }
        public string? OutcomeSummary { get; init; }
        public StonehavenCounterattackMemberResponse[] Members { get; init; } = [];
    }

    private sealed class StonehavenCounterattackMemberResponse
    {
        public Guid ResidentId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool IsDefeated { get; init; }
    }

    private sealed class CombatResponse
    {
        public CharacterResponse Character { get; init; } = new();
        public CreatureResponse Creature { get; init; } = new();
        public int Damage { get; init; }
        public int ExperienceGained { get; init; }
        public bool LeveledUp { get; init; }
        public bool CreatureDefeated { get; init; }
        public bool CharacterKnockedOut { get; init; }
        public LootResponse[] Loot { get; init; } = [];
        public string Message { get; init; } = string.Empty;
    }

    private sealed class SettlementDefenseResponse
    {
        public CreatureResponse Creature { get; init; } = new();
        public int Damage { get; init; }
        public bool CreatureDefeated { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    private sealed class InventoryResponse
    {
        public Guid CharacterId { get; init; }
        public int Attack { get; init; }
        public int Defense { get; init; }
        public int Gold { get; init; }
        public int UsedCapacity { get; init; }
        public int CarryCapacity { get; init; }
        public InventoryItemResponse[] Items { get; init; } = [];
    }

    private sealed class InventoryItemResponse
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Rarity { get; init; } = string.Empty;
        public string? EquipmentSlot { get; init; }
        public int AttackBonus { get; init; }
        public int DefenseBonus { get; init; }
        public int HealingAmount { get; init; }
        public int UnitWeight { get; init; }
        public int TotalWeight { get; init; }
        public string BuyerName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public bool IsEquipped { get; init; }
    }

    private sealed class SkillResponse
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Hotkey { get; init; } = string.Empty;
        public double CooldownSeconds { get; init; }
        public bool IsOffensive { get; init; }
        public float Range { get; init; }
    }

    private sealed class LootResponse
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Quantity { get; init; }
    }

    private sealed class ItemUseResponse
    {
        public CharacterResponse Character { get; init; } = new();
        public InventoryResponse Inventory { get; init; } = new();
        public string Message { get; init; } = string.Empty;
    }

    private sealed class ItemSaleResponse
    {
        public InventoryResponse Inventory { get; init; } = new();
        public int GoldReceived { get; init; }
        public string BuyerName { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    private sealed class SkillUseResponse
    {
        public CharacterResponse Character { get; init; } = new();
        public CreatureResponse? Creature { get; init; }
        public string SkillKey { get; init; } = string.Empty;
        public int Damage { get; init; }
        public int Healed { get; init; }
        public bool CreatureDefeated { get; init; }
        public LootResponse[] Loot { get; init; } = [];
        public string Message { get; init; } = string.Empty;
    }

    private sealed class AdvanceWorldResponse
    {
        public WorldStateResponse World { get; init; } = new();
    }

    private sealed class DevelopmentActionResponse
    {
        public DevelopmentStateResponse State { get; init; } = new();
        public string Message { get; init; } = string.Empty;
    }

    private sealed class DevelopmentStateResponse
    {
        public ResourceNodeResponse[] Nodes { get; init; } = [];
        public ConstructionProjectResponse[] Projects { get; init; } = [];
        public ContributionResponse[] RecentContributions { get; init; } = [];
        public SettlementStoresResponse SettlementStores { get; init; } = new();
    }

    private sealed class ResourceNodeResponse
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Owner { get; init; } = string.Empty;
        public PositionResponse Position { get; init; } = new();
        public int Remaining { get; init; }
        public int Capacity { get; init; }
        public int YieldPerHarvest { get; init; }
        public DateTimeOffset? RespawnAt { get; init; }
    }

    private sealed class ConstructionProjectResponse
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Owner { get; init; } = string.Empty;
        public int WoodRequired { get; init; }
        public int StoneRequired { get; init; }
        public int WoodContributed { get; init; }
        public int StoneContributed { get; init; }
        public int CurrentLevel { get; init; }
        public int MaximumLevel { get; init; }
        public float Progress { get; init; }
        public string Stage { get; init; } = string.Empty;
        public PositionResponse Position { get; init; } = new();
        public DateTimeOffset? CompletedAt { get; init; }
    }

    private sealed class SettlementStoresResponse
    {
        public int Wood { get; init; }
        public int Stone { get; init; }
    }

    private sealed class ContributionResponse
    {
        public string ContributorName { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public int Amount { get; init; }
        public string Source { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt { get; init; }
    }

    private sealed class WorldStateResponse
    {
        public long SimulatedHours { get; init; }
        public int WorldDay { get; init; }
        public string SimulationSpeed { get; init; } = string.Empty;
        public bool CanAccelerate { get; init; }
        public WorldFactionResponse Faction { get; init; } = new();
        public WorldSettlementResponse Settlement { get; init; } = new();
        public WorldEventReadinessResponse EventReadiness { get; init; } = new();
        public WorldEventQueueResponse Events { get; init; } = new();
        public WorldHistoryResponse[] RecentHistory { get; init; } = [];
    }

    private sealed class WorldFactionResponse
    {
        public string Name { get; init; } = string.Empty;
        public int Population { get; init; }
        public int PopulationCapacity { get; init; }
        public int DevelopmentStage { get; init; }
        public string StageName { get; init; } = string.Empty;
        public int TerritorySize { get; init; }
        public int Aggression { get; init; }
        public int Morale { get; init; }
        public int TechnologyLevel { get; init; }
        public int MilitaryStrength { get; init; }
        public WorldResourceResponse[] Resources { get; init; } = [];
        public WorldStructureResponse[] Structures { get; init; } = [];
        public WorldLeaderResponse Leader { get; init; } = new();
    }

    private sealed class WorldResourceResponse
    {
        public string Kind { get; init; } = string.Empty;
        public long Amount { get; init; }
        public long Capacity { get; init; }
    }

    private sealed class WorldStructureResponse
    {
        public string Name { get; init; } = string.Empty;
        public int Level { get; init; }
        public int Health { get; init; }
    }

    private sealed class WorldLeaderResponse
    {
        public string Name { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int Level { get; init; }
        public int Leadership { get; init; }
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public int Attack { get; init; }
        public int Defense { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private sealed class WorldSettlementResponse
    {
        public string Name { get; init; } = string.Empty;
        public int Population { get; init; }
        public int LivingResidents { get; init; }
        public int CombatReadyResidents { get; init; }
        public int HousingCapacity { get; init; }
        public int Food { get; init; }
        public int Wood { get; init; }
        public int Stone { get; init; }
        public int Iron { get; init; }
        public int DefenseRating { get; init; }
        public int GuardStrength { get; init; }
        public WorldSettlementLeaderResponse Leader { get; init; } = new();
    }

    private sealed class WorldSettlementLeaderResponse
    {
        public string Name { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public int Health { get; init; }
        public int MaximumHealth { get; init; }
        public string Status { get; init; } = string.Empty;
        public string PrimarySkill { get; init; } = string.Empty;
        public int SkillLevel { get; init; }
        public string Trait { get; init; } = string.Empty;
        public bool IsMajor { get; init; }
        public string MemorySummary { get; init; } = string.Empty;
    }

    private sealed class WorldEventReadinessResponse
    {
        public WorldTriggerReadinessResponse DarkwoodRaid { get; init; } = new();
        public WorldTriggerReadinessResponse StonehavenCounterattack { get; init; } = new();
    }

    private sealed class WorldTriggerReadinessResponse
    {
        public string Name { get; init; } = string.Empty;
        public int Current { get; init; }
        public int Required { get; init; }
        public bool Ready { get; init; }
        public bool Active { get; init; }
        public bool AdministratorOnline { get; init; }
        public string Explanation { get; init; } = string.Empty;
    }

    private sealed class WorldEventQueueResponse
    {
        public int Pending { get; init; }
        public int Completed { get; init; }
        public int Failed { get; init; }
    }

    private sealed class WorldHistoryResponse
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public DateTimeOffset OccurredAtCentral { get; init; }
    }
}
