<?php
declare(strict_types=1);
require __DIR__ . '/includes/bootstrap.php';
record_visit();

$access = player_access();
if ($access === null) {
    $returnTo = '/feedback.php';
    $query = [];
    foreach (['source', 'build'] as $key) {
        if (isset($_GET[$key]) && is_string($_GET[$key])) $query[$key] = substr($_GET[$key], 0, 40);
    }
    if ($query) $returnTo .= '?' . http_build_query($query);
    header('Location: /playtest/?access=required&return_to=' . rawurlencode($returnTo) . '#player-access', true, 303);
    exit;
}

$email = strtolower((string)$access['email']);
$reportTypes = ['bug' => 'Bug Report', 'feature' => 'Feature Request'];
$areas = [
    'gameplay' => 'Gameplay', 'combat' => 'Combat', 'world' => 'World / Map',
    'npc' => 'NPCs / Creatures', 'interface' => 'Interface / Controls',
    'performance' => 'Performance / Lag', 'account' => 'Account / Login',
    'download' => 'Download / Updates', 'website' => 'Website', 'other' => 'Other',
];
$impacts = [
    'blocked' => 'I cannot continue playing', 'major' => 'It seriously affects play',
    'minor' => 'It is a smaller issue', 'idea' => 'Suggestion or improvement',
];
$source = preg_match('/^[a-z0-9_-]{1,30}$/i', (string)($_GET['source'] ?? 'website'))
    ? strtolower((string)($_GET['source'] ?? 'website')) : 'website';
$buildVersion = preg_match('/^[a-z0-9._-]{1,30}$/i', (string)($_GET['build'] ?? ''))
    ? (string)($_GET['build'] ?? '') : '';
$values = [
    'report_type' => 'bug', 'title' => '', 'area' => 'gameplay', 'impact' => 'major',
    'details' => '', 'steps_to_reproduce' => '', 'expected_result' => '',
    'actual_result' => '', 'world_location' => '', 'build_version' => $buildVersion,
];
$errors = [];

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    foreach (array_keys($values) as $key) $values[$key] = trim((string)($_POST[$key] ?? ''));
    $source = preg_match('/^[a-z0-9_-]{1,30}$/i', (string)($_POST['source'] ?? 'website'))
        ? strtolower((string)$_POST['source']) : 'website';
    if (!hash_equals(csrf_token(), (string)($_POST['csrf'] ?? ''))) $errors[] = 'Your form session expired. Please try again.';
    if ((string)($_POST['website'] ?? '') !== '') $errors[] = 'The report could not be submitted.';
    if (!isset($reportTypes[$values['report_type']])) $errors[] = 'Choose Bug Report or Feature Request.';
    if (!isset($areas[$values['area']])) $errors[] = 'Choose the part of Living Realms this concerns.';
    if (!isset($impacts[$values['impact']])) $errors[] = 'Choose how much this affects you.';
    if (mb_strlen($values['title']) < 5 || mb_strlen($values['title']) > 120) $errors[] = 'Use a title between 5 and 120 characters.';
    if (mb_strlen($values['details']) < 20 || mb_strlen($values['details']) > 5000) $errors[] = 'Give at least 20 characters of detail, up to 5,000.';
    foreach (['steps_to_reproduce', 'expected_result', 'actual_result'] as $key) {
        if (mb_strlen($values[$key]) > 3000) $errors[] = 'Each supporting detail must be 3,000 characters or fewer.';
    }
    if (mb_strlen($values['world_location']) > 120) $errors[] = 'The location must be 120 characters or fewer.';
    if ($values['build_version'] !== '' && !preg_match('/^[a-z0-9._ -]{1,30}$/i', $values['build_version'])) $errors[] = 'Enter a valid build number.';

    if (!$errors) {
        $cutoff = central_now()->modify('-1 hour')->format('Y-m-d H:i:s');
        $rate = db()->prepare('SELECT COUNT(*) FROM player_feedback WHERE email = ? AND created_at >= ?');
        $rate->execute([$email, $cutoff]);
        if ((int)$rate->fetchColumn() >= 10) $errors[] = 'You have submitted several reports recently. Please wait before sending another.';
    }

    $storedName = null;
    $originalName = null;
    $screenshotMime = null;
    $screenshotSize = null;
    $upload = $_FILES['screenshot'] ?? null;
    if (!$errors && is_array($upload) && (int)($upload['error'] ?? UPLOAD_ERR_NO_FILE) !== UPLOAD_ERR_NO_FILE) {
        $uploadError = (int)($upload['error'] ?? UPLOAD_ERR_NO_FILE);
        $screenshotSize = (int)($upload['size'] ?? 0);
        if ($uploadError !== UPLOAD_ERR_OK) $errors[] = 'The screenshot could not be uploaded. Please try a smaller image.';
        elseif ($screenshotSize < 1 || $screenshotSize > 5 * 1024 * 1024) $errors[] = 'Screenshots must be 5 MB or smaller.';
        else {
            $temporaryPath = (string)($upload['tmp_name'] ?? '');
            $finfo = new finfo(FILEINFO_MIME_TYPE);
            $screenshotMime = (string)$finfo->file($temporaryPath);
            $extensions = ['image/png' => 'png', 'image/jpeg' => 'jpg', 'image/webp' => 'webp'];
            if (!isset($extensions[$screenshotMime])) $errors[] = 'Screenshots must be PNG, JPG, or WebP images.';
            else {
                $directory = feedback_upload_directory();
                if (!is_dir($directory) && !mkdir($directory, 0750, true) && !is_dir($directory)) $errors[] = 'Screenshot storage is temporarily unavailable.';
                if (!$errors) {
                    $storedName = bin2hex(random_bytes(20)) . '.' . $extensions[$screenshotMime];
                    $originalName = substr(basename((string)($upload['name'] ?? 'screenshot.' . $extensions[$screenshotMime])), 0, 180);
                    if (!move_uploaded_file($temporaryPath, $directory . DIRECTORY_SEPARATOR . $storedName)) {
                        $storedName = null;
                        $errors[] = 'The screenshot could not be saved.';
                    }
                }
            }
        }
    }

    if (!$errors) {
        try {
            $now = central_now()->format('Y-m-d H:i:s');
            $stmt = db()->prepare('INSERT INTO player_feedback
                (created_at, updated_at, email, report_type, title, area, impact, details,
                 steps_to_reproduce, expected_result, actual_result, world_location,
                 build_version, source, screenshot_original_name, screenshot_storage_name,
                 screenshot_mime, screenshot_size, ip_address, user_agent)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)');
            $stmt->execute([
                $now, $now, $email, $values['report_type'], $values['title'], $values['area'],
                $values['impact'], $values['details'], $values['steps_to_reproduce'] ?: null,
                $values['expected_result'] ?: null, $values['actual_result'] ?: null,
                $values['world_location'] ?: null, $values['build_version'] ?: null, $source,
                $originalName, $storedName, $screenshotMime, $screenshotSize, client_ip(),
                substr($_SERVER['HTTP_USER_AGENT'] ?? '', 0, 500),
            ]);
            record_player_access_event('feedback_submitted', true, $email);
            header('Location: /feedback.php?submitted=1#my-reports', true, 303);
            exit;
        } catch (Throwable $exception) {
            if ($storedName !== null) @unlink(feedback_upload_directory() . DIRECTORY_SEPARATOR . $storedName);
            error_log('Living Realms feedback submission failed: ' . $exception->getMessage());
            $errors[] = 'Your report could not be saved. Please try again shortly.';
        }
    }
}

$reportsStmt = db()->prepare('SELECT id, created_at, updated_at, report_type, title, area, impact, status, player_response, screenshot_original_name FROM player_feedback WHERE email = ? ORDER BY id DESC LIMIT 50');
$reportsStmt->execute([$email]);
$reports = $reportsStmt->fetchAll(PDO::FETCH_ASSOC);
$pageTitle = 'Player Feedback | Living Realms';
$pageDescription = 'Report a Living Realms playtest bug or request a new feature.';
require __DIR__ . '/includes/header.php';
?>
<main class="content-main feedback-main">
<div class="feedback-heading"><p class="eyebrow">Shape the Realm</p><h1>Bug Reports &amp; Feature Requests</h1><p class="lead">Tell us what broke, what felt wrong, or what would make Living Realms better. Every submission is tied to your player account so you can follow its status.</p><p>Signed in as <strong><?= e($email) ?></strong></p></div>
<div class="feedback-layout">
<section class="feedback-card" aria-labelledby="new-feedback-heading"><h2 id="new-feedback-heading">Send Feedback</h2>
<?php if (($_GET['submitted'] ?? '') === '1'): ?><div class="success" role="status"><strong>Your feedback was received.</strong><br>It now appears in your report history below.</div><?php endif; ?>
<?php if ($errors): ?><div class="error" role="alert"><strong>Your feedback was not submitted:</strong><ul><?php foreach ($errors as $error): ?><li><?= e($error) ?></li><?php endforeach; ?></ul></div><?php endif; ?>
<form method="post" enctype="multipart/form-data">
<input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><input type="hidden" name="source" value="<?= e($source) ?>">
<div class="honeypot" aria-hidden="true"><label for="website">Website</label><input id="website" name="website" type="text" tabindex="-1" autocomplete="off"></div>
<div class="feedback-fields two-column">
<div><label for="report-type">What are you sending?</label><select id="report-type" name="report_type" required><?php foreach ($reportTypes as $key => $label): ?><option value="<?= e($key) ?>"<?= $values['report_type'] === $key ? ' selected' : '' ?>><?= e($label) ?></option><?php endforeach; ?></select></div>
<div><label for="feedback-area">Area</label><select id="feedback-area" name="area" required><?php foreach ($areas as $key => $label): ?><option value="<?= e($key) ?>"<?= $values['area'] === $key ? ' selected' : '' ?>><?= e($label) ?></option><?php endforeach; ?></select></div>
</div>
<label for="feedback-title">Short title</label><input id="feedback-title" name="title" type="text" minlength="5" maxlength="120" value="<?= e($values['title']) ?>" placeholder="Example: Guard stops fighting after one raider falls" required>
<label for="feedback-impact">How much does this affect you?</label><select id="feedback-impact" name="impact" required><?php foreach ($impacts as $key => $label): ?><option value="<?= e($key) ?>"<?= $values['impact'] === $key ? ' selected' : '' ?>><?= e($label) ?></option><?php endforeach; ?></select>
<label for="feedback-details">What happened, or what would you like added?</label><textarea id="feedback-details" name="details" minlength="20" maxlength="5000" rows="7" placeholder="Include who or what was involved and why this matters." required><?= e($values['details']) ?></textarea>
<label for="feedback-steps">Steps to reproduce <span class="field-optional">(bugs only, if known)</span></label><textarea id="feedback-steps" name="steps_to_reproduce" maxlength="3000" rows="4" placeholder="1. Entered Stonehaven through the north gate…"><?= e($values['steps_to_reproduce']) ?></textarea>
<div class="feedback-fields two-column"><div><label for="expected-result">What did you expect?</label><textarea id="expected-result" name="expected_result" maxlength="3000" rows="4"><?= e($values['expected_result']) ?></textarea></div><div><label for="actual-result">What happened instead?</label><textarea id="actual-result" name="actual_result" maxlength="3000" rows="4"><?= e($values['actual_result']) ?></textarea></div></div>
<div class="feedback-fields two-column"><div><label for="world-location">World location</label><input id="world-location" name="world_location" type="text" maxlength="120" value="<?= e($values['world_location']) ?>" placeholder="Example: Grid B2, front gate"></div><div><label for="build-version">Game build</label><input id="build-version" name="build_version" type="text" maxlength="30" value="<?= e($values['build_version']) ?>" placeholder="Example: 0.9.1 or Not sure"></div></div>
<label for="feedback-screenshot">Screenshot <span class="field-optional">(optional)</span></label><input id="feedback-screenshot" name="screenshot" type="file" accept="image/png,image/jpeg,image/webp"><p class="form-note">PNG, JPG, or WebP up to 5 MB. Press F12 in the game to save a screenshot. Do not include passwords or other private information.</p>
<button class="button" type="submit">Submit Feedback</button>
</form></section>
<aside class="feedback-guide"><div class="panel"><p class="eyebrow">Helpful Reports</p><h2>What helps us fix it</h2><ol><li>Say exactly where you were and what you were doing.</li><li>Include the names of NPCs, creatures, or buildings involved.</li><li>Tell us whether it happens every time.</li><li>Add a screenshot when the problem is visible.</li></ol><p class="form-note">All report times are recorded in Central time.</p></div></aside>
</div>
<section id="my-reports" class="feedback-history"><p class="eyebrow">Your Account</p><h2>My Reports</h2>
<?php if (!$reports): ?><div class="panel"><p>You have not submitted any feedback yet.</p></div><?php else: ?><div class="feedback-ticket-list"><?php foreach ($reports as $report): ?><article class="feedback-ticket"><div class="feedback-ticket-heading"><div><span class="ticket-number"><?= e(feedback_ticket_number((int)$report['id'])) ?></span><h3><?= e($report['title']) ?></h3></div><span class="status-badge status-<?= e($report['status']) ?>"><?= e(feedback_status_label($report['status'])) ?></span></div><p class="feedback-meta"><?= e($reportTypes[$report['report_type']] ?? ucfirst($report['report_type'])) ?> &bull; <?= e($areas[$report['area']] ?? ucfirst($report['area'])) ?> &bull; Submitted <?= e($report['created_at']) ?> Central</p><?php if ($report['screenshot_original_name']): ?><p><a href="/feedback-attachment.php?id=<?= (int)$report['id'] ?>">View attached screenshot</a></p><?php endif; ?><?php if (trim((string)$report['player_response']) !== ''): ?><div class="player-response"><strong>Development response</strong><p><?= nl2br(e((string)$report['player_response'])) ?></p></div><?php endif; ?></article><?php endforeach; ?></div><?php endif; ?>
</section></main>
<?php require __DIR__ . '/includes/footer.php'; ?>
