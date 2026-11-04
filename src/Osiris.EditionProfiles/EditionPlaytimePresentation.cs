using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Playnite;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    // The hero's total follows Advanced independently of the Exophase card.
    // Reading a linked card must never substitute another edition's baseline.
    public sealed class EditionPlaytimePresentation : FrameworkElement
    {
        public static readonly DependencyProperty UseTotalPlaytimeProperty = DependencyProperty.Register(
            nameof(UseTotalPlaytime), typeof(bool), typeof(EditionPlaytimePresentation), new PropertyMetadata(false));
        public static readonly DependencyProperty DisplayPlaytimeTextProperty = DependencyProperty.Register(
            nameof(DisplayPlaytimeText), typeof(string), typeof(EditionPlaytimePresentation), new PropertyMetadata(""));
        public static readonly DependencyProperty DisplayPlaytimeLabelProperty = DependencyProperty.Register(
            nameof(DisplayPlaytimeLabel), typeof(string), typeof(EditionPlaytimePresentation), new PropertyMetadata("TOTAL TIME PLAYED"));
        public bool UseTotalPlaytime => (bool)GetValue(UseTotalPlaytimeProperty);
        public string DisplayPlaytimeText => (string)GetValue(DisplayPlaytimeTextProperty);
        public string DisplayPlaytimeLabel => (string)GetValue(DisplayPlaytimeLabelProperty);
        private readonly DispatcherTimer refresh;

        public EditionPlaytimePresentation()
        {
            refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            refresh.Tick += (_, __) => Refresh();
            Loaded += (_, __) => { GameEditions.Changed += QueueRefresh; refresh.Start(); Refresh(); };
            Unloaded += (_, __) => { GameEditions.Changed -= QueueRefresh; refresh.Stop(); };
            DataContextChanged += (_, __) => Refresh();
        }

        private void QueueRefresh()
        {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(Refresh), DispatcherPriority.DataBind);
        }

        internal static Game ResolveAdvancedGame(Guid id) => API.Instance?.Database?.Games?.Get(GameEditions.GetSharedPageOwner(id, "Advanced"));

        internal static ulong ReadDisplayedTime(Game game, Func<Guid, ulong> importedTime)
        {
            if (game == null) return 0;
            var owner = ResolveAdvancedGame(game.Id) ?? game;
            try { return importedTime == null ? owner.Playtime : importedTime(owner.Id); }
            catch { return owner.Playtime; }
        }

        private void Refresh()
        {
            var id = GameEditionDetailsController.ResolveGameId(DataContext);
            var game = id.HasValue ? ResolveAdvancedGame(id.Value) : null;
            var seconds = ReadDisplayedTime(game, owner => ExophaseRuntimeBridge.IsLoaded
                ? ExophaseRuntimeBridge.GetEffectivePlaytime(owner) : game.Playtime);
            SetValue(UseTotalPlaytimeProperty, game != null && seconds > game.Playtime);
            SetValue(DisplayPlaytimeTextProperty, string.Format(CultureInfo.CurrentCulture, "{0}h {1:00}m", seconds / 3600, seconds / 60 % 60));
        }
    }
}
