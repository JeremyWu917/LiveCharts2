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

using LiveChartsCore.Drawing;

namespace LiveChartsCore.Kernel
{
    /// <summary>
    /// Defines a point in a chart with known visual and label types.
    /// </summary>
    /// <typeparam name="TVisual">The type of the visual.</typeparam>
    /// <typeparam name="TLabel">The type of the label.</typeparam>
    /// <typeparam name="TDrawingContext">The type of the drawing context.</typeparam>
    public class TypedChartPoint<TVisual, TLabel, TDrawingContext>
        where TVisual : class, IVisualChartPoint<TDrawingContext>
        where TLabel : class, ILabelGeometry<TDrawingContext>
        where TDrawingContext: DrawingContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypedChartPoint{TVisual, TLabel, TDrawingContext}"/> class.
        /// </summary>
        /// <param name="point">The point.</param>
        public TypedChartPoint(ChartPoint point)
        {
            ChartPoint = point;
        }

        /// <summary>
        /// Gets the chart point.
        /// </summary>
        /// <value>
        /// The chart point.
        /// </value>
        public ChartPoint ChartPoint { get; }

        /// <summary>
        /// Gets the visual.
        /// </summary>
        /// <value>
        /// The visual.
        /// </value>
        public TVisual? Visual => ChartPoint.Context.Visual as TVisual;

        /// <summary>
        /// Gets the label.
        /// </summary>
        /// <value>
        /// The label.
        /// </value>
        public TLabel? Label => ChartPoint.Context.Label as TLabel;
    }
}