namespace Gravelio.Api.Users;

/// Checks that an uploaded profile picture is a JPEG of a sane size and strips everything that is not needed to draw it.
/// There is no image library in the box, so this walks the JPEG's marker segments: the picture itself (quantisation and Huffman tables,
/// the frame header and the scan) is copied untouched, while the metadata segments (EXIF with its GPS position, XMP, comments, other
/// application data) are dropped. Nothing is decoded, so there is nothing to exploit in a decoder here; the browser that shows it
/// is bounded by the dimension limit.
public static class JpegPhoto
{
    public const int MaxBytes = 512 * 1024;
    public const int MaxSide = 1024;

    /// The cleaned picture, or null when the bytes are not an acceptable JPEG.
    public static byte[]? Clean(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length is < 4 or > MaxBytes || jpeg[0] != 0xFF || jpeg[1] != 0xD8) return null;
        var output = new List<byte>(jpeg.Length) { 0xFF, 0xD8 };
        bool frame = false;
        int i = 2;
        while (i + 1 < jpeg.Length)
        {
            if (jpeg[i] != 0xFF) return null;
            while (i + 1 < jpeg.Length && jpeg[i + 1] == 0xFF) i++; // fill bytes
            byte marker = jpeg[i + 1];
            i += 2;

            // Markers without a length: a restart, TEM, or the end of the image.
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7)) { output.AddRange([0xFF, marker]); continue; }
            if (marker == 0xD9) { output.AddRange([0xFF, 0xD9]); return frame ? [.. output] : null; }
            if (marker == 0xD8 || i + 2 > jpeg.Length) return null;

            int length = (jpeg[i] << 8) | jpeg[i + 1];
            if (length < 2 || i + length > jpeg.Length) return null;
            var segment = jpeg.Slice(i, length);

            bool isFrame = marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);
            if (isFrame)
            {
                if (segment.Length < 8) return null;
                int height = (segment[3] << 8) | segment[4], width = (segment[5] << 8) | segment[6];
                if (height is < 1 or > MaxSide || width is < 1 or > MaxSide) return null;
                frame = true;
            }

            if (marker == 0xDA)
            {
                // The scan: header plus entropy-coded data up to the end of the image. Copy it as it is.
                if (!frame) return null;
                output.AddRange([0xFF, marker]);
                for (int k = i; k < jpeg.Length; k++) output.Add(jpeg[k]);
                return output.Count >= 2 && jpeg[^2] == 0xFF && jpeg[^1] == 0xD9 ? [.. output] : null;
            }

            bool metadata = marker is >= 0xE1 and <= 0xEF or 0xFE; // APPn except JFIF's APP0, and comments
            if (!metadata)
            {
                output.AddRange([0xFF, marker]);
                for (int k = 0; k < length; k++) output.Add(segment[k]);
            }
            i += length;
        }
        return null;
    }
}
