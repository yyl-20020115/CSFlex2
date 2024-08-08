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

namespace CSFlex;


/**
	 * HiLowEmitter
	 * 
	 * @author Gerwin Klein
	 * @version $Revision: 1.5 $, $Date: 2004/04/12 10:07:48 $
	 * @author Jonathan Gilbert
	 * @version CSFlex 1.4
	 */
public class HiLowEmitter(string name) : PackEmitter(name)
{

    /** number of entries in expanded array */
    private int numEntries;

    /**
		 * Emits hi/low pair unpacking code for the generated array. 
		 * 
		 * @see CSFlex.PackEmitter#emitUnPack()
		 */
    public override void EmitUnpack()
    {

        if (Options.emit_csharp)
            Println(" 0 };"); // close array
        else
            Println("\";"); // close last string chunk:
        Nl();
        Println("  private static int [] zzUnpack" + name + "() {");
        Println("    int [] result = new int[" + numEntries + "];");
        Println("    int offset = 0;");

        for (int i = 0; i < chunks; i++)
        {
            Println("    offset = zzUnpack" + name + "(" + ConstName() + "_PACKED_" + i + ", offset, result);");
        }

        Println("    return result;");
        Println("  }");

        Nl();
        if (Options.emit_csharp)
        {
            Println("  private static int zzUnpack" + name + "(ushort[] packed, int offset, int [] result) {");
            Println("    int i = 0;  /* index in packed string  */");
            Println("    int j = offset;  /* index in unpacked array */");
            Println("    int l = packed.Length;");
            Println("    while (i + 1 < l) {");
            Println("      int high = packed[i++] << 16;");
            Println("      result[j++] = high | packed[i++];");
            Println("    }");
            Println("    return j;");
            Println("  }");
        }
        else
        {
            Println("  private static int zzUnpack" + name + "(string packed, int offset, int [] result) {");
            Println("    int i = 0;  /* index in packed string  */");
            Println("    int j = offset;  /* index in unpacked array */");
            Println("    int l = packed.length();");
            Println("    while (i < l) {");
            Println("      int high = packed.charAt(i++) << 16;");
            Println("      result[j++] = high | packed.charAt(i++);");
            Println("    }");
            Println("    return j;");
            Println("  }");
        }
    }

    /**
		 * Emit one value using two characters. 
		 *
		 * @param val  the value to emit
		 * @prec  0 <= val <= 0xFFFFFFFF 
		 */
    public void Emit(int val)
    {
        numEntries += 1;
        Breaks();
        EmitUC(val >> 16);
        EmitUC(val & 0xFFFF);
    }
}