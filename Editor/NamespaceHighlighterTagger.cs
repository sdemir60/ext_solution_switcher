using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting; // ITextViewLine
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace SolutionSwitcher.Editor
{
    internal sealed class NamespaceSpan
    {
        public SnapshotSpan Span;
        public string Namespace;
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class NamespaceHighlighterProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer) return null;

            // IWpfTextView'e cast şart
            var wpfView = textView as IWpfTextView;
            if (wpfView == null) return null;

            return textView.Properties.GetOrCreateSingletonProperty(
                () => new NamespaceHighlighterTagger(wpfView)) as ITagger<T>;
        }
    }

    internal sealed class NamespaceHighlighterTagger : ITagger<TextMarkerTag>
    {
        private readonly IWpfTextView _view;
        private volatile bool _ctrlDown;
        private List<NamespaceSpan> _current = new();
        private static readonly Regex UsingRegex =
            new(@"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Compiled);

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public NamespaceHighlighterTagger(IWpfTextView view)
        {
            _view = view;
            _view.TextBuffer.Changed += (s, e) => { if (_ctrlDown) Recompute(); };

            _view.VisualElement.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
                {
                    _ctrlDown = true;
                    Recompute();
                }
            };
            _view.VisualElement.PreviewKeyUp += (s, e) =>
            {
                if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
                {
                    _ctrlDown = false;
                    Clear();
                }
            };

            _view.VisualElement.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_ctrlDown) return;

            var y = e.GetPosition(_view.VisualElement).Y;
            var line = _view.TextViewLines?.GetTextViewLineContainingYCoordinate(y);
            if (line == null) return;

            var x = e.GetPosition(_view.VisualElement).X;
            var point = line.GetBufferPositionFromXCoordinate(x);
            if (!point.HasValue) return;

            var pos = point.Value.Position;
            var hit = _current.FirstOrDefault(x => x.Span.Start.Position <= pos && pos <= x.Span.End.Position);
            if (hit == null) return;

            var records = Index.ProjectIndexService.Query(hit.Namespace);
            if (records.Count == 0) return;

            var solutionChoices = records
                .SelectMany(r => r.SolutionPaths.Select(sp => (solution: sp, project: r.ProjectPath)))
                .Distinct()
                .ToList();

            if (solutionChoices.Count == 1)
            {
                SolutionSwitcher.Shell.SolutionOpener.Open(solutionChoices[0].solution);
            }
            else
            {
                SolutionSwitcher.UI.PopupHelper.ShowSolutionPicker(
                    _view, e.GetPosition(_view.VisualElement),
                    solutionChoices.Select(x => x.solution).Distinct().ToList());
            }

            e.Handled = true;
        }

        private void Clear()
        {
            var snapshot = _view.TextSnapshot;
            _current = new List<NamespaceSpan>();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        private void Recompute()
        {
            var snapshot = _view.TextSnapshot;
            var list = new List<NamespaceSpan>();

            // 1) using satırları
            foreach (var line in snapshot.Lines.Take(300))
            {
                var text = line.GetText();
                var m = UsingRegex.Match(text);
                if (!m.Success) continue;

                var ns = m.Groups[1].Value;
                var hits = Index.ProjectIndexService.Query(ns);
                if (hits.Count == 0) continue;

                var nsStart = line.Start + text.IndexOf(ns, StringComparison.Ordinal);
                list.Add(new NamespaceSpan { Span = new SnapshotSpan(snapshot, nsStart, ns.Length), Namespace = ns });
            }

            // 2) Görünür satırlarda tam nitelikli adlar
            var visible = _view.TextViewLines?.Select(l => l.Extent).ToList() ?? new List<SnapshotSpan>();
            foreach (var extent in visible)
            {
                var text = extent.GetText();
                foreach (Match m in Regex.Matches(text, @"\b([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*){1,})\b"))
                {
                    var candidate = m.Groups[1].Value;
                    var hits = Index.ProjectIndexService.Query(candidate);
                    if (hits.Count == 0) continue;

                    var start = extent.Start + m.Index;
                    list.Add(new NamespaceSpan { Span = new SnapshotSpan(snapshot, start, m.Length), Namespace = candidate });
                }
            }

            _current = list;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!_ctrlDown || _current.Count == 0) yield break;

            foreach (var span in spans)
            {
                foreach (var s in _current)
                {
                    if (span.IntersectsWith(s.Span))
                        yield return new TagSpan<TextMarkerTag>(s.Span, new TextMarkerTag("SolutionSwitcher.Highlight"));
                }
            }
        }
    }
}
