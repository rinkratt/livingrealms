<?php
declare(strict_types=1);
require dirname(__DIR__) . '/includes/bootstrap.php';
require dirname(__DIR__) . '/includes/promo.php';
record_visit();
$errors=[]; $success=false;
$values=['email'=>'','player_name'=>'','play_style'=>'','testing_interest'=>'','discord_name'=>''];
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    foreach($values as $key=>$unused) $values[$key]=trim((string)($_POST[$key] ?? ''));
    if (!hash_equals(csrf_token(), (string)($_POST['csrf'] ?? ''))) $errors[]='Your form session expired. Please try again.';
    if (!empty($_POST['realm_name'])) $errors[]='Unable to accept this submission.';
    if (!filter_var($values['email'], FILTER_VALIDATE_EMAIL)) $errors[]='Enter a valid email address.';
    if (mb_strlen($values['player_name']) < 2 || mb_strlen($values['player_name']) > 40) $errors[]='Player name must be between 2 and 40 characters.';
    $styles=['Melee combat','Ranged combat','Magic and support','Exploration','Crafting and economy','Roleplay and lore','Undecided'];
    $interests=['Yes — alpha testing','Yes — beta testing','Both alpha and beta','Development updates only'];
    if (!in_array($values['play_style'],$styles,true)) $errors[]='Choose a preferred play style.';
    if (!in_array($values['testing_interest'],$interests,true)) $errors[]='Choose your testing interest.';
    if (mb_strlen($values['discord_name']) > 80) $errors[]='Discord name is too long.';
    if (!$errors) {
        try {
            $stmt=db()->prepare('INSERT INTO player_signups (created_at,email,player_name,play_style,testing_interest,discord_name,ip_address,user_agent) VALUES (?,?,?,?,?,?,?,?)');
            $stmt->execute([central_now()->format('Y-m-d H:i:s'),strtolower($values['email']),$values['player_name'],$values['play_style'],$values['testing_interest'],$values['discord_name'] ?: null,client_ip(),substr($_SERVER['HTTP_USER_AGENT'] ?? '',0,500)]);
            $success=true; $values=array_fill_keys(array_keys($values),'');
        } catch (PDOException $ex) {
            $errors[] = str_contains($ex->getMessage(),'UNIQUE') ? 'That email is already registered with the realm.' : 'Registration could not be saved. Please try again.';
        }
    }
}
promo_start('Join the Realm','Register to become a founding Living Realms player and receive development and testing opportunities.','The Realm Has Already Begun','The world is already moving. The only question is whether you will shape its history—or arrive after someone else has.'); ?>
<section class="section"><div class="section-inner signup-layout"><div><p class="eyebrow">Become a Founding Player</p><h2>Somewhere, a weak creature survives its first battle.</h2><p class="lead">Somewhere, a camp gathers enough wood to raise its first wall. Somewhere, a village prepares for an attack it does not yet know is coming.</p><p>Register your interest and tell us how you hope to enter the realm. Your signup will be stored securely for development and testing invitations.</p></div><div class="signup-card">
<?php if($success): ?><div class="success" role="status"><h2>Welcome to the Chronicle.</h2><p>Your registration has been received. The realm now knows your name.</p></div><?php else: ?>
<?php if($errors): ?><div class="error" role="alert"><strong>Please correct the following:</strong><ul><?php foreach($errors as $error): ?><li><?= e($error) ?></li><?php endforeach; ?></ul></div><?php endif; ?>
<form method="post"><input type="hidden" name="csrf" value="<?= e(csrf_token()) ?>"><div class="honeypot" aria-hidden="true"><label for="realm_name">Realm name</label><input id="realm_name" name="realm_name" tabindex="-1" autocomplete="off"></div>
<label for="email">Email address</label><input id="email" name="email" type="email" maxlength="254" autocomplete="email" value="<?= e($values['email']) ?>" required>
<label for="player_name">Preferred player name</label><input id="player_name" name="player_name" maxlength="40" value="<?= e($values['player_name']) ?>" required>
<label for="play_style">Preferred play style</label><select id="play_style" name="play_style" required><option value="">Choose one</option><?php foreach(['Melee combat','Ranged combat','Magic and support','Exploration','Crafting and economy','Roleplay and lore','Undecided'] as $option): ?><option<?= $values['play_style']===$option?' selected':'' ?>><?= e($option) ?></option><?php endforeach; ?></select>
<label for="testing_interest">Testing interest</label><select id="testing_interest" name="testing_interest" required><option value="">Choose one</option><?php foreach(['Yes — alpha testing','Yes — beta testing','Both alpha and beta','Development updates only'] as $option): ?><option<?= $values['testing_interest']===$option?' selected':'' ?>><?= e($option) ?></option><?php endforeach; ?></select>
<label for="discord_name">Discord name <span aria-label="optional">(optional)</span></label><input id="discord_name" name="discord_name" maxlength="80" value="<?= e($values['discord_name']) ?>">
<p class="form-note">By registering, you consent to Living Realms storing this information for game development updates and testing invitations. You may request removal by contacting the address on our privacy page.</p><button class="button" type="submit">Join the Founding Players</button></form><?php endif; ?></div></div></section>
<?php promo_end(); ?>
