using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ClientExample.Widgets;

public partial class PointOfView : UserControl, INotifyPropertyChanged
{
    public PointOfView()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(PointOfView),
            new FrameworkPropertyMetadata(0.0, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }


    public static readonly DependencyProperty HasValueProperty =
        DependencyProperty.Register(
            nameof(HasValue),
            typeof(bool),
            typeof(PointOfView));

    public bool HasValue
    {
        get => (bool)GetValue(HasValueProperty);
        private set => SetValue(HasValueProperty, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Internal

    private static void OnValueChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var widget = (PointOfView)d;

        var value = (double)e.NewValue;

        bool hasValue = !double.IsNaN(value) && !double.IsInfinity(value);

        if (!hasValue)
        {
            value = 0;
        }
        else
        {
            value %= 360;

            if (value < 0)
                value += 360;
        }

        widget.PropertyChanged?.Invoke(widget, new PropertyChangedEventArgs(nameof(Value)));

        if (hasValue != widget.HasValue)
        {
            widget.HasValue = hasValue;
            widget.PropertyChanged?.Invoke(widget, new PropertyChangedEventArgs(nameof(HasValue)));
        }
    }

    #endregion
}
