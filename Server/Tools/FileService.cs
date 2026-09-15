using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Server.Tools;

internal class FileService
{
    public static async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context,
        string folder,
        ILogger logger)
    {
        var filename = requestStream.Current?.Metadata?.FileName;
        var result = await UploadFile(requestStream, context, folder);

        if (result.Size > 0)
            logger.LogInformation("Uploaded {name} ({size} bytes)",
                filename, result.Size);
        else
            logger.LogError("Failed to upload {name}: {error}",
                filename, result.ErrorMessage);

        return result;
    }

    public static string? FileNameToPath(
        string filename,
        string folder,
        ILogger? logger)
    {
        if (string.IsNullOrEmpty(filename))
            return null;

        string filePath = filename;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, folder, filePath);
        }

        if (!File.Exists(filePath))
        {
            logger?.LogWarning("File not found: {filename}", filePath);
            return null;
        }

        return filePath;
    }

    public static void ListFiles(
        string folder,
        string[] extensions,
        ILogger logger)
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
                if (++i == MAX_FILES_TO_LIST)
                {
                    logger.LogInformation(" ... [skipping other {count} files]",
                        files.Count - MAX_FILES_TO_LIST);
                    break;
                }
                logger.LogInformation("Found file {file}", file);
            }
        }
        catch
        {
            logger.LogWarning("Folder {folder} does not exist", folder);
        }
    }

    #region Internal

    const int MAX_FILES_TO_LIST = 7;


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

    #endregion
}