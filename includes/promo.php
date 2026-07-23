<?php
function promo_start(string $title, string $description, string $headline, string $lead): void
{
    $GLOBALS['pageTitle'] = $title . ' | Living Realms';
    $GLOBALS['pageDescription'] = $description;
    require dirname(__DIR__) . '/includes/header.php';
    echo '<main class="promo-main"><section class="page-hero"><p class="eyebrow">The World Remembers</p><h1>' . e($headline) . '</h1><p class="lead">' . e($lead) . '</p></section>';
}
function promo_end(): void
{
    echo '</main>';
    require dirname(__DIR__) . '/includes/footer.php';
}
function lore_cards(array $items, bool $withRegion = false): void
{
    echo '<section class="section"><div class="section-inner card-grid">';
    foreach ($items as $item) {
        echo '<article class="lore-card">';
        if ($withRegion) echo '<p class="lore-meta">' . e($item[1]) . '</p>';
        echo '<h2>' . e($item[0]) . '</h2>';
        $body = $withRegion ? $item[2] : [$item[1]];
        foreach ($body as $paragraph) echo '<p class="lore-quote">' . e($paragraph) . '</p>';
        echo '</article>';
    }
    echo '</div></section>';
}
