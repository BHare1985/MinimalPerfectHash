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

namespace MinimalPerfectHash;

[Serializable]
internal class CompressedSeq
{
    private uint[] _lengthRems;
    private uint _n;
    private uint _remR;
    private Select _sel;
    private uint[] _storeTable;
    private uint _totalLength;

    public CompressedSeq()
    {
    }

    internal CompressedSeq(ReadOnlySpan<uint> span)
    {
        var i = 0;
        var lengthRemsLength = span[i++];
        _lengthRems = new uint[lengthRemsLength];
        for (var j = 0; j != lengthRemsLength; j++)
            _lengthRems[j] = span[i++];

        _n = span[i++];
        _remR = span[i++];

        _sel = new Select(span.Slice(i));
        i += _sel.Size / sizeof(uint);

        var storeTableLength = span[i++];
        _storeTable = new uint[storeTableLength];
        for (var j = 0; j != storeTableLength; j++)
            _storeTable[j] = span[i++];

        _totalLength = span[i];
    }

    internal int Size => sizeof(uint) * (5 + _lengthRems.Length + _storeTable.Length) + _sel.Size;

    internal void Dump(Span<uint> span)
    {
        var i = 0;
        var lengthRemsLength = (uint)_lengthRems.Length;
        span[i++] = lengthRemsLength;
        for (var j = 0; j != lengthRemsLength; j++)
            span[i++] = _lengthRems[j];

        span[i++] = _n;
        span[i++] = _remR;

        _sel.Dump(span.Slice(i));
        i += _sel.Size / sizeof(uint);

        var storeTableLength = (uint)_storeTable.Length;
        span[i++] = storeTableLength;
        for (var j = 0; j != storeTableLength; j++)
            span[i++] = _storeTable[j];

        span[i] = _totalLength;
    }

    private static uint Log2(uint x)
    {
        uint res = 0;

        while (x > 1)
        {
            x >>= 1;
            res++;
        }

        return res;
    }

    public void Generate(uint[] valsTable, uint n)
    {
        uint i;
        // lengths: represents lengths of encoded values	
        var lengths = new uint[n];

        this._n = n;
        _totalLength = 0;

        for (i = 0; i < this._n; i++)
            if (valsTable[i] == 0)
            {
                lengths[i] = 0;
            }
            else
            {
                lengths[i] = Log2(valsTable[i] + 1);
                _totalLength += lengths[i];
            }

        _storeTable = new uint[(_totalLength + 31) >> 5];
        _totalLength = 0;

        for (i = 0; i < this._n; i++)
        {
            if (valsTable[i] == 0)
                continue;
            var storedValue = valsTable[i] - ((1U << (int)lengths[i]) - 1U);
            BitBool.SetBitsAtPos(_storeTable, _totalLength, storedValue, lengths[i]);
            _totalLength += lengths[i];
        }

        _remR = Log2(_totalLength / this._n);

        if (_remR == 0) _remR = 1;

        _lengthRems = new uint[(this._n * _remR + 0x1f) >> 5];

        var remsMask = (1U << (int)_remR) - 1U;
        _totalLength = 0;

        for (i = 0; i < this._n; i++)
        {
            _totalLength += lengths[i];
            BitBool.SetBitsValue(_lengthRems, i, _totalLength & remsMask, _remR, remsMask);
            lengths[i] = _totalLength >> (int)_remR;
        }

        _sel = new Select();

        _sel.Generate(lengths, this._n, _totalLength >> (int)_remR);
    }

    public uint Query(uint idx)
    {
        uint selRes;
        uint encIdx;
        var remsMask = (uint)((1 << (int)_remR) - 1);

        if (idx == 0)
        {
            encIdx = 0;
            selRes = _sel.Query(idx);
        }
        else
        {
            selRes = _sel.Query(idx - 1);
            encIdx = (selRes - (idx - 1)) << (int)_remR;
            encIdx += BitBool.GetBitsValue(_lengthRems, idx - 1, _remR, remsMask);
            selRes = _sel.NextQuery(selRes);
        }

        var encLength = (selRes - idx) << (int)_remR;
        encLength += BitBool.GetBitsValue(_lengthRems, idx, _remR, remsMask);
        encLength -= encIdx;
        if (encLength == 0) return 0;
        return BitBool.GetBitsAtPos(_storeTable, encIdx, encLength) + (uint)((1 << (int)encLength) - 1);
    }
}