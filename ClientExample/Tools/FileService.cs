using Grpc.Core;

namespace ClientExample;

internal static class FileService
{
    public static async Task<Common.UploadResult> UploadFile(
        AsyncClientStreamingCall<Common.UploadRequest, Common.UploadResult> call,
        string filename,
        string type)
    {
        await call.RequestStream.WriteAsync(new Common.UploadRequest
        {
            Metadata = new Common.FileMetadata
            {
                FileName = System.IO.Path.GetFileName(filename),
                ContentType = $"{type}/{System.IO.Path.GetExtension(filename)}"
            }
        });

        await using var file = System.IO.File.OpenRead(filename);

        var buffer = new byte[Common.Constants.FILE_CHUNK_SIZE];

        int bytesRead;

        while ((bytesRead = await file.ReadAsync(buffer)) > 0)
        {
            await call.RequestStream.WriteAsync(new Common.UploadRequest
            {
                Chunk = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
            });
        }

        await call.RequestStream.CompleteAsync();

        return await call.ResponseAsync;
    }
}
