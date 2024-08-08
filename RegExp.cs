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

namespace CSFlex;



/**
 * Stores a regular expression of rules section in a C# Flex specification.
 *
 * This base class has no content other than its type. 
 *
 * @author Gerwin Klein
 * @version JFlex 1.4, $Revision: 2.3 $, $Date: 2004/04/12 10:07:47 $
 * @author Jonathan Gilbert
 * @version CSFlex 1.4
 */
public class RegExp
{

    /**
	 * The type of the regular expression. This field will be
	 * filled with values from class sym.java (generated by cup)
	 */
    internal int type;


    /**
	 * Create a new regular expression of the specified type.
	 *
	 * @param type   a value from the cup generated class sym.
	 *
	 * @see CSFlex.sym
	 */
    public RegExp(int type)
    {
        this.type = type;
    }



    /**
	 * Returns a string-representation of this regular expression
	 * with the specified indentation.
	 *
	 * @param tab   a string that should contain only space characters and
	 *              that is inserted in front of standard string-representation
	 *              pf this object.
	 */
    public virtual string Print(string tab)
    {
        return tab + ToString();
    }


    /**
	 * Returns a string-representation of this regular expression
	 */
    public override string ToString()
    {
        return "type = " + type;
    }


    /**
	 * Find out if this regexp is a char class or equivalent to one.
	 * 
	 * @param  macros  for macro expansion
	 * @return true if the regexp is equivalent to a char class.
	 */
    public bool IsCharClass(Macros macros)
    {
        RegExp1 unary;
        RegExp2 binary;

        switch (type)
        {
            case SymbolContants.CHAR:
            case SymbolContants.CHAR_I:
            case SymbolContants.CCLASS:
            case SymbolContants.CCLASSNOT:
                return true;

            case SymbolContants.BAR:
                binary = (RegExp2)this;
                return binary.r1.IsCharClass(macros) && binary.r2.IsCharClass(macros);

            case SymbolContants.MACROUSE:
                unary = (RegExp1)this;
                return macros.GetDefinition((string)unary.content).IsCharClass(macros);

            default: return false;
        }
    }

    /**
	 * The approximate number of NFA states this expression will need (only 
	 * works correctly after macro expansion and without negation)
	 * 
	 * @param macros  macro table for expansion   
	 */
    public int Size(Macros macros)
    {
        RegExp1 unary;
        RegExp2 binary;
        RegExp content;

        switch (type)
        {
            case SymbolContants.BAR:
                binary = (RegExp2)this;
                return binary.r1.Size(macros) + binary.r2.Size(macros) + 2;

            case SymbolContants.CONCAT:
                binary = (RegExp2)this;
                return binary.r1.Size(macros) + binary.r2.Size(macros);

            case SymbolContants.STAR:
                unary = (RegExp1)this;
                content = (RegExp)unary.content;
                return content.Size(macros) + 2;

            case SymbolContants.PLUS:
                unary = (RegExp1)this;
                content = (RegExp)unary.content;
                return content.Size(macros) + 2;

            case SymbolContants.QUESTION:
                unary = (RegExp1)this;
                content = (RegExp)unary.content;
                return content.Size(macros);

            case SymbolContants.BANG:
                unary = (RegExp1)this;
                content = (RegExp)unary.content;
                return content.Size(macros) * content.Size(macros);
            // this is only a very rough estimate (worst case 2^n)
            // exact size too complicated (propably requires construction)

            case SymbolContants.TILDE:
                unary = (RegExp1)this;
                content = (RegExp)unary.content;
                return content.Size(macros) * content.Size(macros) * 3;
            // see sym.BANG

            case SymbolContants.STRING:
            case SymbolContants.STRING_I:
                unary = (RegExp1)this;
                return ((string)unary.content).Length + 1;

            case SymbolContants.CHAR:
            case SymbolContants.CHAR_I:
                return 2;

            case SymbolContants.CCLASS:
            case SymbolContants.CCLASSNOT:
                return 2;

            case SymbolContants.MACROUSE:
                unary = (RegExp1)this;
                return macros.GetDefinition((string)unary.content).Size(macros);
        }

        throw new Exception("unknown regexp type " + type);
    }
}