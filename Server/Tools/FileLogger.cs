using NAudio.Wave;
using System.IO;
using System.Media;
using System.Text;

namespace Server.Tools;

internal sealed class FileLogger : IDisposable
{
    public bool IsLogging => _writer != null;

    public bool SetFilename(string filename)
    {
        lock (_sync)
        {
            if (_disposed)
                return false;

            _writer?.Dispose();
            _writer = null;

            if (string.IsNullOrEmpty(filename))
                return false;

            var filePath = filename;
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(AppContext.BaseDirectory, DATA_FOLDER, filename);
            }

            var folder = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!);
            }

            try
            {
                var stream = new FileStream(
                    filePath,
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

    const string DATA_FOLDER = "data";

    private readonly Lock _sync = new();
    private StreamWriter? _writer;
    private bool _disposed = false;

    #endregion
}
