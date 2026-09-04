using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Server.Tools;

internal class Helpers
{
    public static void ListFiles(
        string folder,
        string[] extensions,
        ILogger serviceLogger)
    {
        try
        {
            List<string> files = [];
            foreach (var ext in extensions)
            {
                foreach (var file in Directory.EnumerateFiles(folder, $"*{ext}"))
                {
                    files.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            int i = 0;
            foreach (var file in files)
            {
                if (++i == MAX_MEDIA_FILES_TO_LIST)
                {
                    serviceLogger.LogInformation(" ... [skipping other {count} files]",
                        files.Count - MAX_MEDIA_FILES_TO_LIST);
                    break;
                }
                serviceLogger.LogInformation("Found file {file}", file);
            }
        }
        catch
        {
            serviceLogger.LogWarning("Folder {folder} does not exist", folder);
        }
    }

    public static bool SetLogFileName(
        string filename,
        FileLogger fileLogger,
        ILogger serviceLogger)
    {
        if (string.IsNullOrEmpty(filename))
        {
            if (fileLogger.IsLogging)
            {
                serviceLogger.LogInformation("Logging disabled");
                fileLogger.SetFileName(string.Empty);
            }
            return false;
        }
        else
        {
            var result = fileLogger.SetFileName(filename);
            if (result)
                serviceLogger.LogInformation("Logging to {filename}", filename);
            else
                serviceLogger.LogWarning("Cannot log to {filename}", filename);
            return result;
        }
    }

    public static async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context,
        string folder)
    {
        Common.FileMetadata? metadata = null;
        FileStream? output = null;
        long totalBytes = 0;
        Common.UploadResult result;

        if (!Directory.Exists(folder))
        {
            var path = Path.Combine(AppContext.BaseDirectory, folder);
            Directory.CreateDirectory(path);
        }

        try
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var request = requestStream.Current;

                if (request.PayloadCase == Common.UploadRequest.PayloadOneofCase.Metadata)
                {
                    if (metadata is not null)
                    {
                        throw new RpcException(
                            new Status(
                                StatusCode.InvalidArgument,
                                "Metadata was already provided."));
                    }

                    metadata = request.Metadata;

                    var fileName = Path.GetFileName(metadata.FileName);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        throw new RpcException(
                            new Status(
                                StatusCode.InvalidArgument,
                                "Invalid file name."));
                    }

                    var filePath = Path.Combine(AppContext.BaseDirectory, folder, fileName);
                    output = new FileStream(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: Common.Constants.FILE_CHUNK_SIZE,
                        useAsync: true);
                }
                else if (request.PayloadCase == Common.UploadRequest.PayloadOneofCase.Chunk)
                {
                    if (output is null)
                    {
                        throw new RpcException(
                            new Status(
                                StatusCode.InvalidArgument,
                                "Metadata must be sent before file data."));
                    }

                    var chunk = request.Chunk;

                    await output.WriteAsync(
                        chunk.Memory,
                        context.CancellationToken);

                    totalBytes += chunk.Length;
                }
            }

            if (metadata is null || output is null)
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        "No file was uploaded."));
            }

            await output.FlushAsync(context.CancellationToken);

            result = new Common.UploadResult
            {
                ErrorMessage = string.Empty,
                Size = totalBytes
            };
        }
        catch (Exception ex)
        {
            result = new Common.UploadResult
            {
                ErrorMessage = ex.Message,
                Size = 0
            };
        }
        finally
        {
            if (output is not null)
            {
                await output.DisposeAsync();
            }
        }

        return result;
    }

    const int MAX_MEDIA_FILES_TO_LIST = 7;
}