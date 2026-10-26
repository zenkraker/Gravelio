namespace Gravelio.Api.Activities.Import;

/// Counts the bytes really read out of a zip's entries. A zip declares the size of each entry in a header that an attacker writes, so
/// the declared sizes cannot be trusted: a few kilobytes can expand to gigabytes. Open entries through <see cref="Open"/> and the
/// limits hold whatever the header claims.
public sealed class ZipBudget(long totalBytes, long entryBytes)
{
    long left = totalBytes;

    public Stream Open(System.IO.Compression.ZipArchiveEntry entry) => new Limited(entry.Open(), this, entryBytes);

    sealed class Limited(Stream inner, ZipBudget budget, long entryLimit) : Stream
    {
        long read;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            read += n;
            budget.left -= n;
            if (read > entryLimit || budget.left < 0) throw new InvalidActivityFileException("The zip expands to more data than this server accepts");
            return n;
        }

        public override int Read(Span<byte> buffer)
        {
            var n = inner.Read(buffer);
            read += n;
            budget.left -= n;
            if (read > entryLimit || budget.left < 0) throw new InvalidActivityFileException("The zip expands to more data than this server accepts");
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
