using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace SolutionSwitcher.Editor
{
    [Export(typeof(EditorFormatDefinition))]
    [Name("SolutionSwitcher.Highlight")]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class HighlightFormatDefinition : MarkerFormatDefinition
    {
        public HighlightFormatDefinition()
        {
            // #F59F00 sınır, açık ton arkaplan
            Border = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59F00")), 1.0);
            BackgroundColor = (Color)ColorConverter.ConvertFromString("#FFF3D6"); // açık ton
            DisplayName = "Solution Switcher Namespace Highlight";
            ZOrder = 10;
        }
    }
}
