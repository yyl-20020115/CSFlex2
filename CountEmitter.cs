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
	 * An emitter for an array encoded as count/value pairs in a string.
	 * 
	 * @author Gerwin Klein
	 * @version $Revision: 1.6 $, $Date: 2004/04/12 10:07:48 $
	 */
public class CountEmitter : PackEmitter
{
    /** number of entries in expanded array */
    private int total_entries;

    /** translate all values by this amount */
    private int translate = 0;

    /**
		 * Create a count/value emitter for a specific field.
		 * 
		 * @param name   name of the generated array
		 */
    protected internal CountEmitter(string name) : base(name)
    {
    }

    /**
		 * Emits count/value unpacking code for the generated array. 
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
        Println("    int [] result = new int[" + total_entries + "];");
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
            Println("    int i = 0;       /* index in packed string  */");
            Println("    int j = offset;  /* index in unpacked array */");
            Println("    int l = packed.Length;");
            Println("    while (i + 1 < l) {");
            Println("      int count = packed[i++];");
            Println("      int value = packed[i++];");
            if (translate == 1)
            {
                Println("      value--;");
            }
            else if (translate != 0)
            {
                Println("      value-= " + translate);
            }
            Println("      do result[j++] = value; while (--count > 0);");
            Println("    }");
            Println("    return j;");
            Println("  }");
        }
        else
        {
            Println("  private static int zzUnpack" + name + "(string packed, int offset, int [] result) {");
            Println("    int i = 0;       /* index in packed string  */");
            Println("    int j = offset;  /* index in unpacked array */");
            Println("    int l = packed.length();");
            Println("    while (i < l) {");
            Println("      int count = packed.charAt(i++);");
            Println("      int value = packed.charAt(i++);");
            if (translate == 1)
            {
                Println("      value--;");
            }
            else if (translate != 0)
            {
                Println("      value-= " + translate);
            }
            Println("      do result[j++] = value; while (--count > 0);");
            Println("    }");
            Println("    return j;");
            Println("  }");
        }
    }

    /**
		 * Translate all values by given amount.
		 * 
		 * Use to move value interval from [0, 0xFFFF] to something different.
		 * 
		 * @param i   amount the value will be translated by. 
		 *            Example: <code>i = 1</code> allows values in [-1, 0xFFFE].
		 */
    public void SetValTranslation(int i)
    {
        this.translate = i;
    }

    /**
		 * Emit one count/value pair. 
		 * 
		 * Automatically translates value by the <code>translate</code> value. 
		 * 
		 * @param count
		 * @param value
		 * 
		 * @see CountEmitter#setValTranslation(int)
		 */
    public void Emit(int count, int value)
    {
        total_entries += count;
        Breaks();
        EmitUC(count);
        EmitUC(value + translate);
    }
}