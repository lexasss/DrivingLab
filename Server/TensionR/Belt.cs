using System.IO.Ports;

namespace TensionR.API;

internal class Belt : IDisposable
{
    public bool IsConnected { get; private set; } = false;

    public event EventHandler<Exception>? Error;
    public event EventHandler<Communicator.RequestEventArgs>? RequestSent;
    public event EventHandler<In.Packet>? DataReceived;

    public Communicator Comm => _comm;

    public Belt(string portName)
    {
        _port = new SerialPort()
        {
            PortName = portName,
            BaudRate = 115200,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 100,
            WriteTimeout = 100,
        };

        _port.DataReceived += OnDataReceived;

        try
        {
            _port.Open();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            return;
        }

        _comm.Request += (s, e) =>
        {
            byte[] bytes = e.Packets.SelectMany(p => p.ToBytes()).ToArray();

            if (IsConnected)
                _port.Write(bytes, 0, bytes.Length);

            RequestSent?.Invoke(this, e);
        };
    }

    public void Dispose()
    {
        _port.Close();

        _comm.Dispose();
        _port.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Internal

    readonly SerialPort _port;
    readonly Communicator _comm = new();

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        int count = _port.BytesToRead;
        byte[] data = new byte[count];

        _port.Read(data, 0, count);

        try
        {
            var packets = In.Packet.FromRaw(data);
            foreach (var packet in packets)
            {
                if (!packet.IsEcho)
                    DataReceived?.Invoke(this, packet);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    #endregion
}
