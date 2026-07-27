<?php
declare(strict_types=1);
require dirname(__DIR__) . '/includes/bootstrap.php';
require dirname(__DIR__) . '/includes/promo.php';
record_visit();

$errors = [];
$email = '';
$returnTo = safe_local_return_path(
    isset($_POST['return_to']) ? (string)$_POST['return_to'] : (string)($_GET['return_to'] ?? ''),
    '');
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $action = (string)($_POST['access_action'] ?? '');
    if (!hash_equals(csrf_token(), (string)($_POST['csrf'] ?? ''))) {
        $errors[] = 'Your form session expired. Please try again.';
    } elseif ($action === 'logout') {
        $currentEmail = (string)(player_access()['email'] ?? '');
        clear_player_access();
        record_player_access_event('logout', true, $currentEmail);
        header('Location: /playtest/?access=signed-out#player-access', true, 303);
        exit;
    } elseif (!in_array($action, ['login', 'register'], true)) {
        $errors[] = 'Choose Portal Login or Create Player Account.';
    } else {
        $email = strtolower(trim((string)($_POST['player_email'] ?? '')));
        $password = (string)($_POST['player_password'] ?? '');
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) $errors[] = 'Enter a valid email address.';
        if ($password === '') $errors[] = 'Enter your password.';
        if (!$errors) {
            try {
                $endpoint = $action === 'register' ? '/api/v1/accounts/register' : '/api/v1/auth/login';
                $response = game_api_request('POST', $endpoint, ['email' => $email, 'password' => $password]);
                $status = (int)$response['status'];
                if (in_array($status, [200, 201], true) && set_player_access($response['data'])) {
                    record_player_access_event($action, true, $email);
                    $destination = $returnTo !== ''
                        ? $returnTo
                        : '/playtest/?access=' . ($action === 'register' ? 'created' : 'granted') . '#player-access';
                    header('Location: ' . $destination, true, 303);
                    exit;
                }

                record_player_access_event($action, false, $email);
                $apiData = $response['data'];
                if ($status === 401 && hash_equals(strtolower((string)($GLOBALS['config']['admin_email'] ?? '')), $email)) {
                    $errors[] = 'That is your administrator portal email, not your player account email. Use the player email registered for the game.';
                }
                elseif ($status === 401) $errors[] = 'The player email or password was not recognized.';
                elseif ($status === 409) $errors[] = (string)($apiData['error'] ?? 'That email already has a player account. Use Portal Login instead.');
                elseif ($status === 429) $errors[] = 'Too many attempts. Please wait a few minutes and try again.';
                elseif ($status === 400 && isset($apiData['errors']) && is_array($apiData['errors'])) {
                    foreach ($apiData['errors'] as $messages) {
                        foreach ((array)$messages as $message) $errors[] = (string)$message;
                    }
                } elseif ($status >= 500 || $status === 0) $errors[] = 'The player account service is temporarily unavailable. Please try again shortly.';
                else $errors[] = (string)($apiData['error'] ?? $apiData['detail'] ?? 'Account access could not be completed.');
            } catch (Throwable $exception) {
                record_player_access_event($action, false, $email);
                error_log('Living Realms website player access failed: ' . $exception->getMessage());
                $errors[] = 'The player account service is temporarily unavailable. Please try again shortly.';
            }
        }
    }
}

$playerAccess = player_access();
$downloadPath = dirname(__DIR__) . '/downloads/LivingRealms-Playtest-Windows-0.9.12.zip';
$downloadReady = is_file($downloadPath);
$downloadSize = $downloadReady ? number_format((float)filesize($downloadPath) / 1048576, 1) . ' MB' : null;
promo_start(
    'Windows Playtest',
    'Create a player account and download the current Living Realms shared-world test build for 64-bit Windows.',
    'Enter the Realm',
    'Create your own player account, choose Alden or Elara, and help shape the same persistent Stonehaven world.');
?>
<section class="section"><div class="section-inner signup-layout">
<div>
<p class="eyebrow">Build 0.9.12</p><h2>A shared world is waiting.</h2>
<p class="lead">Construction, inventory, gathering, combat, and character progress are saved on the live Living Realms server.</p>
<p>Multiple testers can use separate accounts and affect the same world. This build does not yet display other players in real time; live player presence and synchronized multiplayer combat are a later networking step.</p>
<h3>Installation</h3><ol><li>Create a free player account or sign in on this page.</li><li>Download the Windows ZIP.</li><li>Extract the entire ZIP to a writable folder.</li><li>Keep all extracted files together and run <strong>LivingRealms.exe</strong>.</li><li>Sign in to the game with the same email and password.</li></ol>
<h3>What changed in 0.9.12</h3><p>Stonehaven and Darkwood now have 34 persistent destructible assets with real health and armor. Battles damage walls, gates, buildings, farms, mines, docks, and camp structures; breached walls open a usable passage; and the Journey page reports each settlement's damaged or destroyed assets.</p>
<h3>Automatic updates</h3><p>Build 0.9.12 checks for future builds when it starts. New packages are downloaded from living-realms.com, checksum-verified, installed, and relaunched automatically.</p>
<h3>Sharing screenshots</h3><p>Press <strong>F12</strong> while playing to save a PNG in <strong>Pictures\Living Realms</strong>. Press <strong>F10</strong> to release or recapture the mouse without pausing the realm.</p>
<p class="form-note">Early playtest builds are not yet code-signed, so Windows may display an unrecognized-app warning. Only use the package from living-realms.com.</p>
</div>
<div class="signup-card" id="player-access"><p class="eyebrow">Player Access</p><h2>Living Realms Playtest</h2>
<?php if ($playerAccess !== null): ?>
<?php if (($_GET['access'] ?? '') === 'created'): ?><div class="success" role="status"><strong>Your player account is ready.</strong><br>Use these same credentials inside the game.</div><?php elseif (($_GET['access'] ?? '') === 'granted'): ?><div class="success" role="status">Welcome back to the realm.</div><?php endif; ?>
<p>Signed in as <strong><?= e((string)$playerAccess['email']) ?></strong></p>
<?php if ($downloadReady): ?><p>Portable ZIP &bull; <?= e((string)$downloadSize) ?></p><a class="button" href="/playtest-download.php" download>Download the Playtest</a><a class="button secondary" href="/feedback.php">Report a Bug or Request a Feature</a><p class="form-note">Your account already includes Alden and Elara. Delete any earlier incomplete ZIP before downloading this versioned package.</p><?php else: ?><p>The next Windows package is being prepared. Please check again shortly.</p><span class="button secondary" aria-disabled="true">Packaging Build</span><a class="button secondary" href="/feedback.php">Report a Bug or Request a Feature</a><?php endif; ?>
<form method="post" action="/playtest/#player-access" class="compact-form"><input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><button class="button secondary" type="submit" name="access_action" value="logout">Sign Out</button></form>
<?php else: ?>
<?php if (($_GET['access'] ?? '') === 'required'): ?><div class="error" role="alert">Create a player account or sign in to continue.</div><?php elseif (($_GET['access'] ?? '') === 'signed-out'): ?><div class="success" role="status">You have been signed out.</div><?php endif; ?>
<?php if ($errors): ?><div class="error" role="alert"><strong>Account access was not completed:</strong><ul><?php foreach ($errors as $error): ?><li><?= e($error) ?></li><?php endforeach; ?></ul></div><?php endif; ?>
<p>Create one account for both the download and the game. Registration automatically gives you Alden and Elara.</p>
<form method="post" action="/playtest/#player-access"><input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><?php if ($returnTo !== ''): ?><input type="hidden" name="return_to" value="<?= e($returnTo) ?>"><?php endif; ?>
<label for="player-email">Player email address</label><input id="player-email" name="player_email" type="email" maxlength="320" autocomplete="section-player username" value="<?= e($email) ?>" required>
<label for="player-password">Player password</label><div class="password-field"><input id="player-password" name="player_password" type="password" minlength="12" maxlength="128" autocomplete="section-player current-password" required><button class="password-toggle" type="button" data-password-toggle="player-password" aria-label="Show password" aria-pressed="false">Show</button></div>
<p class="form-note">New passwords need 12–128 characters with uppercase, lowercase, a number, and a symbol.</p>
<div class="account-action-row"><button class="button secondary" type="submit" name="access_action" value="login">Player Login</button><button class="button" type="submit" name="access_action" value="register">Create Player Account</button></div>
</form><script src="/assets/player-access.js" defer></script>
<?php endif; ?>
</div></div></section>
<?php promo_end(); ?>
