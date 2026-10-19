# Shared weekly statistics

Released in Osiris Beta 0.0.52 with Stats 0.1.4 and Trophies 0.1.1.

The Stats extension owns the weekly snapshot used by both the Stats page and
Home. Home reads GetHomeWeeklyStatsForOsiris; the Stats page renders that same
StatsHomeWeeklyProvider contract. Do not add a second calculation to the theme.

Last Trophies Earned is a double-width, icon-only card with up to three latest
earned trophies across visible games, sorted by earned date then rarity for ties.
Icons are centred; tooltips contain game, achievement and earned date.
The feed reads Trophies' read-only source-filtered catalogue bridge. No sync or
network request is triggered by Stats. Both Home's latest-trophy card and Stats'
weekly/All Time trophy cards are unavailable without a loaded Trophies extension.

New Games is retired from weekly stats. Home's saved NewGames selections migrate
in memory to TrophiesEarned (the stable metric key), preserving other selections.
The metric includes Available and Trophies fields; Home must filter unavailable
metrics before rendering. Latest icons are global, not limited to this week.

Implementation remains in the legacy ThemeSource pending source consolidation;
deploy its compiled theme DLL to all three Development theme locations and
Views/MainWindow.xaml to the active theme. Preserve the Development Data profile.
