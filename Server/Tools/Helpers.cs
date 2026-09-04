using Grpc.Core;
using Microsoft.Extensions.Logging;
using Server.Tools;
using System.IO;

namespace Server;

internal class Helpers
{
    public static Task<Common.Bool> SetLogFileName<T>(
        string filename,
        string serviceName,
        FileLogger fileLogger,
        ILogger<T> serviceLogger)
    {
        if (string.IsNullOrEmpty(filename))
        {
            if (fileLogger.IsLogging)
            {
                serviceLogger.LogInformation($"[{serviceName}] Logging disabled");
                fileLogger.SetFileName(string.Empty);
            }
            return Task.FromResult(new Common.Bool() { Value = false });
        }
        else
        {
            var result = fileLogger.SetFileName(filename);
            if (result)
                serviceLogger.LogInformation($"[{serviceName}] Logging to {{filename}}", filename);
            else
                serviceLogger.LogWarning($"[{serviceName}] Cannot log to {{filename}}", filename);
            return Task.FromResult(new Common.Bool() { Value = result });
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

                    // Never trust a client-provided path.
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

            return new Common.UploadResult
            {
                FileName = metadata.FileName,
                Size = totalBytes
            };
        }
        finally
        {
            if (output is not null)
            {
                await output.DisposeAsync();
            }
        }
    }
}
