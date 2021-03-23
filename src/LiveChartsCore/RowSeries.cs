﻿// The MIT License(MIT)

// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore.Kernel;
using LiveChartsCore.Drawing;
using System;
using LiveChartsCore.Measure;

namespace LiveChartsCore
{
    public class RowSeries<TModel, TVisual, TLabel, TDrawingContext> : BarSeries<TModel, TVisual, TLabel, TDrawingContext>
        where TVisual : class, ISizedVisualChartPoint<TDrawingContext>, new()
        where TLabel : class, ILabelGeometry<TDrawingContext>, new()
        where TDrawingContext : DrawingContext
    {
        public RowSeries()
            : base(SeriesProperties.Bar | SeriesProperties.HorizontalOrientation) { }

        public override void Measure(
           CartesianChart<TDrawingContext> chart, IAxis<TDrawingContext> secondaryAxis, IAxis<TDrawingContext> primaryAxis)
        {
            var drawLocation = chart.DrawMaringLocation;
            var drawMarginSize = chart.DrawMarginSize;
            var secondaryScale = new Scaler(
                drawLocation, drawMarginSize, primaryAxis.Orientation, primaryAxis.DataBounds, primaryAxis.IsInverted);
            var primaryScale = new Scaler(
                drawLocation, drawMarginSize, secondaryAxis.Orientation, secondaryAxis.DataBounds, secondaryAxis.IsInverted);

            float uw = secondaryScale.ToPixels(1f) - secondaryScale.ToPixels(0f);
            float uwm = 0.5f * uw;
            float sw = Stroke?.StrokeThickness ?? 0;
            float p = primaryScale.ToPixels(unchecked((float)Pivot));

            var pos = chart.SeriesContext.GetColumnPostion(this);
            var count = chart.SeriesContext.GetColumnSeriesCount();
            float cp = 0f;

            if (!IgnoresBarPosition && count > 1)
            {
                uw = uw / count;
                uwm = 0.5f * uw;
                cp = (pos - count / 2f) * uw + uwm;
            }

            if (uw < -1*MaxBarWidth)
            {
                uw = unchecked((float)MaxBarWidth*-1);
                uwm = uw / 2f;
            }

            if (Fill != null) chart.Canvas.AddDrawableTask(Fill);
            if (Stroke != null) chart.Canvas.AddDrawableTask(Stroke);
            if (DataLabelsDrawableTask != null) chart.Canvas.AddDrawableTask(DataLabelsDrawableTask);
            var dls = unchecked((float)DataLabelsSize);

            foreach (var point in Fetch(chart))
            {
                var visual = point.Context.Visual as TVisual;
                var primary = primaryScale.ToPixels(point.PrimaryValue);
                var secondary = secondaryScale.ToPixels(point.SecondaryValue);
                float b = Math.Abs(primary - p);

                if (point.IsNull)
                {
                    if (visual != null)
                    {
                        visual.X = p;
                        visual.Y = secondary - uwm + cp;
                        visual.Width = 0;
                        visual.Height = uw;
                        visual.RemoveOnCompleted = true;
                        point.Context.Visual = null;
                    }
                    continue;
                }

                if (visual == null)
                {
                    var r = new TVisual
                    {
                        X = p,
                        Y = secondary - uwm + cp,
                        Width = 0,
                        Height = uw
                    };

                    visual = r;
                    point.Context.Visual = visual;
                    OnPointCreated(point);
                    r.CompleteAllTransitions();

                    if (Fill != null) Fill.AddGeometyToPaintTask(r);
                    if (Stroke != null) Stroke.AddGeometyToPaintTask(r);
                }

                var sizedGeometry = visual;

                var cx = point.PrimaryValue > Pivot ? primary - b : primary;
                var y = secondary - uwm + cp;

                sizedGeometry.X = cx;
                sizedGeometry.Y = y;
                sizedGeometry.Width = b;
                sizedGeometry.Height = uw;

                point.Context.HoverArea = new RectangleHoverArea().SetDimensions(primary, secondary - uwm + cp, b, uw);

                OnPointMeasured(point);
                chart.MeasuredDrawables.Add(sizedGeometry);

                if (DataLabelsDrawableTask != null)
                {
                    var label = point.Context.Label as TLabel;

                    if (label == null)
                    {
                        var l = new TLabel { X = p, Y = secondary - uwm + cp };

                        l.TransitionateProperties(nameof(l.X), nameof(l.Y))
                            .WithAnimation(a =>
                                a.WithDuration(chart.AnimationsSpeed)
                                .WithEasingFunction(chart.EasingFunction));

                        l.CompleteAllTransitions();
                        label = l;
                        point.Context.Label = l;
                        DataLabelsDrawableTask.AddGeometyToPaintTask(l);
                    }

                    label.Text = DataLabelFormatter(point);
                    label.TextSize = dls;
                    label.Padding = DataLabelsPadding;
                    var labelPosition = GetLabelPosition(
                        cx, y, b, uw, label.Measure(DataLabelsDrawableTask), DataLabelsPosition, SeriesProperties, point.PrimaryValue > Pivot);
                    label.X = labelPosition.X;
                    label.Y = labelPosition.Y;

                    chart.MeasuredDrawables.Add(label);
                }
            }
        }

        public override DimensinalBounds GetBounds(
            CartesianChart<TDrawingContext> chart, IAxis<TDrawingContext> secondaryAxis, IAxis<TDrawingContext> primaryAxis)
        {
            var baseBounds = base.GetBounds(chart, secondaryAxis, primaryAxis);

            var tick = secondaryAxis.GetTick(chart.ControlSize, baseBounds.PrimaryBounds);

            return new DimensinalBounds
            {
                PrimaryBounds = new Bounds
                {
                    Max = baseBounds.SecondaryBounds.Max + 0.5,
                    Min = baseBounds.SecondaryBounds.Min - 0.5
                },
                SecondaryBounds = new Bounds
                {
                    Max = baseBounds.PrimaryBounds.Max + tick.Value,
                    min = baseBounds.PrimaryBounds.min - tick.Value
                }
            };
        }

        protected virtual void DefaultOnPointCreated(TVisual visual, IChartView<TDrawingContext> chart)
        {
            
        }

        protected override void SetDefaultPointTransitions(ChartPoint chartPoint)
        {
            var visual = chartPoint.Context.Visual as TVisual;
            var chart = chartPoint.Context.Chart;

            if (visual == null) throw new Exception("Unable to initialize the point instance.");

            visual
                .TransitionateProperties(
                    nameof(visual.X),
                    nameof(visual.Width))
                .WithAnimation(animation =>
                    animation
                        .WithDuration((long)(chart.AnimationsSpeed.TotalMilliseconds * 1.5))
                        .WithEasingFunction(elasticFunction));

            visual
                .TransitionateProperties(
                    nameof(visual.Y),
                    nameof(visual.Height))
                .WithAnimation(animation =>
                    animation
                        .WithDuration(chart.AnimationsSpeed)
                        .WithEasingFunction(chart.EasingFunction));
        }
    }
}
