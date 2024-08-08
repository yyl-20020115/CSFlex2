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
using System.IO;
using System.Text;

namespace CSFlex;


/**
 * NFA representation in CSFlex.
 *
 * Contains algorithms RegExp -> NFA and NFA -> DFA.
 *
 * @author Gerwin Klein
 * @version JFlex 1.4, $Revision: 2.7 $, $Date: 2004/04/12 10:07:47 $
 * @author Jonathan Gilbert
 * @version CSFlex 1.4
 */
public class NFA
{

    // table[current_state][next_char] is the set of states that can be reached
    // from current_state with an input next_char
    internal StateSet[][] table;

    // epsilon[current_state] is the set of states that can be reached
    // from current_state via epsilon-edges
    internal StateSet[] epsilon;

    // isFinal[state] == true <=> state is a final state of the NFA
    internal bool[] isFinal;

    // isPushback[state] == true <=> state is the final state of a regexp that
    // should only be matched when followed by a certain lookahead.
    internal bool[] isPushback;

    // action[current_state]: the action associated with the state 
    // current_state (null, if there is no action for the state)
    internal Action[] action;

    // the number of states in this NFA
    internal int numStates;

    // the current maximum number of input characters
    internal int numInput;

    // the number of lexical States. Lexical states have the indices
    // 0..numLexStates-1 in the transition table
    internal int numLexStates;

    // estimated size of the NFA (before actual construction)
    internal int estSize = 256;

    internal Macros macros;
    internal CharClasses classes;

    internal LexScan scanner;
    internal RegExps regExps;

    // will be reused by several methods (avoids excessive object creation)
    private static StateSetEnumerator states = new StateSetEnumerator();
    private static StateSet tempStateSet = new StateSet();

    public NFA(int numInput, int estSize)
    {
        this.numInput = numInput;
        this.estSize = estSize;
        numStates = 0;
        epsilon = new StateSet[estSize];
        action = new Action[estSize];
        isFinal = new bool[estSize];
        isPushback = new bool[estSize];
        table = new StateSet[estSize][];
        for (int i = 0; i < table.Length; i++)
            table[i] = new StateSet[numInput];
    }

    public NFA(int numInput, LexScan scanner, RegExps regExps,
               Macros macros, CharClasses classes)
      : this(numInput, regExps.NFASize(macros) + 2 * scanner.states.Number())
    {
        this.scanner = scanner;
        this.regExps = regExps;
        this.macros = macros;
        this.classes = classes;

        numLexStates = scanner.states.Number();

        EnsureCapacity(2 * numLexStates);

        numStates = 2 * numLexStates;
    }

    public void AddStandaloneRule()
    {
        // standalone rule has least priority, fires
        // transition on all characters and has "print it rule"    
        int start = numStates;
        int end = numStates + 1;

        for (int c = 0; c < classes.ClassesCount; c++)
            AddTransition(start, c, end);

        for (int i = 0; i < numLexStates * 2; i++)
            AddEpsilonTransition(i, start);

        action[end] = new Action("System.out.print(yytext());", int.MaxValue);
        isFinal[end] = true;
    }


    public void AddRegExp(int regExpNum)
    {

        if (Options.DEBUG)
            Out.Debug("Adding nfa for regexp " + regExpNum + " :" + Out.NL + regExps.GetRegExp(regExpNum));

        IntPair nfa = InsertNFA(regExps.GetRegExp(regExpNum));

        IEnumerator lexStates = regExps.GetStates(regExpNum).GetEnumerator();

        if (!lexStates.MoveNext())
            lexStates = scanner.states.GetInclusiveStates();

        lexStates.Reset();

        while (lexStates.MoveNext())
        {
            int stateNum = (int)lexStates.Current;

            if (!regExps.IsBOL(regExpNum).GetValueOrDefault())
                AddEpsilonTransition(2 * stateNum, nfa.start);

            AddEpsilonTransition(2 * stateNum + 1, nfa.start);
        }


        if (regExps.GetLookAhead(regExpNum) != null)
        {
            IntPair look = InsertNFA(regExps.GetLookAhead(regExpNum));

            AddEpsilonTransition(nfa.end, look.start);

            Action a = regExps.GetAction(regExpNum);
            a.IsLookAction = true;

            isPushback[nfa.end] = true;
            action[look.end] = a;
            isFinal[look.end] = true;
        }
        else
        {
            action[nfa.end] = regExps.GetAction(regExpNum);
            isFinal[nfa.end] = true;
        }
    }


    private void EnsureCapacity(int newNumStates)
    {
        int oldLength = epsilon.Length;

        if (newNumStates < oldLength) return;

        int newStatesLength = Math.Max(oldLength * 2, newNumStates);

        bool[] newFinal = new bool[newStatesLength];
        bool[] newIsPush = new bool[newStatesLength];
        Action[] newAction = new Action[newStatesLength];
        StateSet[][] newTable = new StateSet[newStatesLength][];
        for (int i = 0; i < newTable.Length; i++)
            newTable[i] = new StateSet[numInput];
        StateSet[] newEpsilon = new StateSet[newStatesLength];

        Array.Copy(isFinal, 0, newFinal, 0, numStates);
        Array.Copy(isPushback, 0, newIsPush, 0, numStates);
        Array.Copy(action, 0, newAction, 0, numStates);
        Array.Copy(epsilon, 0, newEpsilon, 0, numStates);
        Array.Copy(table, 0, newTable, 0, numStates);

        isFinal = newFinal;
        isPushback = newIsPush;
        action = newAction;
        epsilon = newEpsilon;
        table = newTable;
    }

    public void AddTransition(int start, int input, int dest)
    {
        Out.Debug("Adding transition (" + start + ", " + input + ", " + dest + ")");

        int maxS = Math.Max(start, dest) + 1;

        EnsureCapacity(maxS);

        if (maxS > numStates) numStates = maxS;

        if (table[start][input] != null)
            table[start][input].AddState(dest);
        else
            table[start][input] = new StateSet(estSize, dest);
    }

    public void AddEpsilonTransition(int start, int dest)
    {
        int max = Math.Max(start, dest) + 1;
        EnsureCapacity(max);
        if (max > numStates) numStates = max;

        if (epsilon[start] != null)
            epsilon[start].AddState(dest);
        else
            epsilon[start] = new StateSet(estSize, dest);
    }


    /**
	 * Returns <code>true</code>, iff the specified set of states
	 * contains a final state.
	 *
	 * @param set   the set of states that is tested for final states.
	 */
    private bool ContainsFinal(StateSet set)
    {
        states.Reset(set);

        while (states.HasMoreElements())
            if (isFinal[states.NextElement()]) return true;

        return false;
    }


    /**
	 * Returns <code>true</code>, iff the specified set of states
	 * contains a pushback-state.
	 *
	 * @param set   the set of states that is tested for pushback-states.
	 */
    private bool ContainsPushback(StateSet set)
    {
        states.Reset(set);

        while (states.HasMoreElements())
            if (isPushback[states.NextElement()]) return true;

        return false;
    }


    /**
	 * Returns the action with highest priority in the specified 
	 * set of states.
	 *
	 * @param set  the set of states for which to determine the action
	 */
    private Action GetAction(StateSet set)
    {

        states.Reset(set);

        Action maxAction = null;

        Out.Debug("Determining action of : " + set);

        while (states.HasMoreElements())
        {

            Action currentAction = action[states.NextElement()];

            if (currentAction != null)
            {
                if (maxAction == null)
                    maxAction = currentAction;
                else
                    maxAction = maxAction.GetHigherPriority(currentAction);
            }

        }

        return maxAction;
    }


    /**
	 * Calculates the epsilon closure for a specified set of states.
	 *
	 * The epsilon closure for set a is the set of states that can be reached 
	 * by epsilon edges from a.
	 *
	 * @param set the set of states to calculate the epsilon closure for
	 *
	 * @return the epsilon closure of the specified set of states 
	 *         in this NFA
	 */
    private StateSet Closure(int startState)
    {

        // Out.debug("Calculating closure of "+set);

        StateSet notvisited = tempStateSet;
        StateSet closure = new StateSet(numStates, startState);

        notvisited.Clear();
        notvisited.AddState(startState);

        while (notvisited.containsElements())
        {
            // Out.debug("closure is now "+closure);
            // Out.debug("notvisited is "+notvisited);
            int state = notvisited.GetAndRemoveElement();
            // Out.debug("removed element "+state+" of "+notvisited);
            // Out.debug("epsilon[states] = "+epsilon[state]);
            notvisited.Add(closure.Complement(epsilon[state]));
            closure.Add(epsilon[state]);
        }

        // Out.debug("Closure is : "+closure);

        return closure;
    }

    /**
	 * Returns the epsilon closure of a set of states
	 */
    private StateSet Closure(StateSet startStates)
    {
        StateSet result = new StateSet(numStates);

        if (startStates != null)
        {
            states.Reset(startStates);
            while (states.HasMoreElements())
                result.Add(Closure(states.NextElement()));
        }

        return result;
    }


    private void EpsilonFill()
    {
        for (int i = 0; i < numStates; i++)
        {
            epsilon[i] = Closure(i);
        }
    }

    /**
	 * Calculates the set of states that can be reached from another
	 * set of states <code>start</code> with an specified input 
	 * character <code>input</code>
	 *
	 * @param start the set of states to start from
	 * @param input the input character for which to search the next states
	 *
	 * @return the set of states that are reached from <code>start</code> 
	 *         via <code>input</code>
	 */
    private StateSet DFAEdge(StateSet start, char input)
    {
        // Out.debug("Calculating DFAEdge for state set "+start+" and input '"+input+"'");

        tempStateSet.Clear();

        states.Reset(start);
        while (states.HasMoreElements())
            tempStateSet.Add(table[states.NextElement()][input]);

        StateSet result = new StateSet(tempStateSet);

        states.Reset(tempStateSet);
        while (states.HasMoreElements())
            result.Add(epsilon[states.NextElement()]);

        // Out.debug("DFAEdge is : "+result);

        return result;
    }


    /**
	 * Returns an DFA that accepts the same language as this NFA.
	 * This DFA is usualy not minimal.
	 */
    public DFA GetDFA()
    {

        PrettyHashtable<StateSet, int> dfaStates = new(numStates);
        PrettyList<StateSet> dfaVector = new(numStates);

        var dfa = new DFA(2 * numLexStates, numInput);

        int numDFAStates = 0;
        int currentDFAState = 0;

        Out.Println("Converting NFA to DFA : ");

        EpsilonFill();

        StateSet currentState, newState;

        for (int i = 0; i < 2 * numLexStates; i++)
        {
            newState = epsilon[i];

            dfaStates[newState] = numDFAStates;
            dfaVector.Add(newState);

            dfa.SetLexState(i, numDFAStates);

            dfa.SetFinal(numDFAStates, ContainsFinal(newState));
            dfa.SetPushback(numDFAStates, ContainsPushback(newState));
            dfa.SetAction(numDFAStates, GetAction(newState));

            numDFAStates++;
        }

        numDFAStates--;

        if (Options.DEBUG)
            Out.Debug("DFA start states are :" + Out.NL + dfaStates + Out.NL + Out.NL + "ordered :" + Out.NL + dfaVector);

        currentDFAState = 0;

        StateSet tempStateSet = NFA.tempStateSet;
        StateSetEnumerator states = NFA.states;

        // will be reused
        newState = new StateSet(numStates);

        while (currentDFAState <= numDFAStates)
        {

            currentState = (StateSet)dfaVector[currentDFAState];

            for (char input = (char)0; input < numInput; input++)
            {

                // newState = DFAEdge(currentState, input);

                // inlining DFAEdge for performance:

                // Out.debug("Calculating DFAEdge for state set "+currentState+" and input '"+input+"'");

                tempStateSet.Clear();
                states.Reset(currentState);
                while (states.HasMoreElements())
                    tempStateSet.Add(table[states.NextElement()][input]);

                newState.copy(tempStateSet);

                states.Reset(tempStateSet);
                while (states.HasMoreElements())
                    newState.Add(epsilon[states.NextElement()]);

                // Out.debug("DFAEdge is : "+newState);


                if (newState.containsElements())
                {

                    // Out.debug("DFAEdge for input "+(int)input+" and state set "+currentState+" is "+newState);

                    // Out.debug("Looking for state set "+newState);
                    if (dfaStates.TryGetValue(newState, out int nextDFAState))
                    {
                        // Out.debug("FOUND!");
                        dfa.AddTransition(currentDFAState, input, nextDFAState);
                    }
                    else
                    {
                        if (Options.progress) Out.Print(".");
                        // Out.debug("NOT FOUND!");
                        // Out.debug("Table was "+dfaStates);
                        numDFAStates++;

                        // make a new copy of newState to store in dfaStates
                        StateSet storeState = new StateSet(newState);

                        dfaStates[storeState] = numDFAStates;
                        dfaVector.Add(storeState);

                        dfa.AddTransition(currentDFAState, input, numDFAStates);
                        dfa.SetFinal(numDFAStates, ContainsFinal(storeState));
                        dfa.SetPushback(numDFAStates, ContainsPushback(storeState));
                        dfa.SetAction(numDFAStates, GetAction(storeState));
                    }
                }
            }

            currentDFAState++;
        }

        if (Options.verbose) Out.Println("");

        return dfa;
    }


    public void DumpTable()
    {
        Out.Dump(ToString());
    }

    public override string ToString()
    {
        StringBuilder result = new StringBuilder();

        for (int i = 0; i < numStates; i++)
        {
            result.Append("State");
            if (isFinal[i]) result.Append("[FINAL]");
            if (isPushback[i]) result.Append(" [PUSHBACK]");
            result.AppendFormat(" {0}{1}", i, Out.NL);

            for (char input = (char)0; input < numInput; input++)
            {
                if (table[i][input] != null && table[i][input].containsElements())
                    result.AppendFormat("  with {0} in {1}{2}", (int)input, table[i][input], Out.NL);


            }

            if (epsilon[i] != null && epsilon[i].containsElements())
                result.Append("  with epsilon in " + epsilon[i] + Out.NL);
        }

        return result.ToString();
    }

    public void WriteDot(File file)
    {
        try
        {
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine(DotFormat());
            writer.Close();
        }
        catch (IOException)
        {
            Out.Error(ErrorMessages.FILE_WRITE, file);
            throw new GeneratorException();
        }
    }

    public string DotFormat()
    {
        StringBuilder result = new StringBuilder();

        result.Append("digraph NFA {").Append(Out.NL);
        result.Append("rankdir = LR").Append(Out.NL);

        for (int i = 0; i < numStates; i++)
        {
            if (isFinal[i] || isPushback[i]) result.Append(i);
            if (isFinal[i]) result.Append(" [shape = doublecircle]");
            if (isPushback[i]) result.Append(" [shape = box]");
            if (isFinal[i] || isPushback[i]) result.Append(Out.NL);
        }

        for (int i = 0; i < numStates; i++)
        {
            for (int input = 0; input < numInput; input++)
            {
                if (table[i][input] != null)
                {
                    StateSetEnumerator states = table[i][input].states();

                    while (states.HasMoreElements())
                    {
                        int s = states.NextElement();
                        result.AppendFormat("{0} -> {1}", i, s);
                        result.AppendFormat(" [label=\"{0}\"]{1}", classes.ToString(input), Out.NL);
                    }
                }
            }
            if (epsilon[i] != null)
            {
                StateSetEnumerator states = epsilon[i].states();
                while (states.HasMoreElements())
                {
                    int s = states.NextElement();
                    result.AppendFormat("{0} -> {1} [style=dotted]{2}", i, s, Out.NL);
                }
            }
        }

        result.Append("}").Append(Out.NL);

        return result.ToString();
    }


    //-----------------------------------------------------------------------
    // Functions for constructing NFAs out of regular expressions.

    private void InsertLetterNFA(bool caseless, char letter, int start, int end)
    {
        if (caseless)
        {
            int lower = classes.GetClassCode(char.ToLower(letter));
            int upper = classes.GetClassCode(char.ToUpper(letter));
            AddTransition(start, lower, end);
            if (upper != lower) AddTransition(start, upper, end);
        }
        else
        {
            AddTransition(start, classes.GetClassCode(letter), end);
        }
    }

    private IntPair InsertStringNFA(bool caseless, string letters)
    {
        int start = numStates;
        int i;

        for (i = 0; i < letters.Length; i++)
        {
            if (caseless)
            {
                char c = letters[i];
                int lower = classes.GetClassCode(char.ToLower(c));
                int upper = classes.GetClassCode(char.ToUpper(c));
                AddTransition(i + start, lower, i + start + 1);
                if (upper != lower) AddTransition(i + start, upper, i + start + 1);
            }
            else
            {
                AddTransition(i + start, classes.GetClassCode(letters[i]), i + start + 1);
            }
        }

        return new IntPair(start, i + start);
    }


    private void InsertClassNFA(List<Interval> intervalls, int start, int end)
    {
        // empty char class is ok:
        if (intervalls == null) return;

        int[] cl = classes.GetClassCodes(intervalls);
        for (int i = 0; i < cl.Length; i++)
            AddTransition(start, cl[i], end);
    }

    private void InsertNotClassNFA(List<Interval> intervalls, int start, int end)
    {
        int[] cl = classes.GetNotClassCodes(intervalls);

        for (int i = 0; i < cl.Length; i++)
            AddTransition(start, cl[i], end);
    }


    /**
	 * Constructs an NFA accepting the complement of the language
	 * of a given NFA.
	 *
	 * Converts the NFA into a DFA, then negates that DFA.
	 * Exponential state blowup possible and common.
	 *
	 * @param the NFA to construct the complement for.
	 *
	 * @return a pair of integers denoting the index of start
	 *         and end state of the complement NFA.
	 */
    private IntPair Complement(IntPair nfa)
    {

        if (Options.DEBUG)
        {
            Out.Debug("complement for " + nfa);
            Out.Debug("NFA is :" + Out.NL + this);
        }

        int dfaStart = nfa.end + 1;

        // fixme: only need epsilon closure of states reachable from nfa.start
        EpsilonFill();

        PrettyHashtable<StateSet, int> dfaStates = new(numStates);
        PrettyList<StateSet> dfaVector = new(numStates);

        int numDFAStates = 0;
        int currentDFAState = 0;

        StateSet currentState, newState;

        newState = epsilon[nfa.start];
        dfaStates[newState] = numDFAStates;
        dfaVector.Add(newState);

        if (Options.DEBUG)
            Out.Debug("pos DFA start state is :" + Out.NL + dfaStates + Out.NL + Out.NL + "ordered :" + Out.NL + dfaVector);

        currentDFAState = 0;

        while (currentDFAState <= numDFAStates)
        {

            currentState = (StateSet)dfaVector[currentDFAState];

            for (char input = (char)0; input < numInput; input++)
            {
                newState = DFAEdge(currentState, input);

                if (newState.containsElements())
                {

                    // Out.debug("DFAEdge for input "+(int)input+" and state set "+currentState+" is "+newState);

                    // Out.debug("Looking for state set "+newState);

                    if (dfaStates.TryGetValue(newState, out int nextDFAState))
                    {
                        // Out.debug("FOUND!");
                        AddTransition(dfaStart + currentDFAState, input, dfaStart + nextDFAState);
                    }
                    else
                    {
                        if (Options.dump) Out.Print("+");
                        // Out.debug("NOT FOUND!");
                        // Out.debug("Table was "+dfaStates);
                        numDFAStates++;

                        dfaStates[newState] = numDFAStates;
                        dfaVector.Add(newState);

                        AddTransition(dfaStart + currentDFAState, input, dfaStart + numDFAStates);
                    }
                }
            }

            currentDFAState++;
        }

        // We have a dfa accepting the positive regexp. 

        // Now the complement:    
        if (Options.DEBUG)
            Out.Debug("dfa finished, nfa is now :" + Out.NL + this);

        int start = dfaStart + numDFAStates + 1;
        int error = dfaStart + numDFAStates + 2;
        int end = dfaStart + numDFAStates + 3;

        AddEpsilonTransition(start, dfaStart);

        for (int i = 0; i < numInput; i++)
            AddTransition(error, i, error);

        AddEpsilonTransition(error, end);

        for (int s = 0; s <= numDFAStates; s++)
        {
            currentState = (StateSet)dfaVector[s];

            currentDFAState = dfaStart + s;

            // if it was not a final state, it is now in the complement
            if (!currentState.IsElement(nfa.end))
                AddEpsilonTransition(currentDFAState, end);

            // all inputs not present (formerly leading to an implicit error)
            // now lead to an explicit (final) state accepting everything.
            for (int i = 0; i < numInput; i++)
                if (table[currentDFAState][i] == null)
                    AddTransition(currentDFAState, i, error);
        }

        // eliminate transitions leading to dead states
        if (live == null || live.Length < numStates)
        {
            live = new bool[2 * numStates];
            visited = new bool[2 * numStates];
        }

        _end = end;
        _dfaStates = dfaVector;
        _dfaStart = dfaStart;
        RemoveDead(dfaStart);

        if (Options.DEBUG)
            Out.Debug("complement finished, nfa (" + start + "," + end + ") is now :" + this);

        return new IntPair(start, end);
    }

    // "global" data for use in method removeDead only:
    // live[s] == false <=> no final state can be reached from s
    private bool[] live;    // = new boolean [estSize];
    private bool[] visited; // = new boolean [estSize];
    private int _end; // final state of original nfa for dfa (nfa coordinates)
    private List<StateSet> _dfaStates;
    private int _dfaStart; // in nfa coordinates

    private void RemoveDead(int start)
    {
        // Out.debug("removeDead ("+start+")");

        if (visited[start] || live[start]) return;
        visited[start] = true;

        // Out.debug("not yet visited");

        if (Closure(start).IsElement(_end))
            live[start] = true;

        // Out.debug("is final :"+live[start]);

        for (int i = 0; i < numInput; i++)
        {
            StateSet nextState = Closure(table[start][i]);
            StateSetEnumerator states = nextState.states();
            while (states.HasMoreElements())
            {
                int next = states.NextElement();

                if (next != start)
                {
                    RemoveDead(next);

                    if (live[next])
                        live[start] = true;
                    else
                        table[start][i] = null;
                }
            }
        }

        StateSet _nextState = Closure(epsilon[start]);
        StateSetEnumerator _states = _nextState.states();
        while (_states.HasMoreElements())
        {
            int next = _states.NextElement();

            if (next != start)
            {
                RemoveDead(next);

                if (live[next])
                    live[start] = true;
            }
        }

        // Out.debug("state "+start+" is live :"+live[start]);
    }


    /**
	 * Constructs a two state NFA for char class regexps, 
	 * such that the NFA has
	 *
	 *   exactly one start state,
	 *   exactly one end state,
	 *   no transitions leading out of the end state
	 *   no transitions leading into the start state
	 *
	 * Assumes that regExp.isCharClass(macros) == true
	 *   
	 * @param regExp the regular expression to construct the 
	 *        NFA for 
	 * 
	 * @return a pair of integers denoting the index of start
	 *         and end state of the NFA.
	 */
    private void InsertNFA(RegExp regExp, int start, int end)
    {
        switch (regExp.type)
        {

            case SymbolContants.BAR:
                RegExp2 r = (RegExp2)regExp;
                InsertNFA(r.r1, start, end);
                InsertNFA(r.r2, start, end);
                return;

            case SymbolContants.CCLASS:
                InsertClassNFA((List<Interval>)((RegExp1)regExp).content, start, end);
                return;

            case SymbolContants.CCLASSNOT:
                InsertNotClassNFA((List<Interval>)((RegExp1)regExp).content, start, end);
                return;

            case SymbolContants.CHAR:
                InsertLetterNFA(
                  false, (char)((RegExp1)regExp).content,
                  start, end);
                return;

            case SymbolContants.CHAR_I:
                InsertLetterNFA(
                 true, (char)((RegExp1)regExp).content,
                 start, end);
                return;

            case SymbolContants.MACROUSE:
                InsertNFA(macros.GetDefinition((string)((RegExp1)regExp).content),
                          start, end);
                return;
        }

        throw new Exception("Unknown expression type " + regExp.type + " in NFA construction");
    }


    /**
	 * Constructs an NFA for regExp such that the NFA has
	 *
	 *   exactly one start state,
	 *   exactly one end state,
	 *   no transitions leading out of the end state
	 *   no transitions leading into the start state
	 *  
	 * @param regExp the regular expression to construct the 
	 *        NFA for 
	 * 
	 * @return a pair of integers denoting the index of start
	 *         and end state of the NFA.
	 */
    public IntPair InsertNFA(RegExp regExp)
    {

        IntPair nfa1, nfa2;
        int start, end;
        RegExp2 r;

        if (Options.DEBUG)
            Out.Debug("Inserting RegExp : " + regExp);

        if (regExp.IsCharClass(macros))
        {
            start = numStates;
            end = numStates + 1;

            EnsureCapacity(end + 1);
            if (end + 1 > numStates) numStates = end + 1;

            InsertNFA(regExp, start, end);

            return new IntPair(start, end);
        }

        switch (regExp.type)
        {

            case SymbolContants.BAR:

                r = (RegExp2)regExp;

                nfa1 = InsertNFA(r.r1);
                nfa2 = InsertNFA(r.r2);

                start = nfa2.end + 1;
                end = nfa2.end + 2;

                AddEpsilonTransition(start, nfa1.start);
                AddEpsilonTransition(start, nfa2.start);
                AddEpsilonTransition(nfa1.end, end);
                AddEpsilonTransition(nfa2.end, end);

                return new IntPair(start, end);

            case SymbolContants.CONCAT:

                r = (RegExp2)regExp;

                nfa1 = InsertNFA(r.r1);
                nfa2 = InsertNFA(r.r2);

                AddEpsilonTransition(nfa1.end, nfa2.start);

                return new IntPair(nfa1.start, nfa2.end);

            case SymbolContants.STAR:
                nfa1 = InsertNFA((RegExp)((RegExp1)regExp).content);

                start = nfa1.end + 1;
                end = nfa1.end + 2;

                AddEpsilonTransition(nfa1.end, end);
                AddEpsilonTransition(start, nfa1.start);

                AddEpsilonTransition(start, end);
                AddEpsilonTransition(nfa1.end, nfa1.start);

                return new IntPair(start, end);

            case SymbolContants.PLUS:
                nfa1 = InsertNFA((RegExp)((RegExp1)regExp).content);

                start = nfa1.end + 1;
                end = nfa1.end + 2;

                AddEpsilonTransition(nfa1.end, end);
                AddEpsilonTransition(start, nfa1.start);

                AddEpsilonTransition(nfa1.end, nfa1.start);

                return new IntPair(start, end);

            case SymbolContants.QUESTION:
                nfa1 = InsertNFA((RegExp)((RegExp1)regExp).content);

                AddEpsilonTransition(nfa1.start, nfa1.end);

                return new IntPair(nfa1.start, nfa1.end);

            case SymbolContants.BANG:
                return Complement(InsertNFA((RegExp)((RegExp1)regExp).content));

            case SymbolContants.TILDE:
                nfa1 = InsertNFA((RegExp)((RegExp1)regExp).content);

                start = nfa1.end + 1;
                int s1 = start + 1;
                int s2 = s1 + 1;
                end = s2 + 1;

                for (int i = 0; i < numInput; i++)
                {
                    AddTransition(s1, i, s1);
                    AddTransition(s2, i, s2);
                }

                AddEpsilonTransition(start, s1);
                AddEpsilonTransition(s1, nfa1.start);
                AddEpsilonTransition(nfa1.end, s2);
                AddEpsilonTransition(s2, end);

                nfa1 = Complement(new IntPair(start, end));
                nfa2 = InsertNFA((RegExp)((RegExp1)regExp).content);

                AddEpsilonTransition(nfa1.end, nfa2.start);

                return new IntPair(nfa1.start, nfa2.end);

            case SymbolContants.STRING:
                return InsertStringNFA(false, (string)((RegExp1)regExp).content);

            case SymbolContants.STRING_I:
                return InsertStringNFA(true, (string)((RegExp1)regExp).content);

            case SymbolContants.MACROUSE:
                return InsertNFA(macros.GetDefinition((string)((RegExp1)regExp).content));
        }

        throw new Exception("Unknown expression type " + regExp.type + " in NFA construction");
    }
}