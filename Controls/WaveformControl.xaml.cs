using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Vexa.Models;

namespace Vexa.Controls;

public partial class WaveformControl : UserControl
{
    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(WaveformData), typeof(WaveformControl),
            new PropertyMetadata(null, OnWaveformDataChanged));

    public static readonly DependencyProperty CurrentPositionProperty =
        DependencyProperty.Register(nameof(CurrentPosition), typeof(TimeSpan), typeof(WaveformControl),
            new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPositionChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(WaveformControl),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    public WaveformControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    public WaveformData? WaveformData
    {
        get => (WaveformData)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    public TimeSpan CurrentPosition
    {
        get => (TimeSpan)GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).Render();
    private static void OnCurrentPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).UpdatePlayhead();
    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).Render();

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (WaveformData == null || WaveformData.Duration.TotalSeconds == 0) return;

        Point p = e.GetPosition(MainGrid);
        double x = p.X;
        double width = ActualWidth * ZoomLevel;

        if (x < 0 || x > width)
        {
            OnMouseLeave(sender, e);
            return;
        }

        double progress = x / width;
        TimeSpan time = TimeSpan.FromSeconds(progress * WaveformData.Duration.TotalSeconds);

        HoverLine.Visibility = Visibility.Visible;
        HoverLine.Margin = new Thickness(x, 0, 0, 0);

        HoverTooltip.Visibility = Visibility.Visible;
        HoverTimeText.Text = FormatTimeTooltip(time);

        // Position tooltip above mouse, handle edge cases
        double tooltipX = x + 10;
        if (tooltipX + HoverTooltip.ActualWidth > ActualWidth)
            tooltipX = x - HoverTooltip.ActualWidth - 10;

        HoverTooltip.Margin = new Thickness(tooltipX, p.Y - 30, 0, 0);
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HoverLine.Visibility = Visibility.Collapsed;
        HoverTooltip.Visibility = Visibility.Collapsed;
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (WaveformData == null || WaveformData.Duration.TotalSeconds == 0) return;

        Point p = e.GetPosition(MainGrid);
        double progress = p.X / (ActualWidth * ZoomLevel);
        CurrentPosition = TimeSpan.FromSeconds(progress * WaveformData.Duration.TotalSeconds);
    }

    private string FormatTimeTooltip(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"hh\:mm\:ss\.f");
        return time.ToString(@"mm\:ss\.f");
    }

    private void Render()
    {
        WaveformCanvas.Children.Clear();
        RulerCanvas.Children.Clear();

        if (WaveformData == null || WaveformData.Peaks.Length == 0 || ActualWidth == 0)
        {
            Playhead.Visibility = Visibility.Collapsed;
            return;
        }

        Playhead.Visibility = Visibility.Visible;
        UpdatePlayhead();

        double width = ActualWidth * ZoomLevel;
        double height = WaveformCanvas.ActualHeight;
        double midY = height / 2;

        WaveformCanvas.Width = width;
        RulerCanvas.Width = width;

        // Draw Waveform using StreamGeometry for better performance and quality
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            // Start at the bottom left
            ctx.BeginFigure(new Point(0, height), true, true);

            for (int i = 0; i < WaveformData.Peaks.Length; i++)
            {
                double x = (double)i / WaveformData.Peaks.Length * width;
                // Scale peak to 85% of available height, base at bottom
                double y = height - (WaveformData.Peaks[i] * height * 0.85);
                ctx.LineTo(new Point(x, y), true, false);
            }

            // End at the bottom right
            ctx.LineTo(new Point(width, height), true, false);
        }

        Path waveformPath = new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.FromRgb(74, 144, 226)), // Nice blue
            Opacity = 0.6,
            Stroke = new SolidColorBrush(Color.FromRgb(58, 114, 180)), // Darker blue border
            StrokeThickness = 0.5,
            IsHitTestVisible = false
        };

        WaveformCanvas.Children.Add(waveformPath);

        // Draw Ruler
        int minutes = (int)Math.Ceiling(WaveformData.Duration.TotalMinutes);
        for (int i = 0; i <= minutes; i++)
        {
            double x = (TimeSpan.FromMinutes(i).TotalSeconds / WaveformData.Duration.TotalSeconds) * width;
            
            Line mark = new Line
            {
                X1 = x, X2 = x,
                Y1 = 0, Y2 = height,
                Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeThickness = 1,
                Opacity = 1,
                IsHitTestVisible = false
            };
            RulerCanvas.Children.Add(mark);

            TextBlock label = new TextBlock
            {
                Text = $"{i:D2}:00",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DimGray,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 4);
            Canvas.SetTop(label, 2);
            RulerCanvas.Children.Add(label);
        }
    }

    private void UpdatePlayhead()
    {
        if (WaveformData == null || WaveformData.Duration.TotalSeconds == 0) return;

        double progress = CurrentPosition.TotalSeconds / WaveformData.Duration.TotalSeconds;
        double x = progress * ActualWidth * ZoomLevel;
        
        Playhead.Margin = new Thickness(x, 0, 0, 0);
        
        // Auto-scroll logic could go here if wrapped in a ScrollViewer
    }
}
