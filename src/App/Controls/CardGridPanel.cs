using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Trinkwasser.App.Controls;

/// <summary>
/// Karten in gleich breiten Spalten (so viele, wie mit <see cref="MinItemWidth"/> nebeneinander
/// passen). Jede Zeile ist so hoch wie ihre höchste Karte – anders als bei UniformGridLayout,
/// das alle Karten so hoch macht wie die erste und längere Texte abschneidet.
/// </summary>
public sealed partial class CardGridPanel : Panel
{
    public double MinItemWidth { get; set; } = 300;
    public double Spacing { get; set; } = 8;

    private int Columns(double width) =>
        double.IsInfinity(width) ? 1 : Math.Max(1, (int)((width + Spacing) / (MinItemWidth + Spacing)));

    private double ColumnWidth(double width, int columns) =>
        double.IsInfinity(width) ? MinItemWidth : Math.Max(0, (width - Spacing * (columns - 1)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns(availableSize.Width);
        var columnWidth = ColumnWidth(availableSize.Width, columns);
        double height = 0, rowHeight = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            if (i % columns == columns - 1 || i == Children.Count - 1)
            {
                height += rowHeight + (i < Children.Count - 1 ? Spacing : 0);
                rowHeight = 0;
            }
        }
        var width = double.IsInfinity(availableSize.Width) ? columnWidth * columns + Spacing * (columns - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var columnWidth = ColumnWidth(finalSize.Width, columns);
        double y = 0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            var end = Math.Min(start + columns, Children.Count);
            double rowHeight = 0;
            for (var i = start; i < end; i++) rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            for (var i = start; i < end; i++)
                Children[i].Arrange(new Rect((i - start) * (columnWidth + Spacing), y, columnWidth, rowHeight));
            y += rowHeight + Spacing;
        }
        return finalSize;
    }
}
