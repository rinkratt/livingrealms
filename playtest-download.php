<?php
declare(strict_types=1);
require __DIR__ . '/includes/bootstrap.php';

$access = player_access();
if ($access === null && !is_admin()) {
    record_player_access_event('download_denied', false, null);
    header('Location: /playtest/?access=required#player-access', true, 303);
    exit;
}

$packageName = 'LivingRealms-Playtest-Windows-0.9.19.zip';
$packagePath = __DIR__ . '/downloads/' . $packageName;
if (!is_file($packagePath) || !is_readable($packagePath)) {
    http_response_code(503);
    exit('The Windows playtest package is temporarily unavailable.');
}

$email = $access['email'] ?? ($GLOBALS['config']['admin_email'] ?? null);
record_player_access_event('download', true, is_string($email) ? $email : null);
session_write_close();

// Authentication and activity logging happen here, but the web server should
// transfer the large ZIP itself. Streaming it through PHP can be interrupted
// by a FastCGI or proxy timeout and leave Windows with an incomplete archive.
header('Location: /downloads/' . rawurlencode($packageName), true, 303);
exit;
