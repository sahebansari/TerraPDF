namespace TerraPDF.Drawing;

/// <summary>
/// Process-wide cache of PNG images already converted to PDF image streams (Flate-compressed
/// RGB plus optional Flate-compressed alpha), keyed by <see cref="ImageSource.ContentKey"/>.
/// </summary>
/// <remarks>
/// A service typically renders the same logo into every document it produces. Decoding an
/// RGBA PNG and compressing its pixels again for each document dominated the cost of such
/// documents and churned the large object heap. The streams are cached before encryption
/// (which is applied per object afterwards), and compression is deterministic, so a cached
/// entry yields exactly the bytes a fresh conversion would.
/// <para>
/// Bounded by <see cref="MaxBytes"/> of cached stream data; the least recently used entries
/// are evicted first. Images larger than a quarter of the budget are never cached.
/// </para>
/// </remarks>
internal static class EncodedImageCache
{
    /// <summary>Upper bound on the total size of cached streams.</summary>
    internal const long MaxBytes = 32L * 1024 * 1024;

    /// <summary>A PNG converted for embedding: compressed RGB samples and, when transparent, compressed alpha.</summary>
    internal sealed record Entry(byte[] CompressedRgb, byte[]? CompressedAlpha)
    {
        internal long Size => CompressedRgb.Length + (CompressedAlpha?.Length ?? 0);
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, LinkedListNode<(string Key, Entry Entry)>> Map = new();
    private static readonly LinkedList<(string Key, Entry Entry)> Recency = new();   // most recent first
    private static long _bytes;

    /// <summary>
    /// Returns the cached conversion of <paramref name="image"/>, creating it with
    /// <paramref name="convert"/> on a miss. Conversion runs outside the lock, so two
    /// documents converting the same new image at once may both do the work once.
    /// </summary>
    internal static Entry GetOrAdd(ImageSource image, Func<ImageSource, Entry> convert)
    {
        string key = image.ContentKey;
        lock (Gate)
        {
            if (Map.TryGetValue(key, out var node))
            {
                Recency.Remove(node);
                Recency.AddFirst(node);
                return node.Value.Entry;
            }
        }

        var entry = convert(image);
        if (entry.Size > MaxBytes / 4)
            return entry;

        lock (Gate)
        {
            if (Map.TryGetValue(key, out var existing))
                return existing.Value.Entry;

            Map[key] = Recency.AddFirst((key, entry));
            _bytes += entry.Size;
            while (_bytes > MaxBytes && Recency.Last is { } oldest)
            {
                Recency.RemoveLast();
                Map.Remove(oldest.Value.Key);
                _bytes -= oldest.Value.Entry.Size;
            }
        }
        return entry;
    }

    /// <summary>True when the image with this content key is cached (for tests).</summary>
    internal static bool Contains(string contentKey)
    {
        lock (Gate) return Map.ContainsKey(contentKey);
    }

    /// <summary>Total size of the cached streams (for tests).</summary>
    internal static long Bytes
    {
        get { lock (Gate) return _bytes; }
    }

    /// <summary>Removes one image from the cache (for tests).</summary>
    internal static void Remove(string contentKey)
    {
        lock (Gate)
        {
            if (Map.Remove(contentKey, out var node))
            {
                Recency.Remove(node);
                _bytes -= node.Value.Entry.Size;
            }
        }
    }
}
