﻿/* ........................................................................ *
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

namespace MPHTest.MPH
{
    internal unsafe class BitArray
    {
        readonly Byte[] _table;

        public BitArray(Int32 size)
        {
            _table = new Byte[size];
        }

        public Byte this[Int32 i]
        {
            get => _table[i];
	        set => _table[i] = value;
        }

        public Boolean GetBit(UInt64 i) 
        {
            fixed (Byte* ptrTable = &_table[0])
            {
                var i32OccupTable = (UInt32*) ptrTable;
	            return (i32OccupTable[i >> 5] & (1u << ((Int32)i & 0x0000001f)))!=0;
            }
        }

        public void SetBit(UInt32 i)
        {
            fixed (Byte* ptrTable = &_table[0])
            {
                var i32OccupTable = (UInt32*) ptrTable;
                i32OccupTable[i >> 5] |= 1u << ((Int32)i & 0x0000001f);
            }
        }

        public void UnSetBit(UInt32 i)
        {
            fixed (Byte* ptrTable = &_table[0])
            {
                var i32OccupTable = (UInt32*) ptrTable;
                i32OccupTable[i >> 5] ^= 1u << ((Int32)i & 0x0000001f);
            }
        }

        public void Zero()
        {
            Array.Clear(_table, 0, _table.Length);
        }
    }
}