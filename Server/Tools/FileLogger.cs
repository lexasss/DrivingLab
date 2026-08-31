using System.Text;

namespace Server.Tools;

internal sealed class FileLogger : IDisposable
{
    public bool SetFilename(string filename)
    {
        lock (_sync)
        {
            if (_disposed || string.IsNullOrEmpty(filename))
                return false;

            _writer?.Dispose();
            _writer = null;

            try
            {
                var stream = new FileStream(
                    filename,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);

                _writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
            }
            catch (Exception)
            {
                // _writer will remain null
            }
        }

        return _writer != null;
    }

    public void Add(params object[] values)
    {
        if (_disposed || _writer is null)
            return;

        lock (_sync)
        {
            var line = string.Join('\t', values);
            _writer.Write(TimestampSource.Timestamp);
            _writer.Write('\t');
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
            _disposed = true;
        }
    }

    #region Internal

    private readonly Lock _sync = new();
    private StreamWriter? _writer;
    private bool _disposed = false;

    #endregion
}
