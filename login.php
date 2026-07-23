<?php
declare(strict_types=1); require __DIR__ . '/includes/bootstrap.php';
if (is_admin()) { header('Location: /portal.php'); exit; }
$error = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $email = strtolower(trim($_POST['email'] ?? ''));
    $password = $_POST['password'] ?? '';
    $ip = client_ip();
    $recent = db()->prepare('SELECT COUNT(*) FROM login_events WHERE ip_address = ? AND successful = 0 AND attempted_at > ?');
    $recent->execute([$ip, central_now()->modify('-15 minutes')->format('Y-m-d H:i:s')]);
    $allowed = (int)$recent->fetchColumn() < 8;
    $validCsrf = hash_equals(csrf_token(), $_POST['csrf'] ?? '');
    $ok = $allowed && $validCsrf && hash_equals(strtolower($config['admin_email']), $email) && password_verify($password, $config['admin_password_hash']);
    $log = db()->prepare('INSERT INTO login_events (attempted_at,ip_address,email,successful,user_agent) VALUES (?,?,?,?,?)');
    $log->execute([central_now()->format('Y-m-d H:i:s'), $ip, substr($email,0,254), $ok ? 1 : 0, substr($_SERVER['HTTP_USER_AGENT'] ?? '',0,500)]);
    if ($ok) { session_regenerate_id(true); $_SESSION['admin_email'] = $config['admin_email']; header('Location: /portal.php'); exit; }
    $error = $allowed ? 'The email or password was not recognized.' : 'Too many attempts. Please wait 15 minutes.';
}
$pageTitle = 'Portal Login | Living Realms'; require __DIR__ . '/includes/header.php'; ?>
<main class="auth-main"><section class="auth-card"><p class="eyebrow">Private Access</p><h1>Portal Login</h1><p>Enter your administrator credentials to continue.</p>
<?php if ($error): ?><p class="error" role="alert"><?= e($error) ?></p><?php endif; ?>
<form method="post" action="/login.php"><input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><label for="admin-email">Administrator email</label><input id="admin-email" name="email" type="email" autocomplete="section-admin username" required><label for="admin-password">Administrator password</label><input id="admin-password" name="password" type="password" autocomplete="section-admin current-password" required><button class="button" type="submit">Enter Admin Portal</button></form></section></main>
<?php require __DIR__ . '/includes/footer.php'; ?>
