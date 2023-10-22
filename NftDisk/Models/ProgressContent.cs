using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Liuguang.NftDisk.Models;

public class ProgressContent : HttpContent
{
    private readonly byte[] _content;
    private readonly long _count;
    private Action<long, long> _progressHandler;

    public ProgressContent(byte[] content, Action<long, long> progressHandler)
    {
        ArgumentNullException.ThrowIfNull(content);

        _content = content;
        _count = content.LongLength;
        _progressHandler = progressHandler;
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _count;
        return true;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsyncCore(stream, default);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        return SerializeToStreamAsyncCore(stream, cancellationToken);
    }

    protected Task SerializeToStreamAsyncCore(Stream stream, CancellationToken cancellationToken)
    {
        return ProcessWriteAsync(stream, cancellationToken);
    }

    private async Task ProcessWriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        const long buffMaxSize = 4096;
        long offset = 0;
        while (offset < _count)
        {
            var buffSize = Math.Min(buffMaxSize, _count - offset);
            await stream.WriteAsync(_content.AsMemory((int)offset, (int)buffSize), cancellationToken);
            //
            offset += buffSize;
            _progressHandler.Invoke(_count, offset);
        }
    }
}