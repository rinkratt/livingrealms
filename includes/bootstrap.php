<?php
declare(strict_types=1);

$configPath = dirname(__DIR__, 2) . '/private/living-realms-config.php';
if (!is_file($configPath)) {
    http_response_code(503);
    exit('Site configuration is unavailable.');
}
$config = require $configPath;

header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: DENY');
header('Referrer-Policy: strict-origin-when-cross-origin');
header("Permissions-Policy: geolocation=(), camera=(), microphone=()");
header("Content-Security-Policy: default-src 'self'; img-src 'self' data:; style-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'");

if (session_status() !== PHP_SESSION_ACTIVE) {
    session_name('living_realms_portal');
    session_set_cookie_params(['lifetime' => 0, 'path' => '/', 'secure' => true, 'httponly' => true, 'samesite' => 'Strict']);
    session_start();
}

function db(): PDO
{
    global $config;
    static $pdo;
    if ($pdo instanceof PDO) return $pdo;
    $pdo = new PDO('sqlite:' . $config['database_path'], null, null, [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]);
    $pdo->exec('PRAGMA journal_mode=WAL');
    $pdo->exec('CREATE TABLE IF NOT EXISTS visitor_events (
        id INTEGER PRIMARY KEY AUTOINCREMENT, visited_at TEXT NOT NULL, ip_address TEXT NOT NULL,
        country_code TEXT, country TEXT, region TEXT, city TEXT, timezone TEXT, isp TEXT,
        path TEXT NOT NULL, method TEXT NOT NULL, referrer_domain TEXT, user_agent TEXT,
        accept_language TEXT, is_bot INTEGER NOT NULL DEFAULT 0
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_visitor_events_time ON visitor_events(visited_at DESC)');
    $pdo->exec('CREATE TABLE IF NOT EXISTS login_events (
        id INTEGER PRIMARY KEY AUTOINCREMENT, attempted_at TEXT NOT NULL, ip_address TEXT NOT NULL,
        email TEXT NOT NULL, successful INTEGER NOT NULL, user_agent TEXT
    )');
    $pdo->exec('CREATE TABLE IF NOT EXISTS app_meta (meta_key TEXT PRIMARY KEY, meta_value TEXT NOT NULL)');
    $pdo->exec('CREATE TABLE IF NOT EXISTS player_signups (
        id INTEGER PRIMARY KEY AUTOINCREMENT, created_at TEXT NOT NULL, email TEXT NOT NULL COLLATE NOCASE UNIQUE,
        player_name TEXT NOT NULL, play_style TEXT NOT NULL, testing_interest TEXT NOT NULL,
        discord_name TEXT, ip_address TEXT NOT NULL, user_agent TEXT, status TEXT NOT NULL DEFAULT "new", admin_notes TEXT
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_player_signups_created ON player_signups(created_at DESC)');
    $pdo->exec('CREATE TABLE IF NOT EXISTS player_access_events (
        id INTEGER PRIMARY KEY AUTOINCREMENT, occurred_at TEXT NOT NULL, ip_address TEXT NOT NULL,
        email TEXT, action TEXT NOT NULL, successful INTEGER NOT NULL, user_agent TEXT
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_player_access_events_time ON player_access_events(occurred_at DESC)');
    $pdo->exec('CREATE TABLE IF NOT EXISTS player_feedback (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        created_at TEXT NOT NULL, updated_at TEXT NOT NULL, resolved_at TEXT,
        email TEXT NOT NULL COLLATE NOCASE, report_type TEXT NOT NULL, title TEXT NOT NULL,
        area TEXT NOT NULL, impact TEXT NOT NULL, details TEXT NOT NULL,
        steps_to_reproduce TEXT, expected_result TEXT, actual_result TEXT, world_location TEXT,
        build_version TEXT, source TEXT NOT NULL DEFAULT "website",
        status TEXT NOT NULL DEFAULT "new", priority TEXT NOT NULL DEFAULT "normal",
        admin_notes TEXT, player_response TEXT,
        screenshot_original_name TEXT, screenshot_storage_name TEXT,
        screenshot_mime TEXT, screenshot_size INTEGER,
        ip_address TEXT NOT NULL, user_agent TEXT
    )');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_player_feedback_created ON player_feedback(created_at DESC)');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_player_feedback_email ON player_feedback(email, created_at DESC)');
    $pdo->exec('CREATE INDEX IF NOT EXISTS idx_player_feedback_status ON player_feedback(status, report_type, created_at DESC)');
    $migrated = $pdo->query("SELECT meta_value FROM app_meta WHERE meta_key = 'timestamps_central'")->fetchColumn();
    if (!$migrated) {
        $utc = new DateTimeZone('UTC');
        $central = new DateTimeZone($config['timezone']);
        foreach (['visitor_events' => 'visited_at', 'login_events' => 'attempted_at'] as $table => $column) {
            $rows = $pdo->query("SELECT id, $column AS event_time FROM $table")->fetchAll(PDO::FETCH_ASSOC);
            $update = $pdo->prepare("UPDATE $table SET $column = ? WHERE id = ?");
            foreach ($rows as $row) {
                $dt = DateTimeImmutable::createFromFormat('Y-m-d H:i:s', $row['event_time'], $utc);
                if ($dt) $update->execute([$dt->setTimezone($central)->format('Y-m-d H:i:s'), $row['id']]);
            }
        }
        $pdo->prepare("INSERT OR REPLACE INTO app_meta (meta_key, meta_value) VALUES ('timestamps_central', '1')")->execute();
    }
    return $pdo;
}

function central_now(): DateTimeImmutable
{
    global $config;
    return new DateTimeImmutable('now', new DateTimeZone($config['timezone']));
}

function client_ip(): string
{
    $ip = $_SERVER['REMOTE_ADDR'] ?? 'unknown';
    return filter_var($ip, FILTER_VALIDATE_IP) ? $ip : 'unknown';
}

function geo_for_ip(string $ip): array
{
    if (!filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE)) return [];
    $context = stream_context_create(['http' => ['timeout' => 1.5, 'user_agent' => 'LivingRealms/1.0']]);
    $raw = @file_get_contents('https://ipwho.is/' . rawurlencode($ip), false, $context);
    if (!$raw) return [];
    $data = json_decode($raw, true);
    return is_array($data) && ($data['success'] ?? false) ? $data : [];
}

function record_visit(): void
{
    try {
        $ip = client_ip();
        $geo = geo_for_ip($ip);
        $referrerHost = null;
        if (!empty($_SERVER['HTTP_REFERER'])) $referrerHost = parse_url($_SERVER['HTTP_REFERER'], PHP_URL_HOST) ?: null;
        $ua = substr($_SERVER['HTTP_USER_AGENT'] ?? '', 0, 500);
        $stmt = db()->prepare('INSERT INTO visitor_events
            (visited_at, ip_address, country_code, country, region, city, timezone, isp, path, method, referrer_domain, user_agent, accept_language, is_bot)
            VALUES (:at,:ip,:cc,:country,:region,:city,:tz,:isp,:path,:method,:ref,:ua,:lang,:bot)');
        $stmt->execute([
            ':at' => central_now()->format('Y-m-d H:i:s'), ':ip' => $ip, ':cc' => $geo['country_code'] ?? null,
            ':country' => $geo['country'] ?? null, ':region' => $geo['region'] ?? null,
            ':city' => $geo['city'] ?? null, ':tz' => $geo['timezone']['id'] ?? null,
            ':isp' => $geo['connection']['isp'] ?? null,
            ':path' => parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH) ?: '/',
            ':method' => $_SERVER['REQUEST_METHOD'] ?? 'GET', ':ref' => $referrerHost,
            ':ua' => $ua, ':lang' => substr($_SERVER['HTTP_ACCEPT_LANGUAGE'] ?? '', 0, 120),
            ':bot' => preg_match('/bot|crawl|spider|slurp|preview/i', $ua) ? 1 : 0,
        ]);
    } catch (Throwable $e) { error_log('Living Realms visit logging failed: ' . $e->getMessage()); }
}

function csrf_token(): string
{
    if (empty($_SESSION['csrf'])) $_SESSION['csrf'] = bin2hex(random_bytes(32));
    return $_SESSION['csrf'];
}

function safe_local_return_path(?string $value, string $fallback): string
{
    $value = trim((string)$value);
    if ($value === '' || strlen($value) > 500 || $value[0] !== '/' || str_starts_with($value, '//')) return $fallback;
    if (str_contains($value, "\r") || str_contains($value, "\n")) return $fallback;
    $parts = parse_url($value);
    if ($parts === false || isset($parts['scheme']) || isset($parts['host'])) return $fallback;
    return $value;
}

function feedback_upload_directory(): string
{
    $configured = trim((string)($GLOBALS['config']['feedback_upload_path'] ?? ''));
    return $configured !== '' ? $configured : dirname(__DIR__, 2) . '/private/living-realms-feedback';
}

function feedback_ticket_number(int $id): string
{
    return 'LR-' . str_pad((string)$id, 5, '0', STR_PAD_LEFT);
}

function feedback_status_label(string $status): string
{
    return match ($status) {
        'reviewing' => 'Under Review',
        'planned' => 'Planned',
        'in_progress' => 'In Progress',
        'completed' => 'Completed',
        'declined' => 'Not Planned',
        default => 'New',
    };
}

function game_api_request(string $method, string $path, ?array $payload = null, ?string $token = null): array
{
    $baseUrl = rtrim((string)($GLOBALS['config']['game_api_base_url'] ?? 'https://living-realms.com/game-api'), '/');
    $url = $baseUrl . '/' . ltrim($path, '/');
    $headers = ['Accept: application/json', 'User-Agent: LivingRealmsWebsite/1.0'];
    $body = null;
    if ($payload !== null) {
        $body = json_encode($payload, JSON_THROW_ON_ERROR);
        $headers[] = 'Content-Type: application/json';
    }
    if ($token !== null && $token !== '') $headers[] = 'Authorization: Bearer ' . $token;

    $status = 0;
    $raw = '';
    $transportError = null;
    if (function_exists('curl_init')) {
        $handle = curl_init($url);
        curl_setopt_array($handle, [
            CURLOPT_CUSTOMREQUEST => strtoupper($method),
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT => 5,
            CURLOPT_TIMEOUT => 15,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
        ]);
        if ($body !== null) curl_setopt($handle, CURLOPT_POSTFIELDS, $body);
        $response = curl_exec($handle);
        if ($response === false) $transportError = curl_error($handle);
        else $raw = (string)$response;
        $status = (int)curl_getinfo($handle, CURLINFO_RESPONSE_CODE);
        curl_close($handle);
    } else {
        $context = stream_context_create(['http' => [
            'method' => strtoupper($method),
            'header' => implode("\r\n", $headers),
            'content' => $body ?? '',
            'timeout' => 15,
            'ignore_errors' => true,
        ]]);
        $response = @file_get_contents($url, false, $context);
        if ($response === false) $transportError = 'The game account service could not be reached.';
        else $raw = (string)$response;
        foreach ($http_response_header ?? [] as $header) {
            if (preg_match('/^HTTP\/\S+\s+(\d{3})/', $header, $matches)) {
                $status = (int)$matches[1];
                break;
            }
        }
    }

    $decoded = json_decode($raw, true);
    return [
        'status' => $status,
        'data' => is_array($decoded) ? $decoded : [],
        'body' => $raw,
        'error' => $transportError,
    ];
}

function set_player_access(array $authentication): bool
{
    $token = (string)($authentication['token'] ?? '');
    $email = strtolower(trim((string)($authentication['account']['email'] ?? '')));
    $expiresAt = (string)($authentication['expiresAt'] ?? '');
    $expiresTimestamp = strtotime($expiresAt);
    if ($token === '' || !filter_var($email, FILTER_VALIDATE_EMAIL) || $expiresTimestamp === false) return false;
    session_regenerate_id(true);
    $_SESSION['player_access'] = [
        'token' => $token,
        'email' => $email,
        'expires_at' => $expiresTimestamp,
    ];
    return true;
}

function player_access(): ?array
{
    $access = $_SESSION['player_access'] ?? null;
    if (!is_array($access) || empty($access['token']) || empty($access['email']) || (int)($access['expires_at'] ?? 0) <= time()) {
        unset($_SESSION['player_access']);
        return null;
    }
    return $access;
}

function is_player(): bool { return player_access() !== null; }

function clear_player_access(bool $notifyApi = true): void
{
    $access = player_access();
    if ($notifyApi && $access !== null) {
        try { game_api_request('POST', '/api/v1/auth/logout', null, (string)$access['token']); }
        catch (Throwable $e) { error_log('Living Realms player logout failed: ' . $e->getMessage()); }
    }
    unset($_SESSION['player_access']);
    session_regenerate_id(true);
}

function record_player_access_event(string $action, bool $successful, ?string $email = null): void
{
    try {
        $stmt = db()->prepare('INSERT INTO player_access_events
            (occurred_at, ip_address, email, action, successful, user_agent) VALUES (?,?,?,?,?,?)');
        $stmt->execute([
            central_now()->format('Y-m-d H:i:s'), client_ip(),
            $email === null ? null : substr(strtolower(trim($email)), 0, 320),
            substr($action, 0, 40), $successful ? 1 : 0,
            substr($_SERVER['HTTP_USER_AGENT'] ?? '', 0, 500),
        ]);
    } catch (Throwable $e) { error_log('Living Realms player access logging failed: ' . $e->getMessage()); }
}

function is_admin(): bool { return ($_SESSION['admin_email'] ?? null) === ($GLOBALS['config']['admin_email'] ?? null); }
function require_admin(): void { if (!is_admin()) { header('Location: /login.php'); exit; } }
function e(?string $value): string { return htmlspecialchars($value ?? '', ENT_QUOTES, 'UTF-8'); }

function content_data(): array
{
    static $data;
    if ($data === null) $data = require dirname(__DIR__) . '/content/site.php';
    return $data;
}
