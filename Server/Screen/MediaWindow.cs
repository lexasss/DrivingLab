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
            object? content = null;
            double width = size?.Width ?? double.NaN;
            double height = size?.Height ?? double.NaN;
            SizeToContent sizeToContent = size switch
            {
                null => SizeToContent.WidthAndHeight,
                (0, 0) => SizeToContent.WidthAndHeight,
                (0, _) => SizeToContent.Width,
                (_, 0) => SizeToContent.Height,
                _ => SizeToContent.Manual
            };

            if (filename.ToLower().EndsWith("png"))
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(filename, UriKind.Absolute)),
                    Stretch = size == null ? Stretch.None :Stretch.Fill,
                };

                content = image;

                if (size == null)
                {
                    width = image.Source.Width;
                    height = image.Source.Height;
                }
            }
            else if (filename.ToLower().EndsWith("mp4"))
            {
                var media = new MediaElement
                {
                    Source = new Uri(filename, UriKind.Absolute),
                    LoadedBehavior = MediaState.Play,
                    UnloadedBehavior = MediaState.Stop,
                    Stretch = size == null ? Stretch.None : Stretch.Fill,
                };

                content = media;

                if (size == null)
                {
                    width = media.NaturalVideoWidth;
                    height = media.NaturalVideoHeight;
                }
            }

            _window = new Window
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

            var source = new WindowInteropHelper(_window);
            _window.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = source.Handle;

                long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                exStyle |= WS_EX_LAYERED;
                exStyle |= WS_EX_TRANSPARENT;

                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            };

            if (duration > 0)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(duration ?? 0);

                    _window.Dispatcher.Invoke(() =>
                    {
                        _window.Close();
                        Hidden?.Invoke(this, Id);
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                    });
                });
            }
            if (content is MediaElement mediaElement)
            {
                mediaElement.MediaEnded += (_, _) =>
                {
                    _window.Dispatcher.Invoke(() =>
                    {
                        _window.Close();
                        Hidden?.Invoke(this, Id);
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                    });
                };
            }

            _window.Show();

            // Keep the WPF dispatcher alive.
            System.Windows.Threading.Dispatcher.Run();
        });

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public void Close()
    {
        if (_window != null)
        {
            var window = _window;

            window.Dispatcher.Invoke(() =>
            {
                window.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        _thread = null;
        _window = null;
    }

    #region Internal

    Thread? _thread;
    Window? _window;

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
