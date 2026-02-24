using CrosshairFlex.Desktop.Interop;
using CrosshairFlex.Desktop.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;

namespace CrosshairFlex.Desktop;

public partial class OverlayWindow : Window
{
    private int _lastRenderKey;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
            style |= NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExToolWindow;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(style));
        };
    }

    public void ApplyBoundsToVirtualScreen()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public void RenderProfile(CrosshairProfile profile)
    {
        var hash = new HashCode();
        hash.Add(Width);
        hash.Add(Height);
        hash.Add(profile.Shape);
        hash.Add(profile.Size);
        hash.Add(profile.Thickness);
        hash.Add(profile.Gap);
        hash.Add(profile.PumpCornerRounding);
        hash.Add(profile.Red);
        hash.Add(profile.Green);
        hash.Add(profile.Blue);
        hash.Add(profile.Opacity);
        hash.Add(profile.CustomPngPath ?? string.Empty);
        var renderKey = hash.ToHashCode();

        if (renderKey == _lastRenderKey)
        {
            return;
        }

        _lastRenderKey = renderKey;
        RootCanvas.Children.Clear();

        var centerX = Width / 2;
        var centerY = Height / 2;
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(Math.Clamp(profile.Opacity, 0.0, 1.0) * 255), profile.Red, profile.Green, profile.Blue));
        var size = Math.Max(1, profile.Size);
        var thickness = Math.Max(1, profile.Thickness);
        var gap = Math.Max(0, profile.Gap);

        switch (profile.Shape)
        {
            case CrosshairShape.Plus:
                AddPlus(centerX, centerY, size, gap, thickness, brush);
                break;
            case CrosshairShape.Dot:
                AddDot(centerX, centerY, thickness * 2, brush);
                break;
            case CrosshairShape.Circle:
                AddCircle(centerX, centerY, size, thickness, brush);
                break;
            case CrosshairShape.Pump:
                AddPump(centerX, centerY, size, gap, profile.PumpCornerRounding, thickness, brush);
                break;
            case CrosshairShape.CustomPng:
                if (!AddPng(centerX, centerY, size, profile.CustomPngPath ?? string.Empty))
                {
                    AddPlus(centerX, centerY, size, gap, thickness, brush);
                }
                break;
        }
    }

    public void InvalidateRenderState()
    {
        _lastRenderKey = 0;
    }

    private void AddPlus(double centerX, double centerY, double size, double gap, double thickness, Brush brush)
    {
        AddLine(centerX - gap - size, centerY, centerX - gap, centerY, thickness, brush);
        AddLine(centerX + gap, centerY, centerX + gap + size, centerY, thickness, brush);
        AddLine(centerX, centerY - gap - size, centerX, centerY - gap, thickness, brush);
        AddLine(centerX, centerY + gap, centerX, centerY + gap + size, thickness, brush);
    }

    private void AddPump(double centerX, double centerY, double size, double gap, double rounding, double thickness, Brush brush)
    {
        var half = Math.Max(10, size);
        var left = centerX - half;
        var right = centerX + half;
        var top = centerY - half;
        var bottom = centerY + half;

        var radius = Math.Clamp(rounding, 0, half - 2);
        var maxGapHalf = Math.Max(2, half - radius - 2);
        var gapHalf = Math.Clamp(gap / 2.0, 2, maxGapHalf);

        AddLine(left + radius, top, centerX - gapHalf, top, thickness, brush);
        AddLine(centerX + gapHalf, top, right - radius, top, thickness, brush);
        AddLine(left + radius, bottom, centerX - gapHalf, bottom, thickness, brush);
        AddLine(centerX + gapHalf, bottom, right - radius, bottom, thickness, brush);
        AddLine(left, top + radius, left, centerY - gapHalf, thickness, brush);
        AddLine(left, centerY + gapHalf, left, bottom - radius, thickness, brush);
        AddLine(right, top + radius, right, centerY - gapHalf, thickness, brush);
        AddLine(right, centerY + gapHalf, right, bottom - radius, thickness, brush);

        if (radius > 0)
        {
            AddArc(left + radius, top, left, top + radius, thickness, brush, SweepDirection.Counterclockwise);
            AddArc(right - radius, top, right, top + radius, thickness, brush, SweepDirection.Clockwise);
            AddArc(right, bottom - radius, right - radius, bottom, thickness, brush, SweepDirection.Clockwise);
            AddArc(left, bottom - radius, left + radius, bottom, thickness, brush, SweepDirection.Counterclockwise);
        }
        else
        {
            AddLine(left, top, right, top, thickness, brush);
            AddLine(left, bottom, right, bottom, thickness, brush);
            AddLine(left, top, left, bottom, thickness, brush);
            AddLine(right, top, right, bottom, thickness, brush);
        }

        AddDot(centerX, centerY, Math.Max(2, thickness * 0.95), brush);
    }

    private void AddDot(double centerX, double centerY, double radius, Brush brush)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = brush
        };

        Canvas.SetLeft(ellipse, centerX - radius);
        Canvas.SetTop(ellipse, centerY - radius);
        RootCanvas.Children.Add(ellipse);
    }

    private void AddCircle(double centerX, double centerY, double radius, double thickness, Brush brush)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = System.Windows.Media.Brushes.Transparent
        };

        Canvas.SetLeft(ellipse, centerX - radius);
        Canvas.SetTop(ellipse, centerY - radius);
        RootCanvas.Children.Add(ellipse);
    }

    private bool AddPng(double centerX, double centerY, double size, string path)
    {
        var resolvedPath = ResolvePngPath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return false;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(resolvedPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Width = size * 2,
                Height = size * 2,
                Source = bitmap
            };

            Canvas.SetLeft(image, centerX - image.Width / 2);
            Canvas.SetTop(image, centerY - image.Height / 2);
            RootCanvas.Children.Add(image);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolvePngPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var normalized = rawPath.Trim().Trim('"');
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            return absoluteUri.LocalPath;
        }

        if (System.IO.Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return System.IO.Path.GetFullPath(normalized, Environment.CurrentDirectory);
    }

    private void AddLine(double x1, double y1, double x2, double y2, double thickness, Brush brush)
    {
        var line = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            StrokeThickness = thickness,
            Stroke = brush,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        RootCanvas.Children.Add(line);
    }

    private void AddArc(double startX, double startY, double endX, double endY, double thickness, Brush brush, SweepDirection sweepDirection)
    {
        var radius = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
        var figure = new PathFigure
        {
            StartPoint = new System.Windows.Point(startX, startY),
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new System.Windows.Point(endX, endY),
            Size = new System.Windows.Size(radius, radius),
            SweepDirection = sweepDirection,
            IsLargeArc = false
        });

        var path = new System.Windows.Shapes.Path
        {
            Data = new PathGeometry([figure]),
            StrokeThickness = thickness,
            Stroke = brush,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        RootCanvas.Children.Add(path);
    }
}
