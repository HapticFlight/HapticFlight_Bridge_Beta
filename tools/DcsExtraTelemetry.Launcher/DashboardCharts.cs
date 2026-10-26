using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DcsExtraTelemetry.Launcher
{
    // Bar chart for the dashboard: grey bars with the largest (or a chosen) bar picked out
    // in a highlight colour, faint horizontal gridlines and optional axis labels.
    internal sealed class DashboardBarChart : FrameworkElement
    {
        private IList<double> values = new double[0];
        private IList<string> labels = new string[0];

        // Resource keys, so the chart follows the palette.
        public string BarBrushKey { get; set; } = "ChartBar";
        public string HighlightBrushKey { get; set; } = "ChartBarHighlight";
        public bool ShowValueAxis { get; set; } = true;
        public Func<double, string> ValueFormatter { get; set; } = value => value.ToString("0", CultureInfo.CurrentCulture);

        // -1 highlights the largest value.
        public int HighlightIndex { get; set; } = -1;

        public int ResolvedHighlightIndex
        {
            get
            {
                if (HighlightIndex >= 0 && HighlightIndex < values.Count) return HighlightIndex;
                if (values.Count == 0 || values.Max() <= 0) return -1;
                return values.IndexOf(values.Max());
            }
        }

        public void SetData(IList<double> barValues, IList<string> barLabels)
        {
            values = barValues ?? new double[0];
            labels = barLabels ?? new string[0];
            InvalidateVisual();
        }

        public void RefreshTheme() { InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            Brush gridBrush = new SolidColorBrush(RecordingChartPalette.ResourceColor("ChartGridMinor"));
            Brush labelBrush = new SolidColorBrush(RecordingChartPalette.ResourceColor("TextMuted"));
            Brush strongLabelBrush = new SolidColorBrush(RecordingChartPalette.ResourceColor("TextPrimary"));
            Brush barBrush = new SolidColorBrush(RecordingChartPalette.ResourceColor(BarBrushKey));
            Brush highlightBrush = new SolidColorBrush(RecordingChartPalette.ResourceColor(HighlightBrushKey));
            var gridPen = new Pen(gridBrush, 1);
            Typeface typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Typeface boldTypeface = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            bool hasLabels = labels.Count > 0;
            double bottomAxis = hasLabels ? 18 : 0;
            double leftAxis = ShowValueAxis ? 34 : 0;
            double plotTop = 6;
            double plotHeight = Math.Max(1, height - bottomAxis - plotTop);
            double plotWidth = Math.Max(1, width - leftAxis);
            double max = values.Count == 0 ? 0 : values.Max();
            double scaleMax = NiceCeiling(max);

            const int gridLines = 4;
            for (int line = 0; line <= gridLines; line++)
            {
                double y = Math.Round(plotTop + plotHeight * line / gridLines) + .5;
                dc.DrawLine(gridPen, new Point(leftAxis, y), new Point(width, y));
                if (ShowValueAxis && scaleMax > 0)
                {
                    double axisValue = scaleMax * (gridLines - line) / gridLines;
                    var text = new FormattedText(ValueFormatter(axisValue), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        typeface, 9.5, labelBrush, pixelsPerDip);
                    dc.DrawText(text, new Point(0, y - text.Height / 2));
                }
            }

            if (values.Count == 0) return;
            double slot = plotWidth / values.Count;
            double barWidth = Math.Max(3, Math.Min(34, slot * .58));
            int highlight = ResolvedHighlightIndex;
            for (int index = 0; index < values.Count; index++)
            {
                double value = Math.Max(0, values[index]);
                double barHeight = scaleMax <= 0 ? 0 : plotHeight * value / scaleMax;
                double x = leftAxis + slot * index + (slot - barWidth) / 2;
                if (barHeight > 0)
                {
                    double radius = Math.Min(3, barWidth / 2);
                    dc.DrawRoundedRectangle(index == highlight ? highlightBrush : barBrush, null,
                        new Rect(x, plotTop + plotHeight - barHeight, barWidth, barHeight), radius, radius);
                }

                if (hasLabels && index < labels.Count && !string.IsNullOrEmpty(labels[index]))
                {
                    var text = new FormattedText(labels[index], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        index == highlight ? boldTypeface : typeface, 9.5, index == highlight ? strongLabelBrush : labelBrush, pixelsPerDip);
                    double labelX = x + barWidth / 2 - text.Width / 2;
                    labelX = Math.Max(leftAxis, Math.Min(width - text.Width, labelX));
                    dc.DrawText(text, new Point(labelX, height - text.Height));
                }
            }
        }

        private static double NiceCeiling(double value)
        {
            if (value <= 0) return 0;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (double step in new[] { 1, 2, 4, 6, 8, 10 })
            {
                if (step * magnitude >= value) return step * magnitude;
            }
            return 10 * magnitude;
        }
    }

    internal sealed class DonutSlice
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string BrushKey { get; set; }
    }

    // Donut for category shares. Slices are drawn clockwise from 12 o'clock with a small gap.
    internal sealed class DashboardDonutChart : FrameworkElement
    {
        private IList<DonutSlice> slices = new List<DonutSlice>();

        public double Thickness { get; set; } = 17;

        public void SetSlices(IList<DonutSlice> donutSlices)
        {
            slices = donutSlices ?? new List<DonutSlice>();
            InvalidateVisual();
        }

        public void RefreshTheme() { InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            double radius = size / 2 - Thickness / 2;
            double total = slices.Sum(slice => Math.Max(0, slice.Value));

            if (total <= 0)
            {
                var emptyPen = new Pen(new SolidColorBrush(RecordingChartPalette.ResourceColor("ChartLane")), Thickness);
                dc.DrawEllipse(null, emptyPen, center, radius, radius);
                return;
            }

            const double gapDegrees = 2.5;
            double startDegrees = 0;
            foreach (DonutSlice slice in slices)
            {
                double value = Math.Max(0, slice.Value);
                if (value <= 0) continue;
                double sweep = value / total * 360;
                double drawnSweep = Math.Max(.5, sweep - (slices.Count(s => s.Value > 0) > 1 ? gapDegrees : 0));
                var pen = new Pen(new SolidColorBrush(RecordingChartPalette.ResourceColor(slice.BrushKey)), Thickness)
                {
                    StartLineCap = PenLineCap.Flat,
                    EndLineCap = PenLineCap.Flat
                };
                if (drawnSweep >= 359.9)
                    dc.DrawEllipse(null, pen, center, radius, radius);
                else
                    dc.DrawGeometry(null, pen, Arc(center, radius, startDegrees, drawnSweep));
                startDegrees += sweep;
            }
        }

        private static Geometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
        {
            Point start = PointOnCircle(center, radius, startDegrees);
            Point end = PointOnCircle(center, radius, startDegrees + sweepDegrees);
            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Point PointOnCircle(Point center, double radius, double degrees)
        {
            double radians = (degrees - 90) * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }
    }
}
