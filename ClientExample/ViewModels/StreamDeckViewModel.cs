using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

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
        new(Enumerable.Range(0, 10).Select(i => new ButtonState()));

    public StreamDeckViewModel(StreamDeckClient streamDeckClient)
    {
        _streamDeckClient = streamDeckClient;

        _streamDeckClient.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _streamDeckClient.IsConnected;

            var keyboard = _streamDeckClient.GetKeyboard();
            if (IsConnected && keyboard != null)
            {
                List<string> ids = [ALL_KEYS];
                for (int i = 0; i < keyboard.Count; i++)
                    ids.Add(i.ToString());
                KeyIds = ids.ToArray();
            }
            else
            {
                KeyIds = [];
            }

            KeyboardSize = keyboard != null ? $"{keyboard.Rows}x{keyboard.Columns}" : string.Empty;

            OnPropertyChanged(nameof(IsAvailable));
        };

        _streamDeckClient.ConnectionChanged += (s, e) => IsConnected = e;
        _streamDeckClient.KeyStateChanged += (s, e) =>
        {
            // UI represents 2x5 button fromthe left-top StreamDeck corner
            if (e.Id < 5)       // first row, left 5 buttons
            {
                Keys[e.Id].IsPressed = e.IsPressed;
            }
            else if (e.Id >= 8 && e.Id < 13)  // second row, left 5 buttons
            {
                Keys[e.Id - 3].IsPressed = e.IsPressed;
            }
        };
    }


    #region Internal

    const string ALL_KEYS = "All";
    readonly StreamDeckClient _streamDeckClient;

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
