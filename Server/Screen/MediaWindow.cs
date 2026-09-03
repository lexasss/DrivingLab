using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Server.Screen;

internal sealed class MediaWindow
{
    public string Id { get; }
    public string Name { get; private set; } = string.Empty;

    public event EventHandler<bool>? Shown;
    public event EventHandler<string>? Hidden;

    public MediaWindow()
    {
        Id = Guid.NewGuid().ToString();
    }

    public void Show(string filename, Common.Point location, Common.Size? size, int? duration)
    {
        if (_thread != null)
            return;

        Name = System.IO.Path.GetFileNameWithoutExtension(filename);

        _thread = new Thread(() =>
        {
            double width = size?.Width ?? 0;
            double height = size?.Height ?? 0;
            SizeToContent sizeToContent = size switch
            {
                null or (0, 0) => SizeToContent.WidthAndHeight,
                (0, _) => SizeToContent.Width,
                (_, 0) => SizeToContent.Height,
                _ => SizeToContent.Manual
            };
            
            var content = CreateMedia(filename, ref width, ref height);
            if (content == null)
            {
                Shown?.Invoke(this, false);
                return;
            }

            _window = CreateWindow(content, location, width, height, sizeToContent);
            if (duration > 0)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(duration ?? 0, _cancellationTokenSource.Token);
                        Close(true);
                    }
                    finally
                    {
                        _cancellationTokenSource?.Dispose();
                        _cancellationTokenSource = null;
                    }
                });
            }

            _window.Show();

            Shown?.Invoke(this, true);

            // Keep the WPF dispatcher alive.
            System.Windows.Threading.Dispatcher.Run();
        });

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public void Close(bool invokeEvent = false)
    {
        if (_window != null)
        {
            var window = _window;
            window.Dispatcher.Invoke(() =>
            {
                _cancellationTokenSource?.Cancel();

                window.Close();
                if (invokeEvent)
                {
                    Hidden?.Invoke(this, Id);
                }
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        _thread = null;
        _window = null;
    }

    #region Internal

    Thread? _thread;
    Window? _window;
    CancellationTokenSource? _cancellationTokenSource;

    private object? CreateMedia(string filename, ref double width, ref double height)
    {
        object? result = null;

        var ext = System.IO.Path.GetExtension(filename)?.ToLower() ?? string.Empty;
        if (ext.Equals(".png") || ext.Equals(".jpg") || ext.Equals(".jpeg"))
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(filename, UriKind.Absolute)),
                Stretch = Stretch.Fill,
            };

            result = image;

            if (width == 0)
            {
                width = image.Source.Width;
                image.Width = width;
            }
            if (height == 0)
            {
                height = image.Source.Height;
                image.Height = height;
            }
        }
        else if (ext.Equals(".mp4") || ext.Equals(".avi") || ext.Equals(".wmv"))
        {
            var media = new MediaElement
            {
                Source = new Uri(filename, UriKind.Absolute),
                LoadedBehavior = MediaState.Play,
                UnloadedBehavior = MediaState.Stop,
                Stretch = Stretch.Fill,
            };
            media.MediaEnded += (_, _) =>
            {
                _window?.Dispatcher.Invoke(() => Close(true));
            };

            result = media;

            if (width == 0)
            {
                width = media.NaturalVideoWidth;
                media.Width = width;
            }
            if (height == 0)
            {
                height = media.NaturalVideoHeight;
                media.Height = height;
            }
        }

        if (width == 0)
            width = double.NaN;
        if (height == 0)
            height = double.NaN;

        return result;
    }

    private static Window CreateWindow(object content, Common.Point location, double width, double height, SizeToContent sizeToContent)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            IsHitTestVisible = false,
            Background = Brushes.Transparent,
            SizeToContent = sizeToContent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Content = content,
            Width = width,
            Height = height,
            Left = location.X,
            Top = location.Y,
        };

        var source = new WindowInteropHelper(window);
        window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = source.Handle;

            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            exStyle |= WS_EX_LAYERED;
            exStyle |= WS_EX_TRANSPARENT;

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        };

        return window;
    }

    #endregion

    #region WinAPI

    const int GWL_EXSTYLE = -20;

    const long WS_EX_LAYERED = 0x00080000L;
    const long WS_EX_TRANSPARENT = 0x00000020L;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long GetWindowLong(
        IntPtr hWnd,
        int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long SetWindowLong(
        IntPtr hWnd,
        int nIndex,
        long dwNewLong);

    #endregion
}
