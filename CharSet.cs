/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * C# Flex 1.4                                                             *
 * Copyright (C) 2004-2005  Jonathan Gilbert <logic@deltaq.org>            *
 * Derived from:                                                           *
 *                                                                         *
 *   JFlex 1.4                                                             *
 *   Copyright (C) 1998-2004  Gerwin Klein <lsf@jflex.de>                  *
 *   All rights reserved.                                                  *
 *                                                                         *
 * This program is free software; you can redistribute it and/or modify    *
 * it under the terms of the GNU General Public License. See the file      *
 * COPYRIGHT for more information.                                         *
 *                                                                         *
 * This program is distributed in the hope that it will be useful,         *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of          *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the           *
 * GNU General Public License for more details.                            *
 *                                                                         *
 * You should have received a copy of the GNU General Public License along *
 * with this program; if not, write to the Free Software Foundation, Inc., *
 * 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA                 *
 *                                                                         *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Text;

namespace CSFlex;

/**
	 * 
	 * @author Gerwin Klein 
	 * @version JFlex 1.4, $Revision: 2.1 $, $Date: 2004/04/12 10:07:47 $ 
	 * @author Jonathan Gilbert
	 * @version CSFlex 1.4
	 */
public class CharSet
{

    internal const int BITS = 6;           // the number of bits to shift (2^6 = 64)
    internal const int MOD = (1 << BITS) - 1;  // modulus

    internal long[] bits;

    private int elements;

    public CharSet()
    {
        this.bits = new long[1];
    }

    public CharSet(int initialSize, int character)
    {
        this.bits = new long[(initialSize >> BITS) + 1];
        Add(character);
    }

    public void Add(int character)
    {
        Resize(character);

        if ((bits[character >> BITS] & (1L << (character & MOD))) == 0) elements++;

        this.bits[character >> BITS] |= (1L << (character & MOD));
    }

    private int Nbits2size(int nbits) => (nbits >> BITS) + 1;

    private void Resize(int nbits)
    {
        int needed = Nbits2size(nbits);

        if (needed < bits.Length) return;

        long[] newbits = new long[Math.Max(bits.Length * 2, needed)];
        Array.Copy(this.bits, 0, newbits, 0, bits.Length);

        this.bits = newbits;
    }

    public bool IsElement(int character)
    {
        int index = character >> BITS;
        if (index >= bits.Length) return false;
        return (this.bits[index] & (1L << (character & MOD))) != 0;
    }

    public CharSetEnumerator Characters => new (this);

    public bool ContainsElements => elements > 0;

    public int Size => elements;

    public override string ToString()
    {
        var enum_chars = Characters;

        var result = new StringBuilder("{");

        if (enum_chars.HasMoreElements) result.Append(enum_chars.NextElement());

        while (enum_chars.HasMoreElements)
        {
            int i = enum_chars.NextElement();
            result.Append(", ").Append(i);
        }

        result.Append("}");

        return result.ToString();
    }
}