using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    internal static partial class GameEditions
    {
        internal static GameEditionChoice CreateNamedEdition(Guid id, string label)
        {
            label = (label ?? "").Trim();
            if (label.Length == 0) throw new ArgumentException("Enter an edition name.");
            GameEditionRecord added;
            lock (SyncRoot)
            {
                var game = API.Instance?.Database?.Games?.Get(id);
                var profile = GetProfile(id);
                if (game == null || GroupIsRunning(profile, game)) throw new InvalidOperationException("Stop the game before adding an edition.");
                if ((profile == null && label.Equals("Standard", StringComparison.OrdinalIgnoreCase)) ||
                    profile?.Editions.Any(e => label.Equals(e.Label, StringComparison.CurrentCultureIgnoreCase)) == true)
                    throw new ArgumentException("This game already has an edition with that name.");
                if (profile == null) profile = CreateIndependentGroup(game, "Standard");
                else EnsureIndependent(profile, game);
                BackupProfiles(profile);
                AddCatalogLabelCore(label);
                added = AddLocalProfile(profile, label, game.Name);
                Save();
            }
            RaiseChanged();
            return ToChoice(added);
        }
        internal static Guid ResolveEditionForRemoval(Guid id)
        {
            var current = API.Instance?.Database?.Games?.Get(id);
            if (current != null && (current.IsRunning || current.IsLaunching || current.IsInstalling || current.IsUninstalling)) return id;
            // A legacy overlay group still has only one database row. Convert it
            // with the usual recovery snapshot before resolving the selected
            // edition, otherwise native row removal would erase the whole group.
            return PrepareProfileForEdit(id);
        }
        internal static List<Game> ResolveEditionRemovalTargets(IEnumerable<Game> selected, bool active)
        {
            return (selected ?? Enumerable.Empty<Game>()).Where(g => g != null)
                .Select(g => API.Instance.Database.Games.Get(active ? ResolveEditionForRemoval(g.Id) : g.Id))
                .Where(g => g != null).GroupBy(g => g.Id).Select(g => g.First()).ToList();
        }
        internal static string GetRemovalTitle(Game game)
        {
            lock (SyncRoot)
            {
                var label = GetProfile(game.Id)?.Editions.FirstOrDefault(e => e.GameProfileId == game.Id)?.Label;
                return string.IsNullOrEmpty(label) ? game.Name : game.Name + " (" + label + ")";
            }
        }
    }

    public static partial class GameEditPageController
    {
        private sealed partial class GameEditPageState
        {
            private CheckBox mirrorEnabled;
            private ComboBox mirrorSource;
            private FrameworkElement mirrorControls;
            private System.Windows.Controls.Primitives.ButtonBase deleteEditionButton;
            private bool updatingMirrorControls;
            private readonly Dictionary<string, Guid> pendingMirrorCopies = new Dictionary<string, Guid>();
            private readonly Dictionary<System.Windows.Controls.Primitives.ButtonBase, FrameworkElement> mirrorBadges =
                new Dictionary<System.Windows.Controls.Primitives.ButtonBase, FrameworkElement>();
            private readonly HashSet<Guid> createdSessionEditions = new HashSet<Guid>();

            private void InitializeEditionControls()
            {
                mirrorEnabled = root.FindName("PART_OsirisGameEditMirrorEnabled") as CheckBox;
                mirrorSource = root.FindName("PART_OsirisGameEditMirrorSource") as ComboBox;
                mirrorControls = root.FindName("PART_OsirisGameEditMirrorControls") as FrameworkElement;
                deleteEditionButton = root.FindName("PART_OsirisGameEditDeleteEdition") as System.Windows.Controls.Primitives.ButtonBase;
                if (mirrorEnabled != null) { mirrorEnabled.Checked += OnMirrorChanged; mirrorEnabled.Unchecked += OnMirrorChanged; }
                if (mirrorSource != null) mirrorSource.SelectionChanged += OnMirrorSourceChanged;
                if (deleteEditionButton != null) deleteEditionButton.Click += OnDeleteEditionClick;
                foreach (var button in pages.Keys)
                {
                    if (mirrorBadges.ContainsKey(button) || (button as FrameworkElement)?.Name == "PART_OsirisGameEditDangerZone") continue;
                    var label = button.Content;
                    button.Content = null;
                    var grid = new Grid { ClipToBounds = false };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    if (label is UIElement element) grid.Children.Add(element);
                    else grid.Children.Add(new TextBlock { Text = Convert.ToString(label), VerticalAlignment = VerticalAlignment.Center });
                    var canvas = new Canvas { Width = 24, Height = 24 };
                    foreach (var data in new[] {
                        "M10,13 a5,5 0 0 0 7.54,0.54 l3,-3 a5,5 0 0 0 -7.07,-7.07 l-1.72,1.71",
                        "M14,11 a5,5 0 0 0 -7.54,-0.54 l-3,3 a5,5 0 0 0 7.07,7.07 l1.71,-1.71" })
                        canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Stroke = new SolidColorBrush(Color.FromRgb(78, 205, 128)),
                            StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
                    var badge = new Viewbox { Width = 18, Height = 18, Child = canvas, HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0),
                        Visibility = Visibility.Collapsed, IsHitTestVisible = false, Tag = "OsirisEditionMirrorBadge" };
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge); button.Content = grid; mirrorBadges[button] = badge;
                }
            }

            private void DisposeEditionControls()
            {
                if (mirrorEnabled != null) { mirrorEnabled.Checked -= OnMirrorChanged; mirrorEnabled.Unchecked -= OnMirrorChanged; }
                if (mirrorSource != null) mirrorSource.SelectionChanged -= OnMirrorSourceChanged;
                if (deleteEditionButton != null) deleteEditionButton.Click -= OnDeleteEditionClick;
                foreach (var entry in mirrorBadges)
                {
                    var grid = entry.Key.Content as Grid;
                    if (grid?.Children.Count > 0) { var label = grid.Children[0]; grid.Children.Remove(label); entry.Key.Content = label; }
                }
                mirrorBadges.Clear();
            }
            private Guid? EffectiveMirrorSource(string page)
            {
                if (game == null || page == null) return null;
                var source = pendingPageSharing.TryGetValue(page, out var pending) ? pending : GameEditions.GetPageMirrorSource(game.Id, page);
                if (source.HasValue && API.Instance.Database.Games.Get(source.Value) == null)
                {
                    pendingPageSharing[page] = null;
                    return null;
                }
                return source;
            }

            private void UpdateMirrorControls()
            {
                if (game == null) return;
                var page = CurrentCopyPage(); var supported = GameEditions.CanSharePage(page);
                var source = EffectiveMirrorSource(page);
                updatingMirrorControls = true;
                try
                {
                    var validSourceCount = 0;
                    if (mirrorControls != null) mirrorControls.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
                    if (mirrorSource != null)
                    {
                        var previous = mirrorSource.SelectedItem as GameEditionChoice;
                        var choices = editionChoices.Where(e => e.EditionId != activeEditionId && CanChooseMirrorSource(e, page)).ToList();
                        validSourceCount = choices.Count;
                        mirrorSource.ItemsSource = choices;
                        mirrorSource.SelectedItem = choices.FirstOrDefault(e => GameEditions.GetEditionProfile(game.Id, e.EditionId)?.Id == source) ??
                            choices.FirstOrDefault(e => e.EditionId == previous?.EditionId) ?? choices.FirstOrDefault();
                        mirrorSource.IsEnabled = supported && source.HasValue && choices.Count > 0;
                    }
                    if (mirrorEnabled != null) { mirrorEnabled.IsEnabled = supported && (validSourceCount > 0 || source.HasValue); mirrorEnabled.IsChecked = source.HasValue; }
                    if (copyEditionPageButton != null) copyEditionPageButton.IsEnabled = !source.HasValue && editionChoices.Count > 1;
                    if (deleteEditionButton != null) deleteEditionButton.IsEnabled = true;
                    if (resetStatsButton != null) resetStatsButton.IsEnabled = !EffectiveMirrorSource("Advanced").HasValue;
                    foreach (var entry in pages)
                    {
                        var name = (entry.Key as FrameworkElement)?.Name;
                        var key = name != null && name.StartsWith("PART_OsirisGameEdit", StringComparison.Ordinal) ? name.Substring("PART_OsirisGameEdit".Length) : null;
                        var mirrored = key != null && EffectiveMirrorSource(key).HasValue;
                        entry.Value.IsEnabled = !mirrored;
                        if (mirrorBadges.TryGetValue(entry.Key, out var badge))
                        {
                            badge.Visibility = mirrored ? Visibility.Visible : Visibility.Collapsed;
                            badge.ToolTip = mirrored ? "This page mirrors another edition." : null;
                        }
                    }
                }
                finally { updatingMirrorControls = false; }
            }
            private bool CanChooseMirrorSource(GameEditionChoice choice, string page)
            {
                var saved = GameEditions.GetEditionProfile(game.Id, choice.EditionId);
                if (saved == null) return false;
                var seen = new HashSet<Guid> { game.Id }; Guid? id = saved.Id;
                while (id.HasValue)
                {
                    if (!seen.Add(id.Value)) return false;
                    id = GameEditions.GetPageMirrorSource(id.Value, page);
                }
                return true;
            }
            private void OnMirrorChanged(object sender, RoutedEventArgs args)
            {
                if (updatingMirrorControls || restoringEditionDraft || game == null) return;
                if (mirrorEnabled?.IsChecked != true)
                {
                    var previousSource = EffectiveMirrorSource(CurrentCopyPage());
                    if (CurrentCopyPage() == "Trophies") TrophiesGameEditor.Load(root, game.Id,
                        GameEditions.GetSharedPageOwner(previousSource ?? game.Id, "Trophies"));
                    if (previousSource.HasValue) pendingMirrorCopies[CurrentCopyPage()] = GameEditions.GetSharedPageOwner(previousSource.Value, CurrentCopyPage());
                    pendingPageSharing[CurrentCopyPage()] = null;
                    UpdateMirrorControls();
                }
                else StagePageMirror();
            }
            private void OnMirrorSourceChanged(object sender, SelectionChangedEventArgs args)
            {
                if (!updatingMirrorControls && !restoringEditionDraft && mirrorEnabled?.IsChecked == true) StagePageMirror();
            }
            private void StagePageMirror()
            {
                var page = CurrentCopyPage(); var choice = mirrorSource?.SelectedItem as GameEditionChoice;
                if (game == null || choice == null || !GameEditions.CanSharePage(page)) { UpdateMirrorControls(); return; }
                try
                {
                    var source = GameEditions.GetEditionProfile(game.Id, choice.EditionId);
                    if (source == null) throw new InvalidOperationException("That edition is no longer available.");
                    var seen = new HashSet<Guid> { game.Id }; Guid? next = source.Id;
                    while (next.HasValue)
                    {
                        if (!seen.Add(next.Value)) throw new InvalidOperationException("These editions would mirror each other in a loop. Choose another source.");
                        next = GameEditions.GetPageMirrorSource(next.Value, page);
                    }
                    var owner = API.Instance.Database.Games.Get(GameEditions.GetSharedPageOwner(source.Id, page));
                    switch (page)
                    {
                        case "Trophies": TrophiesGameEditor.Load(root, game.Id, owner.Id); break;
                        case "Gallery": DiscardLocalGalleryChanges(); LoadGallerySettings(owner.Id); break;
                        case "Screenshots": ScreenshotsGameEditor.LoadFromStorage(root, game.Id, API.Instance.Database.GetFileStoragePath(owner.Id)); break;
                        default: CopyEditionPage(owner.GetCopy(), page); break;
                    }
                    pendingMirrorCopies[page] = owner.Id;
                    pendingPageSharing[page] = source.Id;
                }
                catch (Exception error) { API.Instance.Dialogs.ShowErrorMessage(error.Message, "Unable to mirror page"); }
                UpdateMirrorControls();
            }

            private void OnDeleteEditionClick(object sender, RoutedEventArgs args)
            {
                args.Handled = true;
                if (game == null || !GameContextMenuBehavior.RemoveEditionProfiles(Window.GetWindow(root), new[] { game }, false)) return;
                var removed = game.Id;
                createdSessionEditions.Remove(removed);
                if (editionDrafts.TryGetValue(removed, out var draft)) { ReleaseEditionDraft(draft); editionDrafts.Remove(removed); }
                DiscardStagedMediaChanges(); DiscardLocalGalleryChanges(); CloseNativeEditModel();
                var next = GameEditions.GetActiveProfileId(removed);
                if (API.Instance.Database.Games.Get(next) == null)
                {
                    DiscardEditionSession(); game = null; stagedGame = null; isNewGame = false;
                    HeaderNavigation.NavigateHome(HeaderNavigation.FindViewModel(root)); return;
                }
                if (editionDrafts.TryGetValue(next, out var kept)) { RestoreEditionDraft(kept); editionDrafts.Remove(next); }
                else { switchingEditionDraft = true; try { LoadGame(); } finally { switchingEditionDraft = false; } }
                LoadEditionSettings(); UpdateEditionHeader();
            }
        }
    }
}
