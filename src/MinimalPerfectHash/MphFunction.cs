/* ........................................................................ *
 * (c) 2010 Laurent Dupuis (www.dupuis.me)                                  *
 * ........................................................................ *
 * < This program is free software: you can redistribute it and/or modify
 * < it under the terms of the GNU General Public License as published by
 * < the Free Software Foundation, either version 3 of the License, or
 * < (at your option) any later version.
 * <
 * < This program is distributed in the hope that it will be useful,
 * < but WITHOUT ANY WARRANTY; without even the implied warranty of
 * < MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * < GNU General Public License for more details.
 * <
 * < You should have received a copy of the GNU General Public License
 * < along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * ........................................................................ */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MinimalPerfectHash;

/// <summary>
///     Minimum Perfect Hash function class
/// </summary>
[Serializable]
public class MphFunction
{
    private readonly CompressedSeq cs;
    private readonly uint hashSeed;
    private readonly uint nBuckets;

    private MphFunction(CompressedSeq cs, uint hashSeed, uint maxValue, uint nBuckets)
    {
        this.cs = cs;
        this.hashSeed = hashSeed;
        this.MaxValue = maxValue;
        this.nBuckets = nBuckets;
    }

    private protected MphFunction()
    {
    }

    /// <summary>
    ///     Create a minimum perfect hash function for the provided key set
    /// </summary>
    /// <param name="loadFactor">Load factor (.5 &gt; c &gt; .99)</param>
    public MphFunction(IList<byte[]> keys, double loadFactor) : this(keys, (uint)keys.Count, loadFactor)
    {
    }

    /// <summary>
    ///     Create a minimum perfect hash function for the provided key set
    /// </summary>
    /// <param name="keySource">Key source</param>
    /// <param name="keyCount">Key count</param>
    /// <param name="loadFactor">Load factor (.5 &gt; c &gt; .99)</param>
    public MphFunction(IEnumerable<byte[]> keySource, uint keyCount, double loadFactor)
    {
        var buckets = new Buckets(keySource, keyCount, loadFactor);
        var dispTable = new uint[buckets.BucketCount];

        var iteration = 100;
        for (;; iteration--)
        {
            if (!buckets.MappingPhase(out hashSeed, out var maxBucketSize))
                throw new Exception("Mapping failure. Duplicate keys?");

            var sortedLists = buckets.OrderingPhase(maxBucketSize);
            var searchingSuccess = buckets.SearchingPhase(maxBucketSize, sortedLists, dispTable);

            if (searchingSuccess)
                break;

            if (iteration <= 0) throw new Exception("Too many iteration");
        }

        cs = new CompressedSeq();
        cs.Generate(dispTable, (uint)dispTable.Length);
        nBuckets = buckets.BucketCount;
        MaxValue = buckets.BinCount;
    }

    /// <summary>
    ///     Maximun value of the hash function.
    /// </summary>
    public uint MaxValue { get; }

    /// <summary>
    ///     The deep size of the entire object in bytes.
    /// </summary>
    public int Size => sizeof(uint) * 3 + cs.Size;

    /// <summary>
    ///     Compute the hash value associate with the key
    /// </summary>
    /// <param name="key">key from the original key set</param>
    /// <returns>Hash value (0 &gt; hash &gt; N)</returns>
    public uint GetHash(ReadOnlySpan<byte> key)
    {
        Span<uint> hl = stackalloc uint[3];
        JenkinsHash.HashVector(hashSeed, key, hl);
        var g = hl[0] % nBuckets;
        var f = hl[1] % MaxValue;
        var h = hl[2] % (MaxValue - 1) + 1;

        var disp = cs.Query(g);
        var probe0Num = disp % MaxValue;
        var probe1Num = disp / MaxValue;
        var position = (uint)((f + (ulong)h * probe0Num + probe1Num) % MaxValue);
        return position;
    }

    public byte[] Dump()
    {
        var bytes = new byte[Size];
        DumpInternal(bytes);
        return bytes;
    }

    public void Dump(Span<byte> bytes)
    {
        if (bytes.Length < Size)
            throw new ArgumentException("Span is shorter than the size of this funciton. See `Size` property.",
                nameof(bytes));
        DumpInternal(bytes);
    }

    private void DumpInternal(Span<byte> bytes)
    {
        var span = MemoryMarshal.Cast<byte, uint>(bytes);
        var i = 0;
        span[i++] = hashSeed;
        span[i++] = MaxValue;
        span[i++] = nBuckets;
        cs.Dump(span.Slice(i));
    }

    public static MphFunction Load(ReadOnlySpan<byte> bytes)
    {
        var span = MemoryMarshal.Cast<byte, uint>(bytes);
        var i = 0;
        var hashSeed = span[i++];
        var maxValue = span[i++];
        var nBuckets = span[i++];
        var cs = new CompressedSeq(span.Slice(i));
        return new MphFunction(cs, hashSeed, maxValue, nBuckets);
    }
}