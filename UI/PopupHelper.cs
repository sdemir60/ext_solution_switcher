using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SolutionSwitcher.UI
{
    internal static class PopupHelper
    {
        public static void ShowSolutionPicker(ITextView view, Point relativeToView, List<string> solutionPaths)
        {
            var popup = new ContextMenu();
            foreach (var sln in solutionPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var mi = new MenuItem { Header = System.IO.Path.GetFileName(sln), Tag = sln };
                mi.Click += (s, e) => Shell.SolutionOpener.Open((string)((MenuItem)s).Tag);
                popup.Items.Add(mi);
            }
            popup.IsOpen = true;
        }
    }
}
