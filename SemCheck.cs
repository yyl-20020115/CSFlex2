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
using System.Collections;
using System.Collections.Generic;

namespace CSFlex;


    /**
 * Performs simple semantic analysis on regular expressions.
 *
 * (used for checking if trailing contexts are legal)
 *
 * @author Gerwin Klein
 * @version JFlex 1.4, $Revision: 2.3 $, $Date: 2004/04/12 10:07:48 $
 * @author Jonathan Gilbert
 * @version CSFlex 1.4
 */
    public class SemCheck
{

	// stored globally since they are used as constants in all checks
	private static Macros macros;
	private static char maxChar;


	/**
	 * Performs semantic analysis for all expressions.
	 *
	 * Currently: illegal lookahead check only
	 * [fixme: more checks possible]
	 *
	 * @param rs   the reg exps to be checked
	 * @param m    the macro table (in expanded form)
	 * @param max  max character of the used charset (for negation)
	 * @param f    the spec file containing the rules [fixme]
	 */
	public static void Check(RegExps rs, Macros m, char max, File f)
	{
		macros = m;
		maxChar = max;

		bool errors = false;
		int num = rs.GetNum();
		for (int i = 0; i < num; i++)
		{
			RegExp r = rs.GetRegExp(i);
			RegExp l = rs.GetLookAhead(i);

			if (!CheckLookAhead(r, l))
			{
				errors = true;
				Out.Error(f, ErrorMessages.LOOKAHEAD_ERROR, rs.GetLine(i).GetValueOrDefault(), -1);
			}
		}

		if (errors) throw new GeneratorException();
	}


	/**
	 * Checks for illegal lookahead expressions. 
	 * 
	 * Lookahead in C# Flex only works when the first expression has fixed
	 * length or when the intersection of the last set of the first expression
	 * and the first set of the second expression is empty.
	 *
	 * @param r1   first regexp
	 * @param r2   second regexp (the lookahead)
	 *
	 * @return true iff C# Flex can generate code for the lookahead expression
	 */
	private static bool CheckLookAhead(RegExp r1, RegExp r2)
	{
		return r2 == null || Length(r1) > 0 || !(Last(r1).And(First(r2)).ContainsElements());
	}


	/**
	 * Returns length if expression has fixed length, -1 otherwise.   
	 */
	private static int Length(RegExp re)
	{
		RegExp2 r;

		switch (re.type)
		{

			case SymbolContants.BAR:
				{
					r = (RegExp2)re;
					int l1 = Length(r.r1);
					if (l1 < 0) return -1;
					int l2 = Length(r.r2);

					if (l1 == l2)
						return l1;
					else
						return -1;
				}

			case SymbolContants.CONCAT:
				{
					r = (RegExp2)re;
					int l1 = Length(r.r1);
					if (l1 < 0) return -1;
					int l2 = Length(r.r2);
					if (l2 < 0) return -1;
					return l1 + l2;
				}

			case SymbolContants.STAR:
			case SymbolContants.PLUS:
			case SymbolContants.QUESTION:
				return -1;

			case SymbolContants.CCLASS:
			case SymbolContants.CCLASSNOT:
			case SymbolContants.CHAR:
				return 1;

			case SymbolContants.STRING:
				{
					string content = (string)((RegExp1)re).content;
					return content.Length;
				}

			case SymbolContants.MACROUSE:
				return Length(macros.GetDefinition((string)((RegExp1)re).content));
		}

		throw new Exception("Unkown expression type " + re.type + " in " + re);   //$NON-NLS-1$ //$NON-NLS-2$
	}


	/**
	 * Returns true iff the matched language contains epsilon
	 */
	private static bool ContainsEpsilon(RegExp re)
	{
		RegExp2 r;

		switch (re.type)
		{

			case SymbolContants.BAR:
				r = (RegExp2)re;
				return ContainsEpsilon(r.r1) || ContainsEpsilon(r.r2);

			case SymbolContants.CONCAT:
				r = (RegExp2)re;
				if (ContainsEpsilon(r.r1))
					return ContainsEpsilon(r.r2);
				else
					return false;

			case SymbolContants.STAR:
			case SymbolContants.QUESTION:
				return true;

			case SymbolContants.PLUS:
				return ContainsEpsilon((RegExp)((RegExp1)re).content);

			case SymbolContants.CCLASS:
			case SymbolContants.CCLASSNOT:
			case SymbolContants.CHAR:
				return false;

			case SymbolContants.STRING:
				return ((string)((RegExp1)re).content).Length <= 0;

			case SymbolContants.MACROUSE:
				return ContainsEpsilon(macros.GetDefinition((string)((RegExp1)re).content));
		}

		throw new Exception("Unkown expression type " + re.type + " in " + re); //$NON-NLS-1$ //$NON-NLS-2$
	}


	/**
	 * Returns the first set of an expression. 
	 *
	 * (the first-character-projection of the language)
	 */
	private static IntCharSet First(RegExp re)
	{
		RegExp2 r;

		switch (re.type)
		{

			case SymbolContants.BAR:
				r = (RegExp2)re;
				return First(r.r1).Add(First(r.r2));

			case SymbolContants.CONCAT:
				r = (RegExp2)re;
				if (ContainsEpsilon(r.r1))
					return First(r.r1).Add(First(r.r2));
				else
					return First(r.r1);

			case SymbolContants.STAR:
			case SymbolContants.PLUS:
			case SymbolContants.QUESTION:
				return First((RegExp)((RegExp1)re).content);

			case SymbolContants.CCLASS:
				return new IntCharSet((List<Interval>)((RegExp1)re).content);

			case SymbolContants.CCLASSNOT:
				var all = new IntCharSet(new Interval((char)0, maxChar));
				var set = new IntCharSet((List<Interval>)((RegExp1)re).content);
				all.Sub(set);
				return all;

			case SymbolContants.CHAR:
				return new IntCharSet((char)((RegExp1)re).content);

			case SymbolContants.STRING:
				string content = (string)((RegExp1)re).content;
				if (content.Length > 0)
					return new IntCharSet(content[0]);
				else
					return new IntCharSet();

			case SymbolContants.MACROUSE:
				return First(macros.GetDefinition((string)((RegExp1)re).content));
		}

		throw new Exception("Unkown expression type " + re.type + " in " + re); //$NON-NLS-1$ //$NON-NLS-2$
	}


	/**
	 * Returns the last set of the expression
	 *
	 * (the last-charater-projection of the language)
	 */
	private static IntCharSet Last(RegExp re)
	{

		RegExp2 r;

		switch (re.type)
		{

			case SymbolContants.BAR:
				r = (RegExp2)re;
				return Last(r.r1).Add(Last(r.r2));

			case SymbolContants.CONCAT:
				r = (RegExp2)re;
				if (ContainsEpsilon(r.r2))
					return Last(r.r1).Add(Last(r.r2));
				else
					return Last(r.r2);

			case SymbolContants.STAR:
			case SymbolContants.PLUS:
			case SymbolContants.QUESTION:
				return Last((RegExp)((RegExp1)re).content);

			case SymbolContants.CCLASS:
				return new IntCharSet((List<Interval>)((RegExp1)re).content);

			case SymbolContants.CCLASSNOT:
				var all = new IntCharSet(new Interval((char)0, maxChar));
				var set = new IntCharSet((List<Interval>)((RegExp1)re).content);
				all.Sub(set);
				return all;

			case SymbolContants.CHAR:
				return new IntCharSet((char)((RegExp1)re).content);

			case SymbolContants.STRING:
				string content = (string)((RegExp1)re).content;
				if (content.Length > 0)
					return new IntCharSet(content[content.Length - 1]);
				else
					return new IntCharSet();

			case SymbolContants.MACROUSE:
				return Last(macros.GetDefinition((string)((RegExp1)re).content));
		}

		throw new Exception("Unkown expression type " + re.type + " in " + re); //$NON-NLS-1$ //$NON-NLS-2$
	}
}