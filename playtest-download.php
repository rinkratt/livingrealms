<?php
declare(strict_types=1);
require __DIR__ . '/includes/bootstrap.php';

$access = player_access();
if ($access === null && !is_admin()) {
    record_player_access_event('download_denied', false, null);
    header('Location: /playtest/?access=required#player-access', true, 303);
    exit;
}

$packageName = 'LivingRealms-Playtest-Windows-0.9.5.zip';
$packagePath = __DIR__ . '/downloads/' . $packageName;
if (!is_file($packagePath) || !is_readable($packagePath)) {
    http_response_code(503);
    exit('The Windows playtest package is temporarily unavailable.');
}

$email = $access['email'] ?? ($GLOBALS['config']['admin_email'] ?? null);
record_player_access_event('download', true, is_string($email) ? $email : null);
session_write_close();
set_time_limit(0);

header('Content-Type: application/zip');
header('Content-Disposition: attachment; filename="' . $packageName . '"');
header('Content-Length: ' . (string)filesize($packagePath));
header('Cache-Control: private, no-store, max-age=0');
header('X-Content-Type-Options: nosniff');
while (ob_get_level() > 0) ob_end_clean();
$handle = fopen($packagePath, 'rb');
if ($handle === false) {
    http_response_code(503);
    exit;
}
fpassthru($handle);
fclose($handle);
exit;
