using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ClientExample.Widgets;

public partial class Joystick : UserControl, INotifyPropertyChanged
{
    public Joystick()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(Point),
            typeof(Joystick),
            new FrameworkPropertyMetadata(new Point(0.5, 0.5), OnValueChanged));

    public Point Value
    {
        get => (Point)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty PointXProperty =
        DependencyProperty.Register(
            nameof(PointX),
            typeof(double),
            typeof(Joystick),
            new FrameworkPropertyMetadata(0.0));

    public double PointX
    {
        get => (double)GetValue(PointXProperty);
        set => SetValue(PointXProperty, value);
    }

    public static readonly DependencyProperty PointYProperty =
        DependencyProperty.Register(
            nameof(PointY),
            typeof(double),
            typeof(Joystick),
            new FrameworkPropertyMetadata(0.0));

    public double PointY
    {
        get => (double)GetValue(PointYProperty);
        set => SetValue(PointYProperty, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Internal

    private static void OnValueChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var widget = (Joystick)d;

        if (widget.canvas != null)
        {
            var point = (Point)e.NewValue;
            widget.PointX = (point.X + 1) * widget.canvas.ActualWidth / 2 - 2;
            widget.PointY = (point.Y + 1) * widget.canvas.ActualHeight / 2 - 2;
            widget.PropertyChanged?.Invoke(widget, new PropertyChangedEventArgs(nameof(PointX)));
            widget.PropertyChanged?.Invoke(widget, new PropertyChangedEventArgs(nameof(PointY)));
        }
    }

    #endregion
}
