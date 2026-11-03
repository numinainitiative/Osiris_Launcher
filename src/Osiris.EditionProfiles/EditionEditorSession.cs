using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace OsirisTheme
{
    public static partial class GameEditPageController
    {
        private sealed partial class GameEditPageState
        {
            private readonly Dictionary<Guid, EditionDraft> editionDrafts = new Dictionary<Guid, EditionDraft>();
            private bool switchingEditionDraft;
            private bool restoringEditionDraft;
            private int editionDraftVersion;
            private sealed class EditionDraft
            {
                internal Guid Id;
                internal Game Game;
                internal object Model;
                internal EditorDraftSnapshot Fields;
                internal EditorDraftSnapshot Cover;
                internal EditorDraftSnapshot Hero;
                internal Action Screenshots;
                internal object TrophiesContent;
                internal object TrophiesTag;
                internal string[] TemporaryFiles;
                internal bool Invalid;
                internal Dictionary<string, Guid?> MirrorChanges;
            }

            private static readonly string[] EditionDraftFields = {
                "game", "stagedGame", "nativeEditModel", "nativeEditModelClosed", "nativeEditModelError", "isNewGame", "activeEditionId",
                "pendingPageSharing", "pendingMirrorCopies", "stagedEditionLabels", "mediaLoaded", "stagedArchiveSizeBytes",
                "originalLogoPaths", "stagedLogoPaths", "temporaryLogoPaths", "originalIconPaths", "stagedIconPaths", "temporaryIconPaths",
                "selectedLogoIndex", "originalSelectedLogoIndex", "selectedIconIndex", "originalSelectedIconIndex",
                "addedLocalGalleryPaths", "removedLocalGalleryPaths", "localGalleryItems",
                "selectedSteamStoreAppId", "selectedSteamStoreGameName", "selectedSteamStoreAppIdConfirmed",
                "howLongToBeatMatches", "exophasePlatforms", "exophaseMatches", "exophaseSelectedMatches", "exophaseAutomaticMatch", "exophasePlatformChoices",
                "manuallySelectedSteamGridDbGameId", "manuallySelectedSteamGridDbGameName", "cachedSteamGridDbGameName", "cachedSteamGridDbGameIds",
                "originalLibraryBindings", "stagedLibraryImports", "libraryBindingSnapshotGameId", "libraryBindingsDirty", "libraryBindingsCommitted"
            };
            private static readonly string[] ArtworkDraftFields = { "OriginalPaths", "StagedPaths", "TemporaryPaths", "SelectedIndex", "OriginalSelectedIndex" };

            private EditionDraft CaptureEditionDraft()
            {
                EditorDraftSnapshot.FlushInputs(root);
                if (stagedGame != null)
                {
                    if (nameTextBox != null) stagedGame.Name = nameTextBox.Text;
                    if (sortingNameTextBox != null) stagedGame.SortingName = sortingNameTextBox.Text;
                    ApplyInstallationFields();
                }
                var fields = EditorDraftSnapshot.Capture(this, EditionDraftFields, root);
                var trophies = root.FindName("PART_OsirisGameEditTrophiesHost") as ContentControl;
                return new EditionDraft {
                    Id = game.Id, Game = stagedGame, Model = nativeEditModel, Fields = fields,
                    Cover = coverGallery == null ? null : EditorDraftSnapshot.Capture(coverGallery, ArtworkDraftFields),
                    Hero = heroGallery == null ? null : EditorDraftSnapshot.Capture(heroGallery, ArtworkDraftFields),
                    Screenshots = ScreenshotsGameEditor.CaptureDraft(root),
                    TrophiesContent = trophies?.Content, TrophiesTag = trophies?.Tag,
                    TemporaryFiles = temporaryLogoPaths.Concat(temporaryIconPaths).Concat(addedLocalGalleryPaths)
                        .Concat(coverGallery?.TemporaryPaths ?? Enumerable.Empty<string>()).Concat(heroGallery?.TemporaryPaths ?? Enumerable.Empty<string>()).Distinct().ToArray(),
                    Invalid = fields.HasValidationErrors || string.IsNullOrWhiteSpace(stagedGame?.Name),
                    MirrorChanges = new Dictionary<string, Guid?>(pendingPageSharing)
                };
            }

            private void SuspendEditionDraft()
            {
                editionDraftVersion++;
                artworkPickerLoadVersion++; steamStoreGalleryLoadVersion++;
                CancelArtworkPickerLoad(); CancelHowLongToBeatSearch(); CloseArtworkPickerSurface();
                // Transfer ownership to the session. Fresh-page loading must not
                // delete another edition's staged images or close its native VM.
                temporaryLogoPaths.Clear(); temporaryIconPaths.Clear(); addedLocalGalleryPaths.Clear(); removedLocalGalleryPaths.Clear();
                coverGallery?.TemporaryPaths.Clear(); heroGallery?.TemporaryPaths.Clear();
                nativeEditModel = null;
            }

            private void RestoreEditionDraft(EditionDraft draft)
            {
                restoringEditionDraft = true;
                loadingLocalStorageSettings = true;
                try
                {
                    Select(generalButton);
                    draft.Fields.RestoreFields(); draft.Cover?.RestoreFields(); draft.Hero?.RestoreFields();
                    foreach (var page in pages.Values) page.DataContext = nativeEditModel;
                    draft.Screenshots?.Invoke();
                    var trophies = root.FindName("PART_OsirisGameEditTrophiesHost") as ContentControl;
                    if (trophies != null) { trophies.Content = draft.TrophiesContent; trophies.Tag = draft.TrophiesTag; }
                    draft.Fields.RestoreInputs();
                    if (localStorageEnabledCheckBox != null) localStorageEnabledCheckBox.IsEnabled = GameEditions.SupportsLocalStorage(game);
                    if (mediaLoaded) { RefreshLogoPreviews(); RefreshIconPreviews(); RefreshArtworkGallery(coverGallery); RefreshArtworkGallery(heroGallery); }
                    steamStoreGalleryLoaded = false; steamStoreGalleryItems.Clear();
                    UpdateGallerySourceCardVisibility(); UpdateEditionHeader(); QueueActionEditorRestyle();
                }
                finally { restoringEditionDraft = false; loadingLocalStorageSettings = false; }
            }

            private bool SaveEditionSession()
            {
                if (game == null) return false;
                var current = CaptureEditionDraft(); editionDrafts[current.Id] = current;
                var labels = stagedEditionLabels.ToArray();
                // Validate every draft before committing any edition.
                var invalid = editionDrafts.Values.FirstOrDefault(d => d.Invalid);
                if (invalid != null)
                {
                    SuspendEditionDraft(); RestoreEditionDraft(invalid); editionDrafts.Remove(invalid.Id);
                    GameEditions.SwitchEdition(game.Id, activeEditionId);
                    API.Instance.Dialogs.ShowErrorMessage("Please correct the invalid fields in this edition before saving.", "Invalid game data");
                    return false;
                }
                foreach (var draft in editionDrafts.Values)
                    foreach (var change in draft.MirrorChanges.Where(c => c.Value.HasValue && API.Instance.Database.Games.Get(c.Value.Value) == null).ToList())
                        draft.MirrorChanges[change.Key] = null;
                try { GameEditions.ValidateMirrorDrafts(editionDrafts.Values.ToDictionary(d => d.Id, d => d.MirrorChanges)); }
                catch (Exception error) { editionDrafts.Remove(current.Id); API.Instance.Dialogs.ShowErrorMessage(error.Message, "Invalid page mirror"); return false; }
                foreach (var draft in editionDrafts.Values.OrderBy(d => d.Id == current.Id ? 1 : 0).ToList())
                {
                    if (API.Instance.Database.Games.Get(draft.Id) == null) { ReleaseEditionDraft(draft); editionDrafts.Remove(draft.Id); continue; }
                    SuspendEditionDraft(); RestoreEditionDraft(draft);
                    stagedEditionLabels.Clear(); foreach (var label in labels) stagedEditionLabels.Add(label);
                    if (!SaveFields()) { editionDrafts.Remove(draft.Id); return false; }
                    editionDrafts.Remove(draft.Id);
                }
                createdSessionEditions.Clear();
                return true;
            }

            private void ReleaseEditionDraft(EditionDraft draft)
            {
                foreach (var path in draft.TemporaryFiles) DeleteFileSafe(path);
                if (draft.Model == null || ReferenceEquals(draft.Model, nativeEditModel)) return;
                try { draft.Model.GetType().GetMethod("CloseView", new[] { typeof(bool), typeof(bool) })?.Invoke(draft.Model, new object[] { false, false }); }
                catch (Exception error) { System.Diagnostics.Debug.WriteLine("Closing edition draft: " + error); }
            }
            private void DiscardEditionSession()
            {
                foreach (var draft in editionDrafts.Values) ReleaseEditionDraft(draft);
                editionDrafts.Clear();
                foreach (var id in createdSessionEditions.ToList())
                {
                    var added = API.Instance?.Database?.Games?.Get(id);
                    if (added == null || added.IsRunning) continue;
                    var editions = GameEditions.GetEditions(id);
                    var choice = editions.FirstOrDefault(e => GameEditions.GetEditionProfile(id, e.EditionId)?.Id == id);
                    if (editions.Count > 1 && choice != null)
                        GameEditions.RemoveEdition(id, choice.EditionId, editions.First(e => e.EditionId != choice.EditionId).EditionId);
                    else { GameEditions.RemoveProfile(id); API.Instance.Database.Games.Remove(added); }
                }
                createdSessionEditions.Clear();
            }
        }
    }
}
