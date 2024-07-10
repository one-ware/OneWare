namespace OneWare.Vcd.Parser.Extensions;

sealed class StreamReadLimitLengthWrapper : Stream
{
    private readonly Stream _mInnerStream;

    public StreamReadLimitLengthWrapper(Stream innerStream, long size)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));

        _mInnerStream = innerStream;
        Length = _mInnerStream.Position + size;
    }

    public override bool CanRead => _mInnerStream.CanRead;

    public override bool CanSeek => _mInnerStream.CanSeek;

    public override bool CanWrite => false;

    public override void Flush()
    {
        _mInnerStream.Flush();
    }

    public override long Length { get; }

    public override long Position
    {
        get => _mInnerStream.Position;
        set => _mInnerStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = GetAllowedCount(count);
        return _mInnerStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _mInnerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override bool CanTimeout => _mInnerStream.CanTimeout;

    public override int ReadTimeout
    {
        get => _mInnerStream.ReadTimeout;
        set => _mInnerStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _mInnerStream.ReadTimeout;
        set => _mInnerStream.ReadTimeout = value;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        count = GetAllowedCount(count);
        return _mInnerStream.BeginRead(buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        throw new NotSupportedException();
    }

    public override void Close()
    {
        // Since this wrapper does not own the underlying stream, we do not want it to close the underlying stream
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return _mInnerStream.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return _mInnerStream.EndRead(asyncResult);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _mInnerStream.FlushAsync(cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        count = GetAllowedCount(count);
        return _mInnerStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override int ReadByte()
    {
        var count = GetAllowedCount(1);
        if (count == 0)
            return -1;

        return _mInnerStream.ReadByte();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override void WriteByte(byte value)
    {
        throw new NotSupportedException();
    }

    private int GetAllowedCount(int count)
    {
        var pos = _mInnerStream.Position;
        var maxCount = Length - pos;
        if (count > maxCount)
            count = (int)maxCount;
        return count;
    }
}