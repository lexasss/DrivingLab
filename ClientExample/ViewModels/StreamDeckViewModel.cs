using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClientExample;

public partial class StreamDeckViewModel : ObservableObject
{
    public bool IsAvailable => _streamDeckClient.IsAvailable;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSetKey))]
    public partial bool IsConnected { get; private set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string KeyboardSize { get; private set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUploadFile))]
    public partial string Filename { get; set; } = string.Empty;
    public bool CanUploadFile => IsAvailable && Filename.Length > 0;
    [ObservableProperty]
    public partial int Brightness { get; set; } = 100;
    [ObservableProperty]
    public partial string[] KeyIds { get; private set; } = [];
    [ObservableProperty]
    public partial string KeyId { get; set; } = "0";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SetKeyButtonText))]
    public partial string KeyFileNameOrColor { get; set; } = string.Empty;

    public bool CanSetKey => IsConnected;
    public string SetKeyButtonText => string.IsNullOrEmpty(KeyFileNameOrColor) ? "Clear" : "Set";
    public ObservableCollection<ButtonState> Keys { get; } = 
        new(Enumerable.Range(0, KEYBOARD_UI_ROWS * KEYBOARD_UI_COLUMNS)
            .Select(i => new ButtonState()));

    public StreamDeckViewModel(StreamDeckClient streamDeckClient)
    {
        _streamDeckClient = streamDeckClient;

        _streamDeckClient.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _streamDeckClient.IsConnected;

            _keyboard = _streamDeckClient.GetKeyboard();
            if (IsConnected && _keyboard != null)
            {
                List<string> ids = [ALL_KEYS];
                for (int i = 0; i < _keyboard.Count; i++)
                    ids.Add(i.ToString());
                KeyIds = ids.ToArray();
            }
            else
            {
                KeyIds = [];
            }

            KeyboardSize = _keyboard != null ? $"{_keyboard.Rows}x{_keyboard.Columns}" : string.Empty;

            OnPropertyChanged(nameof(IsAvailable));
        };

        _streamDeckClient.ConnectionChanged += (s, e) =>
        {
            IsConnected = e;
        };
        _streamDeckClient.KeyStateChanged += (s, e) =>
        {
            int keyboardCols = _keyboard?.Columns ?? KEYBOARD_UI_COLUMNS;
            int keyboardRows = _keyboard?.Rows ?? KEYBOARD_UI_ROWS;

            // UI represents buttons from the left-top StreamDeck corner
            if (e.Id < KEYBOARD_UI_COLUMNS)       // first row, left KEYBOARD_UI_COLUMNS buttons
            {
                Keys[e.Id].IsPressed = e.IsPressed;
            }
            else if (e.Id >= keyboardCols 
                  && e.Id < (keyboardCols + KEYBOARD_UI_COLUMNS))  // second row, left KEYBOARD_UI_COLUMNS buttons
            {
                Keys[e.Id - (keyboardCols - KEYBOARD_UI_COLUMNS)].IsPressed = e.IsPressed;
            }
        };
    }


    #region Internal

    const string ALL_KEYS = "All";
    const int KEYBOARD_UI_ROWS = 2;
    const int KEYBOARD_UI_COLUMNS = 5;

    readonly StreamDeckClient _streamDeckClient;

    StreamDeck.Keyboard? _keyboard;

    [RelayCommand]
    private async Task UploadFile()
    {
        if (!System.IO.File.Exists(Filename))
        {
            return;
        }

        Data = "Uploading file ...";
        try
        {
            var result = await _streamDeckClient.UploadFile(Filename);
            if (result.Size > 0)
            {
                Data = "File uploaded successfully.";
            }
        }
        catch
        {
            Data = "Failed to upload the file.";
        }
    }

    [RelayCommand]
    private void SetKey()
    {

        _streamDeckClient.SetKey(new StreamDeck.Key()
        {
            Id = KeyId.Equals(ALL_KEYS) ? -1 : int.Parse(KeyId),
            FileNameOrColor = KeyFileNameOrColor
        });
    }

    partial void OnBrightnessChanged(int value)
    {
        _streamDeckClient.SetBrightness(value);
    }

    #endregion
}
