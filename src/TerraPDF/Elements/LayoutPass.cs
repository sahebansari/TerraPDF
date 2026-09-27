namespace TerraPDF.Elements;

/// <summary>
/// Identifies the publish operation running on the current thread, so elements can
/// memoise layout results (wrapped lines, table row heights) for its duration.
/// <para>
/// Caches are only valid inside one pass: between two publishes of the same document
/// a font may be registered or content changed. Outside any pass
/// <see cref="Current"/> is 0 and elements must not use their caches.
/// </para>
/// </summary>
internal static class LayoutPass
{
    private static int _lastId;

    [ThreadStatic]
    private static int _current;

    /// <summary>The id of the pass running on this thread, or 0 when none is.</summary>
    internal static int Current => _current;

    /// <summary>Starts a new pass on this thread; dispose the returned scope to end it.</summary>
    internal static Scope Begin()
    {
        var scope = new Scope(_current);
        _current = Interlocked.Increment(ref _lastId);
        return scope;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly int _previous;

        internal Scope(int previous) => _previous = previous;

        public void Dispose() => _current = _previous;
    }
}
