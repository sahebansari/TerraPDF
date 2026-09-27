using System.Buffers.Binary;
using System.Text;

namespace TerraPDF.Drawing.TrueType;

/// <summary>
/// Stage-1 font subsetting: blanks <c>glyf</c> entries for glyphs a document
/// never shows, while keeping every glyph ID, the <c>loca</c> table's entry
/// count/format, and every other table completely untouched. No glyph ID is
/// ever renumbered, so <c>cmap</c>, <c>hmtx</c>, <c>GSUB</c> and the
/// <c>Identity-H</c>/<c>CIDToGIDMap /Identity</c> embedding all stay valid
/// with zero changes elsewhere in the pipeline.
/// <para>
/// The one subtlety this cannot skip: a glyph that <em>is</em> shown can be a
/// <b>composite glyph</b> (e.g. most accented Latin letters — é is commonly
/// "e" + a combining accent as two component glyph references, not one
/// self-contained outline). Blanking around only the directly-shown glyph
/// IDs would blank those components too, since nothing in a content stream
/// ever references them by ID directly — breaking the accent. So the kept
/// set is the closure of the requested glyphs under composite-component
/// references, computed here before anything is blanked.
/// </para>
/// </summary>
internal sealed partial class TrueTypeFont
{
    private const ushort CompArg1And2AreWords   = 0x0001;
    private const ushort CompWeHaveAScale       = 0x0008;
    private const ushort CompMoreComponents     = 0x0020;
    private const ushort CompWeHaveAnXAndYScale = 0x0040;
    private const ushort CompWeHaveATwoByTwo    = 0x0080;

    /// <summary>
    /// Returns a new sfnt font file containing exactly the same tables, glyph
    /// count, and glyph IDs as this font, but with every glyph outside the
    /// closure of <paramref name="usedGlyphIds"/> (plus glyph 0, <c>.notdef</c>,
    /// which every font must keep) blanked to a zero-length <c>glyf</c> entry.
    /// <see cref="RawData"/> itself is never modified; this returns a fresh
    /// array computed from the document's actual (whole-document-merged)
    /// glyph usage.
    /// </summary>
    internal byte[] BuildSubsetRawData(IReadOnlySet<ushort> usedGlyphIds)
    {
        var glyfTable = _tables["glyf"];
        var locaTable = _tables["loca"];

        uint[] oldLocaOffsets = ReadLocaOffsets(RawData, (int)locaTable.Offset, NumGlyphs, _indexToLocFormat);

        // Composite-component closure: BFS from the requested glyphs (+ .notdef)
        // so that every component a kept composite glyph references is kept too.
        var keep = new HashSet<ushort>(usedGlyphIds) { 0 };
        var queue = new Queue<ushort>(keep);
        while (queue.Count > 0)
        {
            ushort gid = queue.Dequeue();
            if (gid >= NumGlyphs) continue;
            uint start = oldLocaOffsets[gid], end = oldLocaOffsets[gid + 1];
            if (end <= start) continue; // already empty — nothing to walk

            foreach (ushort comp in CollectComponentGlyphIds(RawData, (int)glyfTable.Offset + (int)start, (int)(end - start)))
            {
                if (keep.Add(comp)) queue.Enqueue(comp);
            }
        }

        // Rebuild 'glyf': kept glyphs keep their original outline bytes (padded
        // to an even offset, as sfnt requires), blanked glyphs contribute nothing.
        // First pass lays out the new offsets, so the table is allocated once at its
        // exact size and filled with block copies.
        var newLocaOffsets = new uint[NumGlyphs + 1];
        uint glyfLength = 0;
        for (int gid = 0; gid < NumGlyphs; gid++)
        {
            newLocaOffsets[gid] = glyfLength;
            if (!keep.Contains((ushort)gid)) continue;

            uint start = oldLocaOffsets[gid], end = oldLocaOffsets[gid + 1];
            if (end <= start) continue;

            glyfLength += end - start;
            glyfLength += glyfLength & 1; // even-boundary padding between entries
        }
        newLocaOffsets[NumGlyphs] = glyfLength;

        var newGlyf = new byte[glyfLength]; // zero-initialised: padding bytes need no explicit fill
        for (int gid = 0; gid < NumGlyphs; gid++)
        {
            if (newLocaOffsets[gid + 1] == newLocaOffsets[gid]) continue; // blanked or empty
            uint start = oldLocaOffsets[gid];
            uint len = oldLocaOffsets[gid + 1] - start;
            Buffer.BlockCopy(RawData, (int)(glyfTable.Offset + start), newGlyf, (int)newLocaOffsets[gid], (int)len);
        }

        int locaEntrySize = _indexToLocFormat == 0 ? 2 : 4;
        byte[] newLocaBytes = new byte[(NumGlyphs + 1) * locaEntrySize];
        WriteLocaOffsets(newLocaBytes, newLocaOffsets, _indexToLocFormat);

        return RebuildSfnt(("glyf", newGlyf), ("loca", newLocaBytes));
    }

    /// <summary>
    /// <see langword="true"/> if <paramref name="glyphId"/> has a non-zero-length
    /// <c>glyf</c> entry in this font (i.e. it was kept by a previous
    /// <see cref="BuildSubsetRawData"/> call, or this font was never subset).
    /// Exposed for tests verifying subsetting/closure correctness; not used by
    /// the embedding pipeline itself.
    /// </summary>
    internal bool HasNonEmptyGlyphOutline(ushort glyphId)
    {
        if (glyphId >= NumGlyphs) return false;
        var locaTable = _tables["loca"];
        uint[] offsets = ReadLocaOffsets(RawData, (int)locaTable.Offset, NumGlyphs, _indexToLocFormat);
        return offsets[glyphId + 1] > offsets[glyphId];
    }

    /// <summary>
    /// Returns the component glyph IDs <paramref name="glyphId"/>'s <c>glyf</c>
    /// entry directly references (empty for a simple/blanked glyph). Exposed
    /// for tests verifying composite-glyph closure; not used by the embedding
    /// pipeline itself, which only needs the closure as a whole.
    /// </summary>
    internal List<ushort> GetComponentGlyphIds(ushort glyphId)
    {
        if (glyphId >= NumGlyphs) return [];
        var glyfTable = _tables["glyf"];
        var locaTable = _tables["loca"];
        uint[] offsets = ReadLocaOffsets(RawData, (int)locaTable.Offset, NumGlyphs, _indexToLocFormat);
        uint start = offsets[glyphId], end = offsets[glyphId + 1];
        return end <= start ? [] : CollectComponentGlyphIds(RawData, (int)glyfTable.Offset + (int)start, (int)(end - start));
    }

    /// <summary>
    /// Returns the component glyph IDs a composite <c>glyf</c> entry
    /// references (empty for a simple glyph, i.e. <c>numberOfContours &gt;= 0</c>,
    /// or an entry too short to contain even the glyph header).
    /// </summary>
    private static List<ushort> CollectComponentGlyphIds(byte[] data, int glyfEntryOffset, int glyfEntryLength)
    {
        var components = new List<ushort>();
        if (glyfEntryLength < 10) return components;

        short numberOfContours = ReadI16(data, glyfEntryOffset);
        if (numberOfContours >= 0) return components; // simple glyph: no component references

        int pos = glyfEntryOffset + 10;
        int end = glyfEntryOffset + glyfEntryLength;
        while (pos + 4 <= end)
        {
            ushort flags = ReadU16(data, pos);
            ushort glyphIndex = ReadU16(data, pos + 2);
            components.Add(glyphIndex);
            pos += 4;

            pos += (flags & CompArg1And2AreWords) != 0 ? 4 : 2;
            if ((flags & CompWeHaveAScale) != 0) pos += 2;
            else if ((flags & CompWeHaveAnXAndYScale) != 0) pos += 4;
            else if ((flags & CompWeHaveATwoByTwo) != 0) pos += 8;

            if ((flags & CompMoreComponents) == 0) break;
        }
        return components;
    }

    private static uint[] ReadLocaOffsets(byte[] data, int locaOffset, int numGlyphs, short indexToLocFormat)
    {
        var offsets = new uint[numGlyphs + 1];
        if (indexToLocFormat == 0)
        {
            for (int i = 0; i <= numGlyphs; i++)
                offsets[i] = ReadU16(data, locaOffset + i * 2) * 2u;
        }
        else
        {
            for (int i = 0; i <= numGlyphs; i++)
                offsets[i] = ReadU32(data, locaOffset + i * 4);
        }
        return offsets;
    }

    private static void WriteLocaOffsets(byte[] dest, uint[] offsets, short indexToLocFormat)
    {
        if (indexToLocFormat == 0)
        {
            for (int i = 0; i < offsets.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(dest.AsSpan(i * 2, 2), (ushort)(offsets[i] / 2));
        }
        else
        {
            for (int i = 0; i < offsets.Length; i++)
                BinaryPrimitives.WriteUInt32BigEndian(dest.AsSpan(i * 4, 4), offsets[i]);
        }
    }

    /// <summary>
    /// Rewrites the whole sfnt file with the given tables replaced by new
    /// byte content (everything else copied verbatim from <see cref="RawData"/>),
    /// recomputing every table offset (tables after a shrunk one shift back),
    /// every table checksum, and the <c>head</c> table's <c>checkSumAdjustment</c>
    /// — rather than trust the original file's checksums, which some font
    /// tools leave stale anyway.
    /// </summary>
    private byte[] RebuildSfnt(params (string Tag, byte[] NewBytes)[] replacements)
    {
        var replacementMap = replacements.ToDictionary(r => r.Tag, r => r.NewBytes);
        uint sfntVersion = ReadU32(RawData, 0);

        var orderedTags = _tables.OrderBy(kv => kv.Value.Offset).Select(kv => kv.Key).ToList();
        int numTables = orderedTags.Count;

        // Unchanged tables are referenced in place in RawData, not copied.
        var bodies = new ReadOnlyMemory<byte>[numTables];
        for (int i = 0; i < numTables; i++)
        {
            string tag = orderedTags[i];
            if (replacementMap.TryGetValue(tag, out var newBytes))
            {
                bodies[i] = newBytes;
            }
            else
            {
                var (off, len) = _tables[tag];
                bodies[i] = RawData.AsMemory((int)off, (int)len);
            }
        }

        int directorySize = 12 + numTables * 16;
        var newOffsets = new uint[numTables];
        var paddedLengths = new uint[numTables];
        uint cursor = (uint)directorySize;
        for (int i = 0; i < numTables; i++)
        {
            newOffsets[i] = cursor;
            uint len = (uint)bodies[i].Length;
            paddedLengths[i] = (len + 3) & ~3u;
            cursor += paddedLengths[i];
        }

        byte[] result = new byte[cursor]; // zero-initialised: table padding needs no explicit fill

        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(0, 4), sfntVersion);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4, 2), (ushort)numTables);
        int entrySelector = 0;
        while ((1 << (entrySelector + 1)) <= numTables) entrySelector++;
        int searchRange = (1 << entrySelector) * 16;
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(6, 2), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8, 2), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10, 2), (ushort)(numTables * 16 - searchRange));

        int headIndex = orderedTags.IndexOf("head");
        for (int i = 0; i < numTables; i++)
            bodies[i].Span.CopyTo(result.AsSpan((int)newOffsets[i]));

        // checkSumAdjustment must be zero while every table's checksum (including head's own) is computed.
        if (headIndex >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan((int)newOffsets[headIndex] + 8, 4), 0);

        for (int i = 0; i < numTables; i++)
        {
            uint checksum = ComputeChecksum(result, (int)newOffsets[i], paddedLengths[i]);
            int recPos = 12 + i * 16;
            Encoding.ASCII.GetBytes(orderedTags[i]).CopyTo(result, recPos);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(recPos + 4, 4), checksum);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(recPos + 8, 4), newOffsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(recPos + 12, 4), (uint)bodies[i].Length);
        }

        if (headIndex >= 0)
        {
            uint fileChecksum = ComputeChecksum(result, 0, (uint)result.Length);
            uint adjustment = 0xB1B0AFBAu - fileChecksum;
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan((int)newOffsets[headIndex] + 8, 4), adjustment);
        }

        return result;
    }

    /// <summary>sfnt table checksum: the big-endian-uint32-word sum over <paramref name="length"/> bytes (a multiple of 4).</summary>
    private static uint ComputeChecksum(byte[] data, int offset, uint length)
    {
        uint sum = 0;
        for (int pos = offset; pos < offset + length; pos += 4)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4));
        return sum;
    }
}
