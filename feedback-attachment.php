<?php
declare(strict_types=1);
require __DIR__ . '/includes/bootstrap.php';

$id = (int)($_GET['id'] ?? 0);
$stmt = db()->prepare('SELECT email, screenshot_original_name, screenshot_storage_name, screenshot_mime FROM player_feedback WHERE id = ?');
$stmt->execute([$id]);
$attachment = $stmt->fetch(PDO::FETCH_ASSOC);
$access = player_access();
$authorized = is_admin() || ($access !== null && $attachment && strcasecmp((string)$access['email'], (string)$attachment['email']) === 0);
if (!$attachment || !$authorized || empty($attachment['screenshot_storage_name'])) {
    http_response_code(404);
    exit('Screenshot not found.');
}

$storedName = basename((string)$attachment['screenshot_storage_name']);
$path = feedback_upload_directory() . DIRECTORY_SEPARATOR . $storedName;
if (!is_file($path)) {
    http_response_code(404);
    exit('Screenshot not found.');
}

$mime = in_array($attachment['screenshot_mime'], ['image/png', 'image/jpeg', 'image/webp'], true)
    ? (string)$attachment['screenshot_mime'] : 'application/octet-stream';
$downloadName = preg_replace('/[^a-z0-9._-]+/i', '-', (string)$attachment['screenshot_original_name']) ?: 'screenshot';
header('Content-Type: ' . $mime);
header('Content-Length: ' . (string)filesize($path));
header('Content-Disposition: inline; filename="' . $downloadName . '"');
header('Cache-Control: private, no-store');
readfile($path);
