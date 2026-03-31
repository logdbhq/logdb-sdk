using System.IO;
using System.IO.Compression;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Internal compression service for handling compressed gRPC responses
    /// </summary>
    internal interface ICompressionService
    {
        byte[] Compress(byte[] data);
        byte[] Decompress(byte[] compressedData);
    }

    internal class CompressionService : ICompressionService
    {
        public byte[] Compress(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        public byte[] Decompress(byte[] compressedData)
        {
            using var inputStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}


