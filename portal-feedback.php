<?php
declare(strict_types=1);
require __DIR__ . '/includes/bootstrap.php';
require_admin();

$statuses = ['new', 'reviewing', 'planned', 'in_progress', 'completed', 'declined'];
$priorities = ['low', 'normal', 'high', 'urgent'];
$notice = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    if (!hash_equals(csrf_token(), (string)($_POST['csrf'] ?? ''))) {
        $notice = 'That update expired. Please try again.';
    } else {
        $id = (int)($_POST['id'] ?? 0);
        $status = (string)($_POST['status'] ?? 'new');
        $priority = (string)($_POST['priority'] ?? 'normal');
        $adminNotes = trim((string)($_POST['admin_notes'] ?? ''));
        $playerResponse = trim((string)($_POST['player_response'] ?? ''));
        if ($id > 0 && in_array($status, $statuses, true) && in_array($priority, $priorities, true)
            && mb_strlen($adminNotes) <= 5000 && mb_strlen($playerResponse) <= 3000) {
            $now = central_now()->format('Y-m-d H:i:s');
            $resolvedAt = in_array($status, ['completed', 'declined'], true) ? $now : null;
            $stmt = db()->prepare('UPDATE player_feedback SET status = ?, priority = ?, admin_notes = ?, player_response = ?, updated_at = ?, resolved_at = ? WHERE id = ?');
            $stmt->execute([$status, $priority, $adminNotes ?: null, $playerResponse ?: null, $now, $resolvedAt, $id]);
            header('Location: /portal-feedback.php?updated=' . $id . '#ticket-' . $id, true, 303);
            exit;
        }
        $notice = 'The ticket update was not valid.';
    }
}

$statusFilter = (string)($_GET['status'] ?? 'open');
$typeFilter = (string)($_GET['type'] ?? 'all');
$conditions = [];
$parameters = [];
if ($statusFilter === 'open') $conditions[] = "status NOT IN ('completed','declined')";
elseif (in_array($statusFilter, $statuses, true)) { $conditions[] = 'status = ?'; $parameters[] = $statusFilter; }
if (in_array($typeFilter, ['bug', 'feature'], true)) { $conditions[] = 'report_type = ?'; $parameters[] = $typeFilter; }
$where = $conditions ? ' WHERE ' . implode(' AND ', $conditions) : '';
$stmt = db()->prepare('SELECT * FROM player_feedback' . $where . ' ORDER BY CASE priority WHEN "urgent" THEN 0 WHEN "high" THEN 1 WHEN "normal" THEN 2 ELSE 3 END, id DESC LIMIT 250');
$stmt->execute($parameters);
$reports = $stmt->fetchAll(PDO::FETCH_ASSOC);
$counts = db()->query("SELECT COUNT(*) AS total, SUM(CASE WHEN status = 'new' THEN 1 ELSE 0 END) AS new_count, SUM(CASE WHEN report_type = 'bug' AND status NOT IN ('completed','declined') THEN 1 ELSE 0 END) AS open_bugs, SUM(CASE WHEN report_type = 'feature' AND status NOT IN ('completed','declined') THEN 1 ELSE 0 END) AS open_features FROM player_feedback")->fetch(PDO::FETCH_ASSOC);
$pageTitle = 'Player Feedback | Living Realms Admin';
require __DIR__ . '/includes/header.php';
?>
<main class="portal-main"><p class="eyebrow">Administrator</p><h1>Player Feedback</h1>
<div class="portal-tabs"><a href="/portal.php">Visitor Activity</a><a href="/portal-signups.php">Player Signups</a><a href="/portal-feedback.php">Bug &amp; Feature Reports</a></div>
<div class="stats feedback-stats"><div class="panel stat"><strong><?= (int)($counts['new_count'] ?? 0) ?></strong>New</div><div class="panel stat"><strong><?= (int)($counts['open_bugs'] ?? 0) ?></strong>Open bugs</div><div class="panel stat"><strong><?= (int)($counts['open_features'] ?? 0) ?></strong>Open features</div></div>
<?php if ($notice): ?><div class="error" role="alert"><?= e($notice) ?></div><?php elseif (isset($_GET['updated'])): ?><div class="success" role="status">Ticket <?= e(feedback_ticket_number((int)$_GET['updated'])) ?> was updated.</div><?php endif; ?>
<form class="panel feedback-filter" method="get"><div><label for="filter-status">Status</label><select id="filter-status" name="status"><option value="open"<?= $statusFilter === 'open' ? ' selected' : '' ?>>All open</option><option value="all"<?= $statusFilter === 'all' ? ' selected' : '' ?>>All statuses</option><?php foreach ($statuses as $status): ?><option value="<?= e($status) ?>"<?= $statusFilter === $status ? ' selected' : '' ?>><?= e(feedback_status_label($status)) ?></option><?php endforeach; ?></select></div><div><label for="filter-type">Type</label><select id="filter-type" name="type"><option value="all"<?= $typeFilter === 'all' ? ' selected' : '' ?>>Bugs and features</option><option value="bug"<?= $typeFilter === 'bug' ? ' selected' : '' ?>>Bug reports</option><option value="feature"<?= $typeFilter === 'feature' ? ' selected' : '' ?>>Feature requests</option></select></div><button class="button secondary" type="submit">Apply Filters</button></form>
<p class="form-note"><strong><?= count($reports) ?></strong> matching ticket<?= count($reports) === 1 ? '' : 's' ?>. Submission and update times are Central.</p>
<div class="admin-feedback-list"><?php if (!$reports): ?><div class="panel"><p>No feedback matches these filters.</p></div><?php else: ?><?php foreach ($reports as $report): ?><article class="panel admin-feedback-ticket" id="ticket-<?= (int)$report['id'] ?>">
<div class="feedback-ticket-heading"><div><span class="ticket-number"><?= e(feedback_ticket_number((int)$report['id'])) ?> &bull; <?= e(strtoupper($report['report_type'])) ?></span><h2><?= e($report['title']) ?></h2></div><span class="status-badge status-<?= e($report['status']) ?>"><?= e(feedback_status_label($report['status'])) ?></span></div>
<p class="feedback-meta"><a href="mailto:<?= e($report['email']) ?>"><?= e($report['email']) ?></a> &bull; <?= e($report['area']) ?> &bull; <?= e($report['impact']) ?> impact &bull; build <?= e($report['build_version'] ?: 'not supplied') ?> &bull; <?= e($report['source']) ?> &bull; submitted <?= e($report['created_at']) ?> Central</p>
<div class="feedback-report-body"><div><h3>Report</h3><p><?= nl2br(e($report['details'])) ?></p></div><?php if ($report['world_location']): ?><div><h3>Location</h3><p><?= e($report['world_location']) ?></p></div><?php endif; ?><?php if ($report['steps_to_reproduce']): ?><div><h3>Steps to reproduce</h3><p><?= nl2br(e($report['steps_to_reproduce'])) ?></p></div><?php endif; ?><?php if ($report['expected_result']): ?><div><h3>Expected</h3><p><?= nl2br(e($report['expected_result'])) ?></p></div><?php endif; ?><?php if ($report['actual_result']): ?><div><h3>Actual</h3><p><?= nl2br(e($report['actual_result'])) ?></p></div><?php endif; ?></div>
<?php if ($report['screenshot_storage_name']): ?><p><a class="button secondary" href="/feedback-attachment.php?id=<?= (int)$report['id'] ?>" target="_blank" rel="noopener">View Screenshot</a> <span class="form-note"><?= e($report['screenshot_original_name']) ?> &bull; <?= number_format((int)$report['screenshot_size'] / 1024, 0) ?> KB</span></p><?php endif; ?>
<form class="feedback-admin-form" method="post"><input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><input type="hidden" name="id" value="<?= (int)$report['id'] ?>"><div class="feedback-fields two-column"><div><label for="status-<?= (int)$report['id'] ?>">Status</label><select id="status-<?= (int)$report['id'] ?>" name="status"><?php foreach ($statuses as $status): ?><option value="<?= e($status) ?>"<?= $report['status'] === $status ? ' selected' : '' ?>><?= e(feedback_status_label($status)) ?></option><?php endforeach; ?></select></div><div><label for="priority-<?= (int)$report['id'] ?>">Priority</label><select id="priority-<?= (int)$report['id'] ?>" name="priority"><?php foreach ($priorities as $priority): ?><option value="<?= e($priority) ?>"<?= $report['priority'] === $priority ? ' selected' : '' ?>><?= e(ucfirst($priority)) ?></option><?php endforeach; ?></select></div></div><label for="response-<?= (int)$report['id'] ?>">Response visible to player</label><textarea id="response-<?= (int)$report['id'] ?>" name="player_response" rows="3" maxlength="3000"><?= e($report['player_response']) ?></textarea><label for="notes-<?= (int)$report['id'] ?>">Private administrator notes</label><textarea id="notes-<?= (int)$report['id'] ?>" name="admin_notes" rows="3" maxlength="5000"><?= e($report['admin_notes']) ?></textarea><button class="button" type="submit">Save Ticket</button></form>
</article><?php endforeach; ?><?php endif; ?></div></main>
<?php require __DIR__ . '/includes/footer.php'; ?>
