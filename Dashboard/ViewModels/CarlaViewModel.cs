using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;

namespace Dashboard;

public class FieldViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;

    public bool IsChecked
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CheckedChanged;
}

public partial class CarlaViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    public ObservableCollection<FieldViewModel> Fields { get; } = [];

    public CarlaViewModel(GrpcClients.CarlaClient carlaClient)
    {
        _client = carlaClient;
        _client.AvailabilityChanged += (s, e) =>
        {
            IsStreaming = _client.IsStreamingData;
            IsLogging = _client.IsLogging;

            OnPropertyChanged(nameof(IsAvailable));

            var selectedFields = _client.GetStreamingFields();

            foreach (var item in _client.Fields)
            {
                var vm = new FieldViewModel
                {
                    Name = item,
                    IsChecked = selectedFields.Contains(item)
                };
                vm.CheckedChanged += Field_CheckedChanged;
                Fields.Add(vm);
            }
        };
        _client.DataUpdated += (s, e) =>
        {
            if (_client.IsStreamingData)
                SetData(e);
        };
    }

    private void Field_CheckedChanged(object? sender, EventArgs e)
    {
        var checkedFields = Fields
            .Where(f => f.IsChecked)
            .Select(f => f.Name)
            .ToArray();
        _client.SetStreamingFields(checkedFields);
    }

    #region Internal

    readonly GrpcClients.CarlaClient _client;

    partial void OnIsStreamingChanging(bool oldValue, bool newValue)
    {
        if (oldValue)
        {
            _client.Stop();
            Data = string.Empty;
        }
        else
        {
            _client.Start();
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
        IsLogging = _client.IsLogging;
    }

    private void SetData(Carla.Data data)
    {
        var sb = new StringBuilder();
        foreach (var kv in data.Values)
            sb.AppendLine($"{kv.Key} = {kv.Value:F2}");
        Data = sb.ToString();
    }

    #endregion
}
