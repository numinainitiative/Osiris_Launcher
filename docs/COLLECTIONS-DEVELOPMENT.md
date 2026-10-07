# Collection pages (Beta 0.0.51)

Click a collection name in Game Details -> Details to open its collection page.
The banner uses Home's actual hero layout: an 800 px-high, top-aligned image host,
top-aligned Uniform image layers, its 300 px bottom fade, the horizontal fade and
the full 800 px vertical fade. All gradient endpoints, colours and stops match
Home; there is no compact-height override or separately scaled gradient. The
collection title and game count replace the greeting at Home's 78/85 px anchor.
Member games' available static backgrounds cycle every 30 seconds, with Home's
900 ms crossfade. Missing banners leave a neutral background.

The virtualized cover grid overlays the lower hero at Home's 390 px content
anchor instead of being placed below a short banner. It uses Library's saved vertical column count,
cover proportions, spacing and rounded-corner resource. Covers open Game Details;
Back returns to the collection. Membership is the game's exact native Series ID,
not a name search or library filter. Hidden games remain excluded. Selected edition
names are included in cover titles. Other metadata fields remain passive text.

Included in Beta 0.0.51. Opening a collection does not modify
collection/library data. The page is loaded on demand and releases
its cover items, banner images, animations and timer when leaving. All art comes
from the existing local library; no new external fetch or account is needed.

Until theme source consolidation, editable files remain in
`Development/Contents and Stuff/Development (AI)/Development/ThemeSource`:

- `Code/OsirisCollectionPage.cs`: read-only membership, link behaviour and slideshow.
- `Views/CollectionPage.xaml`: banner and virtualized grid.
- `Code/OsirisSidebarBehavior.cs`: identity-aware Back history and page visibility.
- `Views/MainWindow.xaml`: collection page host.
- `Views/DetailsViewGameOverview.xaml`: collection links.
- `Code/Validation/CollectionPageValidation.csproj`: synthetic membership, XAML,
  navigation, slideshow and cleanup checks; no live profile writes.

Build `Code/OsirisTheme.csproj` with `EmbedRajdhaniFonts=true`. Development deployment
needs all three DLL copies (`App/OsirisTheme.dll`, the Default theme root, and its
`CustomControls` folder) plus the three affected/new views. The app-folder assembly
takes precedence during startup; updating only the theme copies is insufficient. Preserve
`Programming/Development/Osiris/Data`; restart through its root `Osiris.exe` wrapper.
