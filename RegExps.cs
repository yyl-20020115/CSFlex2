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

namespace CSFlex;


    /**
 * Stores all rules of the specification for later access in RegExp -> NFA
 *
 * @author Gerwin Klein
 * @version JFlex 1.4, $Revision: 2.3 $, $Date: 2004/04/12 10:07:48 $
 * @author Jonathan Gilbert
 * @version CSFlex 1.4
 */
    public class RegExps
{

	/** the spec line in which a regexp is used */
	ArrayList /* of Integer */ lines;

	/** the lexical states in wich the regexp is used */
	ArrayList/* of ArrayList of Integer */ states;

	/** the regexp */
	ArrayList /* of RegExp */ regExps;

	/** the action of a regexp */
	ArrayList /* of Action */ actions;

	/** flag if it is a BOL regexp */
	ArrayList /* of Boolean */ BOL;

	/** the lookahead expression */
	ArrayList /* of RegExp */ look;

	public RegExps()
	{
		states = new ArrayList();
		regExps = new ArrayList();
		actions = new ArrayList();
		BOL = new ArrayList();
		look = new ArrayList();
		lines = new ArrayList();
	}

	public int Insert(int line, ArrayList stateList, RegExp regExp, Action action,
					   Boolean isBOL, RegExp lookAhead)
	{
		if (Options.DEBUG)
		{
			Out.Debug("Inserting regular expression with statelist :" + Out.NL + stateList);  //$NON-NLS-1$
			Out.Debug("and action code :" + Out.NL + action.content + Out.NL);     //$NON-NLS-1$
			Out.Debug("expression :" + Out.NL + regExp);  //$NON-NLS-1$
		}

		states.Add(stateList);
		regExps.Add(regExp);
		actions.Add(action);
		BOL.Add(isBOL);
		look.Add(lookAhead);
		lines.Add(line);

		return states.Count - 1;
	}

	public int Insert(ArrayList stateList, Action action)
	{

		if (Options.DEBUG)
		{
			Out.Debug("Inserting eofrule with statelist :" + Out.NL + stateList);   //$NON-NLS-1$
			Out.Debug("and action code :" + Out.NL + action.content + Out.NL);      //$NON-NLS-1$
		}

		states.Add(stateList);
		regExps.Add(null);
		actions.Add(action);
		BOL.Add(null);
		look.Add(null);
		lines.Add(null);

		return states.Count - 1;
	}

	public void AddStates(int regNum, ArrayList newStates)
	{
		IEnumerator  s = newStates.GetEnumerator();

		while (s.MoveNext())
			((ArrayList)states[regNum]).Add(s.Current);
	}

	public int GetNum()
	{
		return states.Count;
	}

	public bool? IsBOL(int num)
	{
		return (bool?)BOL[num];
	}

	public RegExp GetLookAhead(int num)
	{
		return (RegExp)look[num];
	}

	public bool IsEOF(int num)
	{
		return BOL[num] == null;
	}

	public ArrayList GetStates(int num)
	{
		return (ArrayList)states[num];
	}

	public RegExp GetRegExp(int num)
	{
		return (RegExp)regExps[num];
	}

	public int? GetLine(int num)
	{
		return (int?)lines[num];
	}

	public void CheckActions()
	{
		if (actions[actions.Count - 1] == null)
		{
			Out.Error(ErrorMessages.NO_LAST_ACTION);
			throw new GeneratorException();
		}
	}

	public Action GetAction(int num)
	{
		while (num < actions.Count && actions[num] == null)
			num++;

		return (Action)actions[num];
	}

	public int NFASize(Macros macros)
	{
		int size = 0;
		IEnumerator e = regExps.GetEnumerator();
		while (e.MoveNext())
		{
			RegExp r = (RegExp)e.Current;
			if (r != null) size += r.Size(macros);
		}
		e = look.GetEnumerator();
		while (e.MoveNext())
		{
			RegExp r = (RegExp)e.Current;
			if (r != null) size += r.Size(macros);
		}
		return size;
	}
}