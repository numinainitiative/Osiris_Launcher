using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OsirisTheme
{
    // In-memory editor ownership, never serialization of a live profile. The
    // caller explicitly lists draft fields; UI services/handlers stay attached.
    internal sealed class EditorDraftSnapshot
    {
        private readonly object owner;
        private readonly List<Action> restoreFields = new List<Action>();
        private readonly List<Action> restoreInputs = new List<Action>();
        internal bool HasValidationErrors { get; private set; }
        private EditorDraftSnapshot(object owner) { this.owner = owner; }

        internal static EditorDraftSnapshot Capture(object owner, IEnumerable<string> names, FrameworkElement root = null)
        {
            var snapshot = new EditorDraftSnapshot(owner);
            foreach (var name in names)
            {
                var field = owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) throw new InvalidOperationException("Missing editor draft field: " + name);
                var value = field.GetValue(owner);
                if (value is IDictionary dictionary)
                {
                    var entries = new List<DictionaryEntry>();
                    var iterator = dictionary.GetEnumerator();
                    while (iterator.MoveNext()) entries.Add(iterator.Entry);
                    snapshot.restoreFields.Add(() => { if (!field.IsInitOnly) field.SetValue(owner, value); dictionary.Clear(); foreach (var entry in entries) dictionary.Add(entry.Key, entry.Value); });
                }
                else if (value is IList list)
                {
                    var entries = list.Cast<object>().ToArray();
                    snapshot.restoreFields.Add(() => { if (!field.IsInitOnly) field.SetValue(owner, value); list.Clear(); foreach (var entry in entries) list.Add(entry); });
                }
                else if (value is ICollection<string> strings)
                {
                    var entries = strings.ToArray();
                    snapshot.restoreFields.Add(() => { if (!field.IsInitOnly) field.SetValue(owner, value); strings.Clear(); foreach (var entry in entries) strings.Add(entry); });
                }
                else snapshot.restoreFields.Add(() => field.SetValue(owner, value));
            }
            if (root != null)
            {
                foreach (var control in Descendants(root).OfType<FrameworkElement>())
                {
                    snapshot.HasValidationErrors |= Validation.GetHasError(control);
                    if (!control.Name.StartsWith("PART_OsirisGameEdit", StringComparison.Ordinal) ||
                        control.Name == "PART_OsirisGameEditEdition") continue;
                    if (control is TextBox text) snapshot.Remember(text, TextBox.TextProperty);
                    if (control is ToggleButton toggle) snapshot.Remember(toggle, ToggleButton.IsCheckedProperty);
                    if (control is ComboBox combo) snapshot.Remember(combo, Selector.SelectedItemProperty);
                    if (control is DatePicker date) snapshot.Remember(date, DatePicker.SelectedDateProperty);
                    if (control is TextBlock status) snapshot.Remember(status, TextBlock.TextProperty);
                    if (control is TextBox || control is ToggleButton || control is ComboBox)
                        snapshot.Remember(control, FrameworkElement.TagProperty);
                }
            }
            return snapshot;
        }

        private void Remember(DependencyObject control, DependencyProperty property)
        {
            var value = control.GetValue(property);
            restoreInputs.Add(() => control.SetCurrentValue(property, value));
        }
        internal void RestoreFields() { foreach (var restore in restoreFields) restore(); }
        internal void RestoreInputs() { foreach (var restore in restoreInputs) restore(); }

        internal static void FlushInputs(FrameworkElement root)
        {
            foreach (var control in Descendants(root))
            {
                if (control is TextBox text) text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                if (control is DatePicker date) date.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
            while (pending.Count > 0)
            {
                var item = pending.Pop(); if (!seen.Add(item)) continue;
                yield return item;
                foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) pending.Push(child);
                if (item is Visual || item is System.Windows.Media.Media3D.Visual3D)
                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++) pending.Push(VisualTreeHelper.GetChild(item, i));
            }
        }
    }
}
