// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.VisualElements;

namespace LiveChartsCore.SkiaSharpView.Avalonia;

/// <inheritdoc cref="IPolarChartView{TDrawingContext}" />
public class PolarChart : UserControl, IPolarChartView<SkiaSharpDrawingContext>, IAvaloniaChart
{
    #region fields

    /// <summary>
    /// The legend
    /// </summary>
    protected IChartLegend<SkiaSharpDrawingContext>? legend;

    /// <summary>
    /// The tool tip
    /// </summary>
    protected IChartTooltip<SkiaSharpDrawingContext>? tooltip;

    private MotionCanvas? _avaloniaCanvas;
    private Chart<SkiaSharpDrawingContext>? _core;
    private readonly CollectionDeepObserver<ISeries> _seriesObserver;
    private readonly CollectionDeepObserver<IPolarAxis> _angleObserver;
    private readonly CollectionDeepObserver<IPolarAxis> _radiusObserver;
    private readonly CollectionDeepObserver<ChartElement<SkiaSharpDrawingContext>> _visualsObserver;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="PolarChart"/> class.
    /// </summary>
    /// <exception cref="Exception">Default colors are not valid</exception>
    public PolarChart()
    {
        InitializeComponent();

        // workaround to detect mouse events.
        // Avalonia do not seem to detect pointer events if background is not set.
        ((IChartView)this).BackColor = LvcColor.FromArgb(0, 0, 0, 0);

        if (!LiveCharts.IsConfigured) LiveCharts.Configure(LiveChartsSkiaSharp.DefaultPlatformBuilder);

        var stylesBuilder = LiveCharts.CurrentSettings.GetTheme<SkiaSharpDrawingContext>();
        var initializer = stylesBuilder.GetVisualsInitializer();
        if (stylesBuilder.CurrentColors is null || stylesBuilder.CurrentColors.Length == 0)
            throw new Exception("Default colors are not valid");
        initializer.ApplyStyleToChart(this);

        InitializeCore();

        AttachedToVisualTree += OnAttachedToVisualTree;

        _seriesObserver = new CollectionDeepObserver<ISeries>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
        _angleObserver = new CollectionDeepObserver<IPolarAxis>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
        _radiusObserver = new CollectionDeepObserver<IPolarAxis>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
        _visualsObserver = new CollectionDeepObserver<ChartElement<SkiaSharpDrawingContext>>(
           OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);

        AngleAxes = new List<IPolarAxis>()
            {
                LiveCharts.CurrentSettings.GetProvider<SkiaSharpDrawingContext>().GetDefaultPolarAxis()
            };
        RadiusAxes = new List<IPolarAxis>()
            {
                LiveCharts.CurrentSettings.GetProvider<SkiaSharpDrawingContext>().GetDefaultPolarAxis()
            };
        Series = new ObservableCollection<ISeries>();
        VisualElements = new ObservableCollection<ChartElement<SkiaSharpDrawingContext>>();

        PointerWheelChanged += PolarChart_PointerWheelChanged;
        PointerPressed += PolarChart_PointerPressed;
        PointerMoved += PolarChart_PointerMoved;

        PointerLeave += PolarChart_PointerLeave;
        DetachedFromVisualTree += PolarChart_DetachedFromVisualTree;
    }

    #region avalonia/dependency properties

    /// <summary>
    /// The sync context property.
    /// </summary>
    public static readonly AvaloniaProperty<object> SyncContextProperty =
       AvaloniaProperty.Register<PolarChart, object>(nameof(SyncContext), new object(), inherits: true);

    /// <summary>
    /// The title property.
    /// </summary>
    public static readonly AvaloniaProperty<VisualElement<SkiaSharpDrawingContext>?> TitleProperty =
       AvaloniaProperty.Register<PolarChart, VisualElement<SkiaSharpDrawingContext>?>(nameof(Title), null, inherits: true);

    /// <summary>
    /// The series property.
    /// </summary>
    public static readonly AvaloniaProperty<IEnumerable<ISeries>> SeriesProperty =
       AvaloniaProperty.Register<PolarChart, IEnumerable<ISeries>>(nameof(Series), Enumerable.Empty<ISeries>(), inherits: true);

    /// <summary>
    /// The visual elements property
    /// </summary>
    public static readonly AvaloniaProperty<IEnumerable<ChartElement<SkiaSharpDrawingContext>>> VisualElementsProperty =
        AvaloniaProperty.Register<PolarChart, IEnumerable<ChartElement<SkiaSharpDrawingContext>>>(
            nameof(VisualElements), Enumerable.Empty<ChartElement<SkiaSharpDrawingContext>>(), inherits: true);

    /// <summary>
    /// The fit to bounds property.
    /// </summary>
    public static readonly AvaloniaProperty<bool> FitToBoundsProperty =
        AvaloniaProperty.Register<PolarChart, bool>(nameof(FitToBounds), false, inherits: true);

    /// <summary>
    /// The total angle property.
    /// </summary>
    public static readonly AvaloniaProperty<double> TotalAngleProperty =
        AvaloniaProperty.Register<PolarChart, double>(nameof(TotalAngle), 360d, inherits: true);

    /// <summary>
    /// The inner radius property.
    /// </summary>
    public static readonly AvaloniaProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<PolarChart, double>(nameof(InnerRadius), 0d, inherits: true);

    /// <summary>
    /// The initial rotation property.
    /// </summary>
    public static readonly AvaloniaProperty<double> InitialRotationProperty =
        AvaloniaProperty.Register<PolarChart, double>(
            nameof(InitialRotation), LiveCharts.CurrentSettings.PolarInitialRotation, inherits: true);

    /// <summary>
    /// The x axes property.
    /// </summary>
    public static readonly AvaloniaProperty<IEnumerable<IPolarAxis>> AngleAxesProperty =
        AvaloniaProperty.Register<PolarChart, IEnumerable<IPolarAxis>>(nameof(AngleAxes), Enumerable.Empty<IPolarAxis>(), inherits: true);

    /// <summary>
    /// The y axes property.
    /// </summary>
    public static readonly AvaloniaProperty<IEnumerable<IPolarAxis>> RadiusAxesProperty =
        AvaloniaProperty.Register<PolarChart, IEnumerable<IPolarAxis>>(nameof(RadiusAxes), Enumerable.Empty<IPolarAxis>(), inherits: true);

    /// <summary>
    /// The animations speed property.
    /// </summary>
    public static readonly AvaloniaProperty<TimeSpan> AnimationsSpeedProperty =
        AvaloniaProperty.Register<PolarChart, TimeSpan>(
            nameof(AnimationsSpeed), LiveCharts.CurrentSettings.DefaultAnimationsSpeed, inherits: true);

    /// <summary>
    /// The easing function property.
    /// </summary>
    public static readonly AvaloniaProperty<Func<float, float>> EasingFunctionProperty =
        AvaloniaProperty.Register<PolarChart, Func<float, float>>(
            nameof(AnimationsSpeed), LiveCharts.CurrentSettings.DefaultEasingFunction, inherits: true);

    /// <summary>
    /// The tool tip template property.
    /// </summary>
    public static readonly AvaloniaProperty<DataTemplate?> TooltipTemplateProperty =
        AvaloniaProperty.Register<PolarChart, DataTemplate?>(nameof(TooltipTemplate), null, inherits: true);

    /// <summary>
    /// The tool tip position property.
    /// </summary>
    public static readonly AvaloniaProperty<TooltipPosition> TooltipPositionProperty =
        AvaloniaProperty.Register<PolarChart, TooltipPosition>(
            nameof(TooltipPosition), LiveCharts.CurrentSettings.DefaultTooltipPosition, inherits: true);

    /// <summary>
    /// The tool tip finding strategy property.
    /// </summary>
    public static readonly AvaloniaProperty<TooltipFindingStrategy> TooltipFindingStrategyProperty =
        AvaloniaProperty.Register<PolarChart, TooltipFindingStrategy>(
            nameof(LegendPosition), LiveCharts.CurrentSettings.DefaultTooltipFindingStrategy, inherits: true);

    /// <summary>
    /// The tool tip font family property.
    /// </summary>
    public static readonly AvaloniaProperty<FontFamily> TooltipFontFamilyProperty =
        AvaloniaProperty.Register<PolarChart, FontFamily>(
            nameof(TooltipFontFamily), new FontFamily("Arial"), inherits: true);

    /// <summary>
    /// The tool tip font size property.
    /// </summary>
    public static readonly AvaloniaProperty<double> TooltipFontSizeProperty =
        AvaloniaProperty.Register<PolarChart, double>(nameof(TooltipFontSize), 13d, inherits: true);

    /// <summary>
    /// The tool tip font weight property.
    /// </summary>
    public static readonly AvaloniaProperty<FontWeight> TooltipFontWeightProperty =
        AvaloniaProperty.Register<PolarChart, FontWeight>(nameof(TooltipFontWeight), FontWeight.Normal, inherits: true);

    /// <summary>
    /// The tool tip font style property.
    /// </summary>
    public static readonly AvaloniaProperty<FontStyle> TooltipFontStyleProperty =
        AvaloniaProperty.Register<PolarChart, FontStyle>(
            nameof(TooltipFontStyle), FontStyle.Normal, inherits: true);

    /// <summary>
    /// The tool tip text brush property.
    /// </summary>
    public static readonly AvaloniaProperty<SolidColorBrush> TooltipTextBrushProperty =
        AvaloniaProperty.Register<PolarChart, SolidColorBrush>(
            nameof(TooltipTextBrush), new SolidColorBrush(new Color(255, 35, 35, 35)), inherits: true);

    /// <summary>
    /// The tool tip background property.
    /// </summary>
    public static readonly AvaloniaProperty<IBrush> TooltipBackgroundProperty =
        AvaloniaProperty.Register<PolarChart, IBrush>(nameof(TooltipBackground),
            new SolidColorBrush(new Color(255, 250, 250, 250)), inherits: true);

    /// <summary>
    /// The legend position property.
    /// </summary>
    public static readonly AvaloniaProperty<LegendPosition> LegendPositionProperty =
        AvaloniaProperty.Register<PolarChart, LegendPosition>(
            nameof(LegendPosition), LiveCharts.CurrentSettings.DefaultLegendPosition, inherits: true);

    /// <summary>
    /// The legend orientation property.
    /// </summary>
    public static readonly AvaloniaProperty<LegendOrientation> LegendOrientationProperty =
        AvaloniaProperty.Register<PolarChart, LegendOrientation>(
            nameof(LegendOrientation), LiveCharts.CurrentSettings.DefaultLegendOrientation, inherits: true);

    /// <summary>
    /// The legend template property.
    /// </summary>
    public static readonly AvaloniaProperty<DataTemplate?> LegendTemplateProperty =
        AvaloniaProperty.Register<PolarChart, DataTemplate?>(nameof(LegendTemplate), null, inherits: true);

    /// <summary>
    /// The legend font family property.
    /// </summary>
    public static readonly AvaloniaProperty<FontFamily> LegendFontFamilyProperty =
       AvaloniaProperty.Register<PolarChart, FontFamily>(
           nameof(LegendFontFamily), new FontFamily("Arial"), inherits: true);

    /// <summary>
    /// The legend font size property
    /// </summary>
    public static readonly AvaloniaProperty<double> LegendFontSizeProperty =
        AvaloniaProperty.Register<PolarChart, double>(nameof(LegendFontSize), 13d, inherits: true);

    /// <summary>
    /// The legend font weight property.
    /// </summary>
    public static readonly AvaloniaProperty<FontWeight> LegendFontWeightProperty =
        AvaloniaProperty.Register<PolarChart, FontWeight>(nameof(LegendFontWeight), FontWeight.Normal, inherits: true);

    /// <summary>
    /// The legend font style property.
    /// </summary>
    public static readonly AvaloniaProperty<FontStyle> LegendFontStyleProperty =
        AvaloniaProperty.Register<PolarChart, FontStyle>(
            nameof(LegendFontStyle), FontStyle.Normal, inherits: true);

    /// <summary>
    /// The legend text brush property.
    /// </summary>
    public static readonly AvaloniaProperty<SolidColorBrush> LegendTextBrushProperty =
        AvaloniaProperty.Register<PolarChart, SolidColorBrush>(
            nameof(LegendTextBrush), new SolidColorBrush(new Color(255, 35, 35, 35)), inherits: true);

    /// <summary>
    /// The legend background property.
    /// </summary>
    public static readonly AvaloniaProperty<IBrush> LegendBackgroundProperty =
        AvaloniaProperty.Register<PolarChart, IBrush>(nameof(LegendBackground),
            new SolidColorBrush(new Color(255, 255, 255, 255)), inherits: true);

    /// <summary>
    /// The data pointer down command property
    /// </summary>
    public static readonly AvaloniaProperty<ICommand?> DataPointerDownCommandProperty =
        AvaloniaProperty.Register<PolarChart, ICommand?>(nameof(DataPointerDownCommand), null, inherits: true);

    /// <summary>
    /// The chart point pointer down command property
    /// </summary>
    public static readonly AvaloniaProperty<ICommand?> ChartPointPointerDownCommandProperty =
        AvaloniaProperty.Register<PolarChart, ICommand?>(nameof(ChartPointPointerDownCommand), null, inherits: true);

    /// <summary>
    /// The <see cref="VisualElement{TDrawingContext}"/> pointer down command property
    /// </summary>
    public static readonly AvaloniaProperty<ICommand?> VisualElementsPointerDownCommandProperty =
        AvaloniaProperty.Register<PolarChart, ICommand?>(nameof(VisualElementsPointerDownCommand), null, inherits: true);

    #endregion

    #region events

    /// <inheritdoc cref="IChartView{TDrawingContext}.Measuring" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? Measuring;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateStarted" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateStarted;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateFinished" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateFinished;

    /// <inheritdoc cref="IChartView.DataPointerDown" />
    public event ChartPointsHandler? DataPointerDown;

    /// <inheritdoc cref="IChartView.ChartPointPointerDown" />
    public event ChartPointHandler? ChartPointPointerDown;

    /// <inheritdoc cref="IChartView{TDrawingContext}.VisualElementsPointerDown"/>
    public event VisualElementHandler<SkiaSharpDrawingContext>? VisualElementsPointerDown;

    #endregion

    #region properties

    /// <inheritdoc cref="IChartView.DesignerMode" />
    bool IChartView.DesignerMode => Design.IsDesignMode;

    /// <inheritdoc cref="IChartView.CoreChart" />
    public IChart CoreChart => _core ?? throw new Exception("Core not set yet.");

    /// <inheritdoc cref="IChartView{TDrawingContext}.CoreCanvas" />
    public MotionCanvas<SkiaSharpDrawingContext> CoreCanvas => _core is null ? throw new Exception("core not found") : _core.Canvas;

    PolarChart<SkiaSharpDrawingContext> IPolarChartView<SkiaSharpDrawingContext>.Core =>
        _core is null ? throw new Exception("core not found") : (PolarChart<SkiaSharpDrawingContext>)_core;

    LvcColor IChartView.BackColor
    {
        get => Background is not ISolidColorBrush b
                ? new LvcColor()
                : LvcColor.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B);
        set => Background = new SolidColorBrush(new Color(value.R, value.G, value.B, value.A));
    }

    LvcSize IChartView.ControlSize => _avaloniaCanvas is null
        ? new LvcSize()
        : new LvcSize
        {
            Width = (float)_avaloniaCanvas.Bounds.Width,
            Height = (float)_avaloniaCanvas.Bounds.Height
        };

    /// <inheritdoc cref="IChartView.DrawMargin" />
    public Margin? DrawMargin { get => null; set => throw new NotImplementedException(); }

    /// <inheritdoc cref="IChartView.SyncContext" />
    public object SyncContext
    {
        get => GetValue(SyncContextProperty);
        set => SetValue(SyncContextProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.FitToBounds" />
    public bool FitToBounds
    {
        get => (bool)GetValue(FitToBoundsProperty);
        set => SetValue(FitToBoundsProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.TotalAngle" />
    public double TotalAngle
    {
        get => (double)GetValue(TotalAngleProperty);
        set => SetValue(TotalAngleProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.InnerRadius" />
    public double InnerRadius
    {
        get => (double)GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.InitialRotation" />
    public double InitialRotation
    {
        get => (double)GetValue(InitialRotationProperty);
        set => SetValue(InitialRotationProperty, value);
    }

    /// <inheritdoc cref="IChartView{SkiaSharpDrawingContext}.Title" />
    public VisualElement<SkiaSharpDrawingContext>? Title
    {
        get => (VisualElement<SkiaSharpDrawingContext>)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.Series" />
    public IEnumerable<ISeries> Series
    {
        get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.AngleAxes" />
    public IEnumerable<IPolarAxis> AngleAxes
    {
        get => (IEnumerable<IPolarAxis>)GetValue(AngleAxesProperty);
        set => SetValue(AngleAxesProperty, value);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.RadiusAxes" />
    public IEnumerable<IPolarAxis> RadiusAxes
    {
        get => (IEnumerable<IPolarAxis>)GetValue(RadiusAxesProperty);
        set => SetValue(RadiusAxesProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.VisualElements" />
    public IEnumerable<ChartElement<SkiaSharpDrawingContext>> VisualElements
    {
        get => (IEnumerable<ChartElement<SkiaSharpDrawingContext>>)GetValue(VisualElementsProperty);
        set => SetValue(VisualElementsProperty, value);
    }

    /// <inheritdoc cref="IChartView.AnimationsSpeed" />
    public TimeSpan AnimationsSpeed
    {
        get => (TimeSpan)GetValue(AnimationsSpeedProperty);
        set => SetValue(AnimationsSpeedProperty, value);
    }

    /// <inheritdoc cref="IChartView.EasingFunction" />
    public Func<float, float>? EasingFunction
    {
        get => (Func<float, float>)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    /// <inheritdoc cref="IChartView.TooltipPosition" />
    public TooltipPosition TooltipPosition
    {
        get => (TooltipPosition)GetValue(TooltipPositionProperty);
        set => SetValue(TooltipPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip data template.
    /// </summary>
    /// <value>
    /// The tool tip template.
    /// </value>
    public DataTemplate TooltipTemplate
    {
        get => (DataTemplate)GetValue(TooltipTemplateProperty);
        set => SetValue(TooltipTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip default font family.
    /// </summary>
    /// <value>
    /// The tool tip font family.
    /// </value>
    public FontFamily TooltipFontFamily
    {
        get => (FontFamily)GetValue(TooltipFontFamilyProperty);
        set => SetValue(TooltipFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the default size of the tool tip font.
    /// </summary>
    /// <value>
    /// The size of the tool tip font.
    /// </value>
    public double TooltipFontSize
    {
        get => (double)GetValue(TooltipFontSizeProperty);
        set => SetValue(TooltipFontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip default font weight.
    /// </summary>
    /// <value>
    /// The tool tip font weight.
    /// </value>
    public FontWeight TooltipFontWeight
    {
        get => (FontWeight)GetValue(TooltipFontWeightProperty);
        set => SetValue(TooltipFontWeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip default font style.
    /// </summary>
    /// <value>
    /// The tool tip font style.
    /// </value>
    public FontStyle TooltipFontStyle
    {
        get => (FontStyle)GetValue(TooltipFontStyleProperty);
        set => SetValue(TooltipFontStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip default text brush.
    /// </summary>
    /// <value>
    /// The tool tip text brush.
    /// </value>
    public SolidColorBrush TooltipTextBrush
    {
        get => (SolidColorBrush)GetValue(TooltipTextBrushProperty);
        set => SetValue(TooltipTextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the tool tip default background.
    /// </summary>
    /// <value>
    /// The tool tip background.
    /// </value>
    public IBrush TooltipBackground
    {
        get => (IBrush)GetValue(TooltipBackgroundProperty);
        set => SetValue(TooltipBackgroundProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Tooltip" />
    public IChartTooltip<SkiaSharpDrawingContext>? Tooltip => tooltip;

    /// <inheritdoc cref="IChartView.LegendPosition" />
    public LegendPosition LegendPosition
    {
        get => (LegendPosition)GetValue(LegendPositionProperty);
        set => SetValue(LegendPositionProperty, value);
    }

    /// <inheritdoc cref="IChartView.LegendOrientation" />
    public LegendOrientation LegendOrientation
    {
        get => (LegendOrientation)GetValue(LegendOrientationProperty);
        set => SetValue(LegendOrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend template.
    /// </summary>
    /// <value>
    /// The legend template.
    /// </value>
    public DataTemplate LegendTemplate
    {
        get => (DataTemplate)GetValue(LegendTemplateProperty);
        set => SetValue(LegendTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend default font family.
    /// </summary>
    /// <value>
    /// The legend font family.
    /// </value>
    public FontFamily LegendFontFamily
    {
        get => (FontFamily)GetValue(LegendFontFamilyProperty);
        set => SetValue(LegendFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the legend default font.
    /// </summary>
    /// <value>
    /// The size of the legend font.
    /// </value>
    public double LegendFontSize
    {
        get => (double)GetValue(LegendFontSizeProperty);
        set => SetValue(LegendFontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend default font weight.
    /// </summary>
    /// <value>
    /// The legend font weight.
    /// </value>
    public FontWeight LegendFontWeight
    {
        get => (FontWeight)GetValue(LegendFontWeightProperty);
        set => SetValue(LegendFontWeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend default font style.
    /// </summary>
    /// <value>
    /// The legend font style.
    /// </value>
    public FontStyle LegendFontStyle
    {
        get => (FontStyle)GetValue(LegendFontStyleProperty);
        set => SetValue(LegendFontStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend default text brush.
    /// </summary>
    /// <value>
    /// The legend text brush.
    /// </value>
    public SolidColorBrush LegendTextBrush
    {
        get => (SolidColorBrush)GetValue(LegendTextBrushProperty);
        set => SetValue(LegendTextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the legend default background.
    /// </summary>
    /// <value>
    /// The legend background.
    /// </value>
    public IBrush LegendBackground
    {
        get => (IBrush)GetValue(LegendBackgroundProperty);
        set => SetValue(LegendBackgroundProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Legend" />
    public IChartLegend<SkiaSharpDrawingContext>? Legend => legend;

    /// <inheritdoc cref="IChartView{TDrawingContext}.AutoUpdateEnabled" />
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <inheritdoc cref="IChartView.UpdaterThrottler" />
    public TimeSpan UpdaterThrottler
    {
        get => _core?.UpdaterThrottler ?? throw new Exception("core not set yet.");
        set
        {
            if (_core is null) throw new Exception("core not set yet.");
            _core.UpdaterThrottler = value;
        }
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a data or data points.
    /// </summary>
    public ICommand? DataPointerDownCommand
    {
        get => (ICommand?)GetValue(DataPointerDownCommandProperty);
        set => SetValue(DataPointerDownCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a chart point.
    /// </summary>
    public ICommand? ChartPointPointerDownCommand
    {
        get => (ICommand?)GetValue(ChartPointPointerDownCommandProperty);
        set => SetValue(ChartPointPointerDownCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a visual element(s).
    /// </summary>
    public ICommand? VisualElementsPointerDownCommand
    {
        get => (ICommand?)GetValue(VisualElementsPointerDownCommandProperty);
        set => SetValue(VisualElementsPointerDownCommandProperty, value);
    }

    #endregion

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.ScalePixelsToData(LvcPointD, int, int)"/>
    public LvcPointD ScalePixelsToData(LvcPointD point, int angleAxisIndex = 0, int radiusAxisIndex = 0)
    {
        if (_core is not PolarChart<SkiaSharpDrawingContext> cc) throw new Exception("core not found");

        var scaler = new PolarScaler(
            cc.DrawMarginLocation, cc.DrawMarginSize, cc.AngleAxes[angleAxisIndex], cc.RadiusAxes[radiusAxisIndex],
            cc.InnerRadius, cc.InitialRotation, cc.TotalAnge);

        return scaler.ToChartValues(point.X, point.Y);
    }

    /// <inheritdoc cref="IPolarChartView{TDrawingContext}.ScaleDataToPixels(LvcPointD, int, int)"/>
    public LvcPointD ScaleDataToPixels(LvcPointD point, int angleAxisIndex = 0, int radiusAxisIndex = 0)
    {
        if (_core is not PolarChart<SkiaSharpDrawingContext> cc) throw new Exception("core not found");

        var scaler = new PolarScaler(
            cc.DrawMarginLocation, cc.DrawMarginSize, cc.AngleAxes[angleAxisIndex], cc.RadiusAxes[radiusAxisIndex],
            cc.InnerRadius, cc.InitialRotation, cc.TotalAnge);

        var r = scaler.ToPixels(point.X, point.Y);

        return new LvcPointD { X = (float)r.X, Y = (float)r.Y };
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.GetPointsAt(LvcPoint, TooltipFindingStrategy)"/>
    public IEnumerable<ChartPoint> GetPointsAt(LvcPoint point, TooltipFindingStrategy strategy = TooltipFindingStrategy.Automatic)
    {
        if (_core is not PolarChart<SkiaSharpDrawingContext> cc) throw new Exception("core not found");

        if (strategy == TooltipFindingStrategy.Automatic)
            strategy = cc.Series.GetTooltipFindingStrategy();

        return cc.Series.SelectMany(series => series.FindHitPoints(cc, point, strategy));
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.GetVisualsAt(LvcPoint)"/>
    public IEnumerable<VisualElement<SkiaSharpDrawingContext>> GetVisualsAt(LvcPoint point)
    {
        return _core is not PolarChart<SkiaSharpDrawingContext> cc
            ? throw new Exception("core not found")
            : cc.VisualElements.SelectMany(visual => ((VisualElement<SkiaSharpDrawingContext>)visual).IsHitBy(point));
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.ShowTooltip(IEnumerable{ChartPoint})"/>
    public void ShowTooltip(IEnumerable<ChartPoint> points)
    {
        if (tooltip is null || _core is null) return;

        tooltip.Show(points, _core);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.HideTooltip"/>
    public void HideTooltip()
    {
        if (tooltip is null || _core is null) return;

        _core.ClearTooltipData();
        tooltip.Hide();
    }

    /// <inheritdoc cref="IAvaloniaChart.GetCanvasPosition"/>
    Point IAvaloniaChart.GetCanvasPosition()
    {
        var p = _avaloniaCanvas.TranslatePoint(new Point(0, 0), this);
        return _avaloniaCanvas is null || p is null ? throw new Exception("Canvas not found") : p.Value;
    }

    /// <inheritdoc cref="IChartView.SetTooltipStyle(LvcColor, LvcColor)"/>
    public void SetTooltipStyle(LvcColor background, LvcColor textColor)
    {
        TooltipBackground = new SolidColorBrush(new Color(background.A, background.R, background.G, background.B));
        TooltipTextBrush = new SolidColorBrush(new Color(textColor.A, textColor.R, textColor.G, textColor.B));
    }

    void IChartView.InvokeOnUIThread(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    /// <summary>
    /// Initializes the core.
    /// </summary>
    /// <returns></returns>
    protected void InitializeCore()
    {
        var canvas = this.FindControl<MotionCanvas>("canvas");
        _avaloniaCanvas = canvas;
        _core = new PolarChart<SkiaSharpDrawingContext>(
            this, LiveChartsSkiaSharp.DefaultPlatformBuilder, canvas.CanvasCore);

        _core.Measuring += OnCoreMeasuring;
        _core.UpdateStarted += OnCoreUpdateStarted;
        _core.UpdateFinished += OnCoreUpdateFinished;

        legend = this.FindControl<DefaultLegend>("legend");
        tooltip = this.FindControl<DefaultTooltip>("tooltip");

        _core.Update();
    }

    /// <inheritdoc cref="OnPropertyChanged{T}(AvaloniaPropertyChangedEventArgs{T})" />
    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);

        if (_core is null || change.Property.Name == nameof(IsPointerOver)) return;

        if (change.Property.Name == nameof(SyncContext))
        {
            CoreCanvas.Sync = change.NewValue;
        }

        if (change.Property.Name == nameof(Series))
        {
            _seriesObserver?.Dispose((IEnumerable<ISeries>)change.OldValue.Value);
            _seriesObserver?.Initialize((IEnumerable<ISeries>)change.NewValue.Value);
        }

        if (change.Property.Name == nameof(AngleAxes))
        {
            _angleObserver?.Dispose((IEnumerable<IPolarAxis>)change.OldValue.Value);
            _angleObserver?.Initialize((IEnumerable<IPolarAxis>)change.NewValue.Value);
        }

        if (change.Property.Name == nameof(RadiusAxes))
        {
            _radiusObserver?.Dispose((IEnumerable<IPolarAxis>)change.OldValue.Value);
            _radiusObserver?.Initialize((IEnumerable<IPolarAxis>)change.NewValue.Value);
        }

        if (change.Property.Name == nameof(VisualElements))
        {
            _visualsObserver?.Dispose((IEnumerable<ChartElement<SkiaSharpDrawingContext>>)change.OldValue.Value);
            _visualsObserver?.Initialize((IEnumerable<ChartElement<SkiaSharpDrawingContext>>)change.NewValue.Value);
        }

        _core.Update();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDeepCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;

        _core.Update();
    }

    private void OnDeepCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;

        _core.Update();
    }

    private void PolarChart_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        //if (core is null) return;

        //var c = (PolarChart<SkiaSharpDrawingContext>)core;
        //var p = e.GetPosition(this);

        //c.Zoom(new PointF((float)p.X, (float)p.Y), e.Delta.Y > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut);
    }

    private void PolarChart_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Application.Current.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var p = e.GetPosition(this);
        foreach (var w in desktop.Windows) w.PointerReleased += Window_PointerReleased;
        _core?.InvokePointerDown(new LvcPoint((float)p.X, (float)p.Y), false);
    }

    private void PolarChart_PointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(_avaloniaCanvas);
        _core?.InvokePointerMove(new LvcPoint((float)p.X, (float)p.Y));
    }

    private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Application.Current.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        foreach (var w in desktop.Windows) w.PointerReleased -= Window_PointerReleased;
        var p = e.GetPosition(this);
        _core?.InvokePointerUp(new LvcPoint((float)p.X, (float)p.Y), false);
    }

    private void OnCoreUpdateFinished(IChartView<SkiaSharpDrawingContext> chart)
    {
        UpdateFinished?.Invoke(this);
    }

    private void OnCoreUpdateStarted(IChartView<SkiaSharpDrawingContext> chart)
    {
        UpdateStarted?.Invoke(this);
    }

    private void OnCoreMeasuring(IChartView<SkiaSharpDrawingContext> chart)
    {
        Measuring?.Invoke(this);
    }

    private void PolarChart_PointerLeave(object? sender, PointerEventArgs e)
    {
        _ = Dispatcher.UIThread.InvokeAsync(HideTooltip, DispatcherPriority.Background);
        _core?.InvokePointerLeft();
    }

    private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        _core?.Load();
    }

    private void PolarChart_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        _core?.Unload();
    }

    void IChartView.OnDataPointerDown(IEnumerable<ChartPoint> points, LvcPoint pointer)
    {
        DataPointerDown?.Invoke(this, points);
        if (DataPointerDownCommand is not null && DataPointerDownCommand.CanExecute(points)) DataPointerDownCommand.Execute(points);

        var closest = points.FindClosestTo(pointer);
        ChartPointPointerDown?.Invoke(this, closest);
        if (ChartPointPointerDownCommand is not null && ChartPointPointerDownCommand.CanExecute(closest)) ChartPointPointerDownCommand.Execute(closest);
    }

    void IChartView<SkiaSharpDrawingContext>.OnVisualElementPointerDown(
        IEnumerable<VisualElement<SkiaSharpDrawingContext>> visualElements, LvcPoint pointer)
    {
        var args = new VisualElementsEventArgs<SkiaSharpDrawingContext>(visualElements, pointer);

        VisualElementsPointerDown?.Invoke(this, args);
        if (VisualElementsPointerDownCommand is not null && VisualElementsPointerDownCommand.CanExecute(args))
            VisualElementsPointerDownCommand.Execute(args);
    }

    void IChartView.Invalidate()
    {
        CoreCanvas.Invalidate();
    }
}
