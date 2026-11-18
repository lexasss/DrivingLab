namespace Server.Settings;

internal class Settings
{
    public CarlaSettings Carla { get; set; } = new();
}

internal record class CarlaSettings(
    string Ip = "127.0.0.1",
    int Port = 20777,
    bool IsEmulating = false,
    bool IsVerbose = false
);
