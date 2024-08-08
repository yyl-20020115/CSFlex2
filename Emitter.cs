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
using System.IO;
using System.Text;

namespace CSFlex;


/**
	 * This class manages the actual code generation, putting
	 * the scanner together, filling in skeleton sections etc.
	 *
	 * Table compression, string packing etc. is also done here.
	 *
	 * @author Gerwin Klein
	 * @version JFlex 1.4, $Revision: 2.22 $, $Date: 2004/04/12 10:07:48 $
	 * @author Jonathan Gilbert
	 * @version CSFlex 1.4
	 */
public class Emitter
{

    // bit masks for state attributes
    private const int FINAL = 1;
    private const int PUSHBACK = 2;
    private const int LOOKEND = 4;
    private const int NOLOOK = 8;

    private static readonly string date = DateTime.Now.ToShortDateString();

    private readonly File inputFile;

    private readonly TextWriter writer;
    private readonly Skeleton skel;
    private readonly LexScan scanner;
    private readonly LexParse parser;
    private readonly DFA dfa;

    // for switch statement:
    // table[i][j] is the set of input characters that leads from state i to state j
    private CharSet[][] table;

    private bool[] isTransition;

    // noTarget[i] is the set of input characters that have no target state in state i
    private CharSet[] noTarget;

    // for row killing:
    private int numRows;
    private int[] rowMap;
    private bool[] rowKilled;

    // for col killing:
    private int numCols;
    private int[] colMap;
    private bool[] colKilled;


    /** maps actions to their switch label */
    private readonly PrettyHashtable<Action, int> actionTable = [];

    private CharClassInterval[] intervalls;

    private string visibility = "public";

    public Emitter(File inputFile, LexParse parser, DFA dfa)
    {
        var name = Options.emit_csharp ? parser.scanner.className + ".cs" : parser.scanner.className + ".java";
        File outputFile = Normalize(name, inputFile);

        Out.Println("Writing code to \"" + outputFile + "\"");

        this.writer = new StreamWriter(outputFile);
        this.parser = parser;
        this.scanner = parser.scanner;
        this.visibility = scanner.visibility;
        this.inputFile = inputFile;
        this.dfa = dfa;
        this.skel = new Skeleton(writer);
    }


    /**
		 * Constructs a file in Options.getDir() or in the same directory as
		 * another file. Makes a backup if the file already exists.
		 *
		 * @param name  the name (without path) of the file
		 * @param path  the path where to construct the file
		 * @param input fallback location if path = <tt>null</tt>
		 *              (expected to be a file in the directory to write to)   
		 */
    public static File Normalize(string name, File input)
    {
        var outputFile = Options.GetDir() == null
            ? input == null || input.GetParent() == null ? new File(name) : new File(input.GetParent(), name)
            : new File(Options.GetDir(), name);
        if (outputFile.Exists() && !Options.no_backup)
        {
            var backup = new File(outputFile.ToString() + "~");

            if (backup.Exists()) backup.Delete();

            if (outputFile.RenameTo(backup))
                Out.Println("Old file \"" + outputFile + "\" saved as \"" + backup + "\"");
            else
                Out.Println("Couldn't save old file \"" + outputFile + "\", overwriting!");
        }

        return outputFile;
    }

    private void Println()
    {
        writer.WriteLine();
    }

    private void Println(string line)
    {
        writer.WriteLine(line);
    }

    private void Println(int i)
    {
        writer.WriteLine(i);
    }

    private void Print(string line)
    {
        writer.Write(line);
    }

    private void Print(int i)
    {
        writer.Write(i);
    }

    private void Print(int i, int tab)
    {
        int exp = i < 0 ? 1 : 10;
        while (tab-- > 1)
        {
            if (Math.Abs(i) < exp) Print(" ");
            exp *= 10;
        }

        Print(i);
    }

    private void EmitScanError()
    {
        Print("  private void zzScanError(int errorCode)");

        if (!Options.emit_csharp)
        {
            if (scanner.scanErrorException != null)
                Print(" throws " + scanner.scanErrorException);
        }

        Println(" {");

        skel.EmitNext();

        if (scanner.scanErrorException == null)
        {
            if (Options.emit_csharp)
                Println("    throw new Exception(message);");
            else
                Println("    throw new Error(message);");
        }
        else
            Println("    throw new " + scanner.scanErrorException + "(message);");

        skel.EmitNext();

        Print("  " + visibility + " void yypushback(int number) ");

        if (scanner.scanErrorException == null)
            Println(" {");
        else
        {
            if (Options.emit_csharp)
                Println(" {");
            else
                Println(" throws " + scanner.scanErrorException + " {");
        }
    }

    private void EmitMain()
    {
        if (!(scanner.standalone || scanner.debugOption || scanner.cupDebug)) return;

        if (scanner.cupDebug)
        {
            Println("  /**");
            Println("   * Converts an int token code into the name of the");
            Println("   * token by reflection on the cup symbol class/interface " + scanner.cupSymbol);
            Println("   *");
            Println("   * This code was contributed by Karl Meissner <meissnersd@yahoo.com>");
            Println("   */");
            if (Options.emit_csharp)
            {
                Println("  private string getTokenName(int token) {");
                Println("    try {");
                Println("      System.Reflection.FieldInfo[] classFields = typeof(" + scanner.cupSymbol + ").GetFields();");
                Println("      for (int i = 0; i < classFields.Length; i++) {");
                Println("        if (((int)classFields[i].GetValue(null)) == token) {");
                Println("          return classFields[i].Name;");
                Println("        }");
                Println("      }");
                Println("    } catch (Exception e) {");
                Println("      Out.error(e.ToString());");
                Println("    }");
                Println("");
                Println("    return \"UNKNOWN TOKEN\";");
                Println("  }");
            }
            else
            {
                Println("  private string getTokenName(int token) {");
                Println("    try {");
                Println("      java.lang.reflect.Field [] classFields = " + scanner.cupSymbol + ".class.getFields();");
                Println("      for (int i = 0; i < classFields.length; i++) {");
                Println("        if (classFields[i].getInt(null) == token) {");
                Println("          return classFields[i].getName();");
                Println("        }");
                Println("      }");
                Println("    } catch (Exception e) {");
                Println("      e.printStackTrace(System.err);");
                Println("    }");
                Println("");
                Println("    return \"UNKNOWN TOKEN\";");
                Println("  }");
            }
            Println("");
            Println("  /**");
            Println("   * Same as " + scanner.functionName + " but also prints the token to standard out");
            Println("   * for debugging.");
            Println("   *");
            Println("   * This code was contributed by Karl Meissner <meissnersd@yahoo.com>");
            Println("   */");

            Print("  " + visibility + " ");
            if (scanner.tokenType == null)
            {
                if (scanner.isInteger)
                    Print("int");
                else
                  if (scanner.isIntWrap)
                    Print("Integer");
                else
                    Print("Yytoken");
            }
            else
                Print(scanner.tokenType);

            Print(" debug_");

            Print(scanner.functionName);

            if (Options.emit_csharp)
                Print("()");
            else
            {
                Print("() throws java.io.IOException");

                if (scanner.lexThrow != null)
                {
                    Print(", ");
                    Print(scanner.lexThrow);
                }

                if (scanner.scanErrorException != null)
                {
                    Print(", ");
                    Print(scanner.scanErrorException);
                }
            }

            Println(" {");

            Println("    java_cup.runtime.Symbol s = " + scanner.functionName + "();");
            if (Options.emit_csharp)
            {
                Print("    Console.WriteLine( \"");

                int @base = 0;

                if (scanner.lineCount) { Print("line:{" + @base + "}"); @base++; }
                if (scanner.columnCount) { Print(" col:{" + @base + "}"); @base++; }
                Println(" --{" + (@base) + "}--{" + (@base + 1) + "}--\",");
                Println("      ");
                if (scanner.lineCount) Print("yyline+1, ");
                if (scanner.columnCount) Print("yycolumn+1, ");
                Println("yytext(), getTokenName(s.sym));");
            }
            else
            {
                Print("    System.out.println( ");
                if (scanner.lineCount) Print("\"line:\" + (yyline+1) + ");
                if (scanner.columnCount) Print("\" col:\" + (yycolumn+1) + ");
                Println("\" --\"+ yytext() + \"--\" + getTokenName(s.sym) + \"--\");");
            }
            Println("    return s;");
            Println("  }");
            Println("");
        }

        if (scanner.standalone)
        {
            Println("  /**");
            Println("   * Runs the scanner on input files.");
            Println("   *");
            Println("   * This is a standalone scanner, it will print any unmatched");
            Println("   * text to System.out unchanged.");
            Println("   *");
            Println("   * @param argv   the command line, contains the filenames to run");
            Println("   *               the scanner on.");
            Println("   */");
        }
        else
        {
            Println("  /**");
            Println("   * Runs the scanner on input files.");
            Println("   *");
            Println("   * This main method is the debugging routine for the scanner.");
            Println("   * It prints debugging information about each returned token to");
            Println("   * System.out until the end of file is reached, or an error occured.");
            Println("   *");
            Println("   * @param argv   the command line, contains the filenames to run");
            Println("   *               the scanner on.");
            Println("   */");
        }

        if (Options.emit_csharp)
        {
            Println("  public static void Main(string[] argv) {");
            Println("    if (argv.Length == 0) {");
            Println("      Console.WriteLine(\"Usage : " + scanner.className + " <inputfile>\");");
            Println("    }");
            Println("    else {");
            Println("      for (int i = 0; i < argv.Length; i++) {");
            Println("        " + scanner.className + " scanner = null;");
            Println("        try {");
            Println("          scanner = new " + scanner.className + "( new StreamReader(argv[i]) );");

            if (scanner.standalone)
            {
                Println("          while ( !scanner.zzAtEOF ) scanner." + scanner.functionName + "();");
            }
            else if (scanner.cupDebug)
            {
                Println("          while ( !scanner.zzAtEOF ) scanner.debug_" + scanner.functionName + "();");
            }
            else
            {
                Println("          do {");
                Println("            System.out.println(scanner." + scanner.functionName + "());");
                Println("          } while (!scanner.zzAtEOF);");
                Println("");
            }

            Println("        }");
            Println("        catch (FileNotFoundException) {");
            Println("          Console.WriteLine(\"File not found : \\\"{0}\\\"\", argv[i]);");
            Println("        }");
            Println("        catch (IOException e) {");
            Println("          Console.WriteLine(\"IO error scanning file \\\"{0}\\\"\", argv[i]);");
            Println("          Console.WriteLine(e);");
            Println("        }");
            Println("        catch (Exception e) {");
            Println("          Console.WriteLine(\"Unexpected exception:\");");
            Println("          Console.WriteLine(e.ToString());");
            Println("        }");
            Println("      }");
            Println("    }");
            Println("  }");
        }
        else
        {
            Println("  public static void main(string argv[]) {");
            Println("    if (argv.length == 0) {");
            Println("      System.out.println(\"Usage : java " + scanner.className + " <inputfile>\");");
            Println("    }");
            Println("    else {");
            Println("      for (int i = 0; i < argv.length; i++) {");
            Println("        " + scanner.className + " scanner = null;");
            Println("        try {");
            Println("          scanner = new " + scanner.className + "( new java.io.FileReader(argv[i]) );");

            if (scanner.standalone)
            {
                Println("          while ( !scanner.zzAtEOF ) scanner." + scanner.functionName + "();");
            }
            else if (scanner.cupDebug)
            {
                Println("          while ( !scanner.zzAtEOF ) scanner.debug_" + scanner.functionName + "();");
            }
            else
            {
                Println("          do {");
                Println("            System.out.println(scanner." + scanner.functionName + "());");
                Println("          } while (!scanner.zzAtEOF);");
                Println("");
            }

            Println("        }");
            Println("        catch (java.io.FileNotFoundException e) {");
            Println("          System.out.println(\"File not found : \\\"\"+argv[i]+\"\\\"\");");
            Println("        }");
            Println("        catch (java.io.IOException e) {");
            Println("          System.out.println(\"IO error scanning file \\\"\"+argv[i]+\"\\\"\");");
            Println("          System.out.println(e);");
            Println("        }");
            Println("        catch (Exception e) {");
            Println("          System.out.println(\"Unexpected exception:\");");
            Println("          e.printStackTrace();");
            Println("        }");
            Println("      }");
            Println("    }");
            Println("  }");
        }
        Println("");
    }

    private void EmitNoMatch()
    {
        Println("            zzScanError(ZZ_NO_MATCH);");
    }

    private void EmitNextInput()
    {
        Println("          if (zzCurrentPosL < zzEndReadL)");
        Println("            zzInput = zzBufferL[zzCurrentPosL++];");
        Println("          else if (zzAtEOF) {");
        Println("            zzInput = YYEOF;");
        if (Options.emit_csharp)
            Println("            goto zzForAction;");
        else
            Println("            break zzForAction;");
        Println("          }");
        Println("          else {");
        Println("            // store back cached positions");
        Println("            zzCurrentPos  = zzCurrentPosL;");
        Println("            zzMarkedPos   = zzMarkedPosL;");
        if (scanner.lookAheadUsed)
            Println("            zzPushbackPos = zzPushbackPosL;");
        if (Options.emit_csharp)
            Println("            bool eof = zzRefill();");
        else
            Println("            boolean eof = zzRefill();");
        Println("            // get translated positions and possibly new buffer");
        Println("            zzCurrentPosL  = zzCurrentPos;");
        Println("            zzMarkedPosL   = zzMarkedPos;");
        Println("            zzBufferL      = zzBuffer;");
        Println("            zzEndReadL     = zzEndRead;");
        if (scanner.lookAheadUsed)
            Println("            zzPushbackPosL = zzPushbackPos;");
        Println("            if (eof) {");
        Println("              zzInput = YYEOF;");
        if (Options.emit_csharp)
            Println("            goto zzForAction;");
        else
            Println("            break zzForAction;");
        Println("            }");
        Println("            else {");
        Println("              zzInput = zzBufferL[zzCurrentPosL++];");
        Println("            }");
        Println("          }");
    }

    private void EmitHeader()
    {
        Println("/* The following code was generated by CSFlex " + MainClass.version + " on " + date + " */");
        Println("");
    }

    private void EmitUserCode()
    {
        if (scanner.userCode.Length > 0)
        {
            if (Options.emit_csharp)
            {
                Println("#line 1 \"" + scanner.file + "\"");
                Println(scanner.userCode.ToString());
                Println("#line default");
            }
            else
                Println(scanner.userCode.ToString());
        }
    }

    private void EmitEpilogue()
    {
        if (scanner.epilogue.Length > 0)
        {
            if (Options.emit_csharp)
            {
                Println("#line " + scanner.epilogue_line + " \"" + scanner.file + "\"");
                Println(scanner.epilogue.ToString());
                Println("#line default");
            }
            else
                Println(scanner.epilogue.ToString());
        }
    }

    private void EmitClassName()
    {
        if (!EndsWithJavadoc(scanner.userCode))
        {
            string path = inputFile.ToString();
            // slashify path (avoid backslash u sequence = unicode escape)
            if (File.separatorChar != '/')
            {
                path = path.Replace(File.separatorChar, '/');
            }

            Println("/**");
            Println(" * This class is a scanner generated by <a href=\"http://www.sourceforge.net/projects/csflex/\">C# Flex</a>, based on");
            Println(" * <a href=\"http://www.jflex.de/\">JFlex</a>, version " + MainClass.version);
            Println(" * on " + date + " from the specification file");
            Println(" * <tt>" + path + "</tt>");
            Println(" */");
        }

        if (scanner.isPublic) Print("public ");

        if (scanner.isAbstract) Print("abstract ");

        if (scanner.isFinal)
        {
            if (Options.emit_csharp)
                Print("sealed ");
            else
                Print("final ");
        }

        Print("class ");
        Print(scanner.className);

        if (scanner.isExtending != null)
        {
            if (Options.emit_csharp)
                Print(": ");
            else
                Print(" extends ");
            Print(scanner.isExtending);
        }

        if (scanner.isImplementing != null)
        {
            if (Options.emit_csharp)
            {
                if (scanner.isExtending != null) // then we already output the ':'
                    Print(", ");
                else
                    Print(": ");
            }
            else
                Print(" implements ");
            Print(scanner.isImplementing);
        }

        Println(" {");
    }

    /**
		 * Try to find out if user code ends with a javadoc comment 
		 * 
		 * @param buffer   the user code
		 * @return true    if it ends with a javadoc comment
		 */
    public static bool EndsWithJavadoc(StringBuilder usercode)
    {
        string s = usercode.ToString().Trim();

        if (!s.EndsWith("*/")) return false;

        // find beginning of javadoc comment   
        int i = s.LastIndexOf("/**");
        if (i < 0) return false;

        // javadoc comment shouldn't contain a comment end
        return s.Substring(i, s.Length - 2 - i).IndexOf("*/") < 0;
    }


    private void EmitLexicalStates()
    {
        IEnumerator stateNames = scanner.states.Names();

        string @const = (Options.emit_csharp ? "const" : "static final");

        while (stateNames.MoveNext())
        {
            string name = (string)stateNames.Current;

            int? num = scanner.states.GetNumber(name);

            if (scanner.bolUsed)
                Println("  " + visibility + " " + @const + " int " + name + " = " + 2 * num + ";");
            else if (num != null)
            {
                Println("  " + visibility + " " + @const + " int " + name + " = " + dfa.lexState[2 * num.Value] + ";");
            }
            else
            {
                //not found!
                throw new InvalidOperationException($"state of name {name} is not found");
            }
        }

        if (scanner.bolUsed)
        {
            Println("");
            Println("  /**");
            Println("   * ZZ_LEXSTATE[l] is the state in the DFA for the lexical state l");
            Println("   * ZZ_LEXSTATE[l+1] is the state in the DFA for the lexical state l");
            Println("   *                  at the beginning of a line");
            Println("   * l is of the form l = 2*k, k a non negative integer");
            Println("   */");
            if (Options.emit_csharp)
                Println("  private static readonly int[] ZZ_LEXSTATE = new int[]{ ");
            else
                Println("  private static final int ZZ_LEXSTATE[] = { ");

            int i, j = 0;
            Print("    ");

            for (i = 0; i < dfa.lexState.Length - 1; i++)
            {
                Print(dfa.lexState[i], 2);

                Print(", ");

                if (++j >= 16)
                {
                    Println();
                    Print("    ");
                    j = 0;
                }
            }

            Println(dfa.lexState[i]);
            Println("  };");

        }
    }

    private void EmitDynamicInit()
    {
        int count = 0;
        int value = dfa.table[0][0];

        Println("  /** ");
        Println("   * The transition table of the DFA");
        Println("   */");

        CountEmitter e = new CountEmitter("Trans");
        e.SetValTranslation(+1); // allow vals in [-1, 0xFFFE]
        e.EmitInit();

        for (int i = 0; i < dfa.numStates; i++)
        {
            if (!rowKilled[i])
            {
                for (int c = 0; c < dfa.numInput; c++)
                {
                    if (!colKilled[c])
                    {
                        if (dfa.table[i][c] == value)
                        {
                            count++;
                        }
                        else
                        {
                            e.Emit(count, value);

                            count = 1;
                            value = dfa.table[i][c];
                        }
                    }
                }
            }
        }

        e.Emit(count, value);
        e.EmitUnpack();

        Println(e.ToString());
    }


    private void EmitCharMapInitFunction()
    {

        CharClasses cl = parser.GetCharClasses();

        if (cl.MaxCharCode < 256) return;

        Println("");
        Println("  /** ");
        Println("   * Unpacks the compressed character translation table.");
        Println("   *");
        Println("   * @param packed   the packed character translation table");
        Println("   * @return         the unpacked character translation table");
        Println("   */");
        if (Options.emit_csharp)
        {
            Println("  private static char [] zzUnpackCMap(ushort[] packed) {");
            Println("    char [] map = new char[0x10000];");
            Println("    int i = 0;  /* index in packed string  */");
            Println("    int j = 0;  /* index in unpacked array */");
            Println("    while (i < " + 2 * intervalls.Length + ") {");
            Println("      int  count = packed[i++];");
            Println("      char value = (char)packed[i++];");
            Println("      do map[j++] = value; while (--count > 0);");
            Println("    }");
            Println("    return map;");
            Println("  }");
        }
        else
        {
            Println("  private static char [] zzUnpackCMap(string packed) {");
            Println("    char [] map = new char[0x10000];");
            Println("    int i = 0;  /* index in packed string  */");
            Println("    int j = 0;  /* index in unpacked array */");
            Println("    while (i < " + 2 * intervalls.Length + ") {");
            Println("      int  count = packed.charAt(i++);");
            Println("      char value = packed.charAt(i++);");
            Println("      do map[j++] = value; while (--count > 0);");
            Println("    }");
            Println("    return map;");
            Println("  }");
        }
    }

    private void EmitZZTrans()
    {

        int i, c;
        int n = 0;

        Println("  /** ");
        Println("   * The transition table of the DFA");
        Println("   */");
        if (Options.emit_csharp)
            Println("  private static readonly int[] ZZ_TRANS = new int[] {");
        else
            Println("  private static final int ZZ_TRANS [] = {"); //XXX

        Print("    ");
        for (i = 0; i < dfa.numStates; i++)
        {

            if (!rowKilled[i])
            {
                for (c = 0; c < dfa.numInput; c++)
                {
                    if (!colKilled[c])
                    {
                        if (n >= 10)
                        {
                            Println();
                            Print("    ");
                            n = 0;
                        }
                        Print(dfa.table[i][c]);
                        if (i != dfa.numStates - 1 || c != dfa.numInput - 1)
                            Print(", ");
                        n++;
                    }
                }
            }
        }

        Println();
        Println("  };");
    }

    private void EmitCharMapArrayUnPacked()
    {

        CharClasses cl = parser.GetCharClasses();
        intervalls = cl.GetIntervalls();

        Println("");
        Println("  /** ");
        Println("   * Translates characters to character classes");
        Println("   */");
        if (Options.emit_csharp)
            Println("  private static readonly char[] ZZ_CMAP = new char[] {");
        else
            Println("  private static final char [] ZZ_CMAP = {");

        int n = 0;  // numbers of entries in current line    
        Print("    ");

        int max = cl.MaxCharCode;
        int i = 0;
        while (i < intervalls.Length && intervalls[i].start <= max)
        {

            int end = Math.Min(intervalls[i].end, max);
            for (int c = intervalls[i].start; c <= end; c++)
            {

                if (Options.emit_csharp)
                    Print("(char)");
                Print(colMap[intervalls[i].charClass], 2);

                if (c < max)
                {
                    Print(", ");
                    if (++n >= 16)
                    {
                        Println();
                        Print("    ");
                        n = 0;
                    }
                }
            }

            i++;
        }

        Println();
        Println("  };");
        Println();
    }

    private void EmitCSharpStaticConstructor(bool include_char_map_array)
    {
        if (!Options.emit_csharp)
            return;

        Println("  static " + scanner.className + "()");
        Println("  {");
        if (include_char_map_array)
            Println("    ZZ_CMAP = zzUnpackCMap(ZZ_CMAP_PACKED);");
        Println("    ZZ_ACTION = zzUnpackAction();");
        Println("    ZZ_ROWMAP = zzUnpackRowMap();");
        Println("    ZZ_TRANS = zzUnpackTrans();");
        Println("    ZZ_ATTRIBUTE = zzUnpackAttribute();");
        Println("  }");
        Println("");
    }

    private void EmitCharMapArray()
    {
        CharClasses cl = parser.GetCharClasses();

        if (cl.MaxCharCode < 256)
        {
            EmitCSharpStaticConstructor(false);
            EmitCharMapArrayUnPacked();
            return;
        }
        else
            EmitCSharpStaticConstructor(true);

        // ignores cl.getMaxCharCode(), emits all intervalls instead

        intervalls = cl.GetIntervalls();

        Println("");
        Println("  /** ");
        Println("   * Translates characters to character classes");
        Println("   */");
        if (Options.emit_csharp)
            Println("  private static readonly ushort[] ZZ_CMAP_PACKED = new ushort[] {");
        else
            Println("  private static final string ZZ_CMAP_PACKED = ");

        int n = 0;  // numbers of entries in current line    
        if (Options.emit_csharp)
            Print("   ");
        else
            Print("    \"");

        int i = 0;
        while (i < intervalls.Length - 1)
        {
            int count = intervalls[i].end - intervalls[i].start + 1;
            int value = colMap[intervalls[i].charClass];

            PrintUC(count);
            PrintUC(value);

            if (++n >= 10)
            {
                if (Options.emit_csharp)
                {
                    Println("");
                    Print("   ");
                }
                else
                {
                    Println("\"+");
                    Print("    \"");
                }
                n = 0;
            }

            i++;
        }

        PrintUC(intervalls[i].end - intervalls[i].start + 1);
        PrintUC(colMap[intervalls[i].charClass]);

        if (Options.emit_csharp)
            Println(" 0 };"); // the extraneous 0 can't be avoided without restructuring printUC()
        else
            Println("\";");
        Println();

        Println("  /** ");
        Println("   * Translates characters to character classes");
        Println("   */");
        if (Options.emit_csharp)
            Println("  private static readonly char[] ZZ_CMAP;");
        else
            Println("  private static final char [] ZZ_CMAP = zzUnpackCMap(ZZ_CMAP_PACKED);");
        Println();
    }


    /**
		 * Print number as octal/unicode escaped string character.
		 * 
		 * @param c   the value to print
		 * @prec  0 <= c <= 0xFFFF 
		 */
    private void PrintUC(int c)
    {
        if (Options.emit_csharp)
            writer.Write(" ");

        if (c > 255)
        {
            if (Options.emit_csharp)
            {
                writer.Write("0x");
                if (c < 0x1000) writer.Write("0");
                writer.Write(IntegerUtil.ToHexString(c));
            }
            else
            {
                writer.Write("\\u");
                if (c < 0x1000) writer.Write("0");
                writer.Write(IntegerUtil.ToHexString(c));
            }
        }
        else
        {
            if (Options.emit_csharp)
                writer.Write(c.ToString());
            else
            {
                writer.Write("\\");
                writer.Write(IntegerUtil.ToOctalString(c));
            }
        }

        if (Options.emit_csharp)
            writer.Write(",");
    }


    private void EmitRowMapArray()
    {
        Println("");
        Println("  /** ");
        Println("   * Translates a state to a row index in the transition table");
        Println("   */");

        HiLowEmitter e = new HiLowEmitter("RowMap");
        e.EmitInit();
        for (int i = 0; i < dfa.numStates; i++)
        {
            e.Emit(rowMap[i] * numCols);
        }
        e.EmitUnpack();
        Println(e.ToString());
    }


    private void EmitAttributes()
    {
        Println("  /**");
        Println("   * ZZ_ATTRIBUTE[aState] contains the attributes of state <code>aState</code>");
        Println("   */");

        CountEmitter e = new CountEmitter("Attribute");
        e.EmitInit();

        int count = 1;
        int value = 0;
        if (dfa.isFinal[0]) value = FINAL;
        if (dfa.isPushback[0]) value |= PUSHBACK;
        if (dfa.isLookEnd[0]) value |= LOOKEND;
        if (!isTransition[0]) value |= NOLOOK;

        for (int i = 1; i < dfa.numStates; i++)
        {
            int attribute = 0;
            if (dfa.isFinal[i]) attribute = FINAL;
            if (dfa.isPushback[i]) attribute |= PUSHBACK;
            if (dfa.isLookEnd[i]) attribute |= LOOKEND;
            if (!isTransition[i]) attribute |= NOLOOK;

            if (value == attribute)
            {
                count++;
            }
            else
            {
                e.Emit(count, value);
                count = 1;
                value = attribute;
            }
        }

        e.Emit(count, value);
        e.EmitUnpack();

        Println(e.ToString());
    }


    private void EmitClassCode()
    {
        if (scanner.eofCode != null)
        {
            Println("  /** denotes if the user-EOF-code has already been executed */");
            if (Options.emit_csharp)
                Println("  private bool zzEOFDone;");
            else
                Println("  private boolean zzEOFDone;");
            Println("");
        }

        if (scanner.classCode != null)
        {
            Println("  /* user code: */");
            Println(scanner.classCode);
        }
    }

    private void EmitConstructorDecl()
    {

        Print("  ");

        if (Options.emit_csharp)
        {
            if (scanner.isPublic)
                Print("public ");
            else
                Print("internal ");
            Print(scanner.className);
            Print("(TextReader @in)");
        }
        else
        {
            if (scanner.isPublic)
                Print("public ");
            Print(scanner.className);
            Print("(java.io.Reader in)");

            if (scanner.initThrow != null)
            {
                Print(" throws ");
                Print(scanner.initThrow);
            }
        }

        Println(" {");

        if (scanner.initCode != null)
        {
            Print("  ");
            Print(scanner.initCode);
        }

        Println("    this.zzReader = @in;");

        Println("  }");
        Println();


        if (Options.emit_csharp)
        {
            Println("  /**");
            Println("   * Creates a new scanner.");
            Println("   * There is also TextReader version of this constructor.");
            Println("   *");
            Println("   * @param   in  the System.IO.Stream to read input from.");
            Println("   */");

            Print("  ");
            if (scanner.isPublic)
                Print("public ");
            else
                Print("internal ");
            Print(scanner.className);
            Print("(Stream @in)");

            Println(" : this(new StreamReader(@in))");
            Println("  {");
            Println("  }");
        }
        else
        {
            Println("  /**");
            Println("   * Creates a new scanner.");
            Println("   * There is also java.io.Reader version of this constructor.");
            Println("   *");
            Println("   * @param   in  the java.io.Inputstream to read input from.");
            Println("   */");

            Print("  ");
            if (scanner.isPublic) Print("public ");
            Print(scanner.className);
            Print("(java.io.InputStream in)");

            if (scanner.initThrow != null)
            {
                Print(" throws ");
                Print(scanner.initThrow);
            }

            Println("  {");
            Println("    this(new java.io.InputStreamReader(in));");
            Println("  }");
        }
    }


    private void EmitDoEOF()
    {
        if (scanner.eofCode == null) return;

        Println("  /**");
        Println("   * Contains user EOF-code, which will be executed exactly once,");
        Println("   * when the end of file is reached");
        Println("   */");

        Print("  private void zzDoEOF()");

        if (!Options.emit_csharp)
            if (scanner.eofThrow != null)
            {
                Print(" throws ");
                Print(scanner.eofThrow);
            }

        Println(" {");

        Println("    if (!zzEOFDone) {");
        Println("      zzEOFDone = true;");
        Println("      " + scanner.eofCode);
        Println("    }");
        Println("  }");
        Println("");
        Println("");
    }

    private void EmitLexFunctHeader()
    {

        if (scanner.cupCompatible)
        {
            // force public, because we have to implement java_cup.runtime.Symbol
            Print("  public ");
        }
        else
        {
            Print("  " + visibility + " ");
        }

        if (scanner.tokenType == null)
        {
            if (scanner.isInteger)
                Print("int");
            else
            if (scanner.isIntWrap)
                Print("Integer");
            else
                Print("Yytoken");
        }
        else
            Print(scanner.tokenType);

        Print(" ");

        Print(scanner.functionName);

        if (Options.emit_csharp)
            Print("()");
        else
        {
            Print("() throws java.io.IOException");

            if (scanner.lexThrow != null)
            {
                Print(", ");
                Print(scanner.lexThrow);
            }

            if (scanner.scanErrorException != null)
            {
                Print(", ");
                Print(scanner.scanErrorException);
            }
        }

        Println(" {");

        skel.EmitNext();

        if (scanner.useRowMap)
        {
            Println("    int [] zzTransL = ZZ_TRANS;");
            Println("    int [] zzRowMapL = ZZ_ROWMAP;");
            Println("    int [] zzAttrL = ZZ_ATTRIBUTE;");

        }

        if (scanner.lookAheadUsed)
        {
            Println("    int zzPushbackPosL = zzPushbackPos = -1;");
            if (Options.emit_csharp)
                Println("    bool zzWasPushback;");
            else
                Println("    boolean zzWasPushback;");
        }

        skel.EmitNext();

        if (scanner.charCount)
        {
            Println("      yychar+= zzMarkedPosL-zzStartRead;");
            Println("");
        }

        if (scanner.lineCount || scanner.columnCount)
        {
            if (Options.emit_csharp)
                Println("      bool zzR = false;");
            else
                Println("      boolean zzR = false;");
            Println("      for (zzCurrentPosL = zzStartRead; zzCurrentPosL < zzMarkedPosL;");
            Println("                                                             zzCurrentPosL++) {");
            Println("        switch (zzBufferL[zzCurrentPosL]) {");
            Println("        case '\\u000B':");
            Println("        case '\\u000C':");
            Println("        case '\\u0085':");
            Println("        case '\\u2028':");
            Println("        case '\\u2029':");
            if (scanner.lineCount)
                Println("          yyline++;");
            if (scanner.columnCount)
                Println("          yycolumn = 0;");
            Println("          zzR = false;");
            Println("          break;");
            Println("        case '\\r':");
            if (scanner.lineCount)
                Println("          yyline++;");
            if (scanner.columnCount)
                Println("          yycolumn = 0;");
            Println("          zzR = true;");
            Println("          break;");
            Println("        case '\\n':");
            Println("          if (zzR)");
            Println("            zzR = false;");
            Println("          else {");
            if (scanner.lineCount)
                Println("            yyline++;");
            if (scanner.columnCount)
                Println("            yycolumn = 0;");
            Println("          }");
            Println("          break;");
            Println("        default:");
            Println("          zzR = false;");
            if (scanner.columnCount)
                Println("          yycolumn++;");
            if (Options.emit_csharp)
                Println("          break;");
            Println("        }");
            Println("      }");
            Println();

            if (scanner.lineCount)
            {
                Println("      if (zzR) {");
                Println("        // peek one character ahead if it is \\n (if we have counted one line too much)");
                if (Options.emit_csharp)
                    Println("        bool zzPeek;");
                else
                    Println("        boolean zzPeek;");
                Println("        if (zzMarkedPosL < zzEndReadL)");
                Println("          zzPeek = zzBufferL[zzMarkedPosL] == '\\n';");
                Println("        else if (zzAtEOF)");
                Println("          zzPeek = false;");
                Println("        else {");
                if (Options.emit_csharp)
                    Println("          bool eof = zzRefill();");
                else
                    Println("          boolean eof = zzRefill();");
                Println("          zzMarkedPosL = zzMarkedPos;");
                Println("          zzBufferL = zzBuffer;");
                Println("          if (eof) ");
                Println("            zzPeek = false;");
                Println("          else ");
                Println("            zzPeek = zzBufferL[zzMarkedPosL] == '\\n';");
                Println("        }");
                Println("        if (zzPeek) yyline--;");
                Println("      }");
            }
        }

        if (scanner.bolUsed)
        {
            // zzMarkedPos > zzStartRead <=> last match was not empty
            // if match was empty, last value of zzAtBOL can be used
            // zzStartRead is always >= 0
            Println("      if (zzMarkedPosL > zzStartRead) {");
            Println("        switch (zzBufferL[zzMarkedPosL-1]) {");
            Println("        case '\\n':");
            Println("        case '\\u000B':");
            Println("        case '\\u000C':");
            Println("        case '\\u0085':");
            Println("        case '\\u2028':");
            Println("        case '\\u2029':");
            Println("          zzAtBOL = true;");
            Println("          break;");
            Println("        case '\\r': ");
            Println("          if (zzMarkedPosL < zzEndReadL)");
            Println("            zzAtBOL = zzBufferL[zzMarkedPosL] != '\\n';");
            Println("          else if (zzAtEOF)");
            Println("            zzAtBOL = false;");
            Println("          else {");
            if (Options.emit_csharp)
                Println("            bool eof = zzRefill();");
            else
                Println("            boolean eof = zzRefill();");
            Println("            zzMarkedPosL = zzMarkedPos;");
            Println("            zzBufferL = zzBuffer;");
            Println("            if (eof) ");
            Println("              zzAtBOL = false;");
            Println("            else ");
            Println("              zzAtBOL = zzBufferL[zzMarkedPosL] != '\\n';");
            Println("          }");
            Println("          break;");
            Println("        default:");
            Println("          zzAtBOL = false;");
            if (Options.emit_csharp)
                Println("          break;");
            Println("        }");
            Println("      }");
        }

        skel.EmitNext();

        if (scanner.bolUsed)
        {
            Println("      if (zzAtBOL)");
            Println("        zzState = ZZ_LEXSTATE[zzLexicalState+1];");
            Println("      else");
            Println("        zzState = ZZ_LEXSTATE[zzLexicalState];");
            Println();
        }
        else
        {
            Println("      zzState = zzLexicalState;");
            Println();
        }

        if (scanner.lookAheadUsed)
            Println("      zzWasPushback = false;");

        skel.EmitNext();
    }


    private void EmitGetRowMapNext()
    {
        Println("          int zzNext = zzTransL[ zzRowMapL[zzState] + zzCMapL[zzInput] ];");
        if (Options.emit_csharp)
            Println("          if (zzNext == " + DFA.NO_TARGET + ") goto zzForAction;");
        else
            Println("          if (zzNext == " + DFA.NO_TARGET + ") break zzForAction;");
        Println("          zzState = zzNext;");
        Println();

        Println("          int zzAttributes = zzAttrL[zzState];");

        if (scanner.lookAheadUsed)
        {
            Println("          if ( (zzAttributes & " + PUSHBACK + ") == " + PUSHBACK + " )");
            Println("            zzPushbackPosL = zzCurrentPosL;");
            Println();
        }

        Println("          if ( (zzAttributes & " + FINAL + ") == " + FINAL + " ) {");
        if (scanner.lookAheadUsed)
            Println("            zzWasPushback = (zzAttributes & " + LOOKEND + ") == " + LOOKEND + ";");

        skel.EmitNext();

        if (Options.emit_csharp)
            Println("            if ( (zzAttributes & " + NOLOOK + ") == " + NOLOOK + " ) goto zzForAction;");
        else
            Println("            if ( (zzAttributes & " + NOLOOK + ") == " + NOLOOK + " ) break zzForAction;");

        skel.EmitNext();
    }

    private void EmitTransitionTable()
    {
        TransformTransitionTable();

        Println("          zzInput = zzCMapL[zzInput];");
        Println();

        if (Options.emit_csharp)
        {
            if (scanner.lookAheadUsed)
                Println("          bool zzPushback = false;");

            Println("          bool zzIsFinal = false;");
            Println("          bool zzNoLookAhead = false;");
            Println();
        }
        else
        {
            if (scanner.lookAheadUsed)
                Println("          boolean zzPushback = false;");

            Println("          boolean zzIsFinal = false;");
            Println("          boolean zzNoLookAhead = false;");
            Println();
        }

        if (Options.emit_csharp)
        {
            Println("          switch (zzState) {");
            Println("            case 2147483647:");
            Println("              zzForNext: break;");
            Println("            case 2147483646:");
            Println("              goto zzForNext;");
        }
        else
            Println("          zzForNext: { switch (zzState) {");

        for (int state = 0; state < dfa.numStates; state++)
            if (isTransition[state]) EmitState(state);

        Println("            default:");
        Println("              // if this is ever reached, there is a serious bug in JFlex/C# Flex");
        Println("              zzScanError(ZZ_UNKNOWN_ERROR);");
        Println("              break;");
        if (Options.emit_csharp)
            Println("          }");
        else
            Println("          } }");
        Println();

        Println("          if ( zzIsFinal ) {");

        if (scanner.lookAheadUsed)
            Println("            zzWasPushback = zzPushback;");

        skel.EmitNext();

        if (Options.emit_csharp)
            Println("            if ( zzNoLookAhead ) goto zzForAction;");
        else
            Println("            if ( zzNoLookAhead ) break zzForAction;");

        skel.EmitNext();
    }


    /**
		 * Escapes all " ' \ tabs and newlines
		 */
    private string Escapify(string s)
    {
        StringBuilder result = new StringBuilder(s.Length * 2);

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\'': result.Append("\\\'"); break;
                case '\"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\t': result.Append("\\t"); break;
                case '\r':
                    if (i + 1 == s.Length || s[i + 1] != '\n') result.Append("\"+ZZ_NL+\"");
                    break;
                case '\n': result.Append("\"+ZZ_NL+\""); break;
                default: result.Append(c); break;
            }
        }

        return result.ToString();
    }

    public void EmitActionTable()
    {
        int lastAction = 1;
        int count = 0;
        int value = 0;

        Println("  /** ");
        Println("   * Translates DFA states to action switch labels.");
        Println("   */");
        CountEmitter e = new CountEmitter("Action");
        e.EmitInit();

        for (int i = 0; i < dfa.numStates; i++)
        {
            int newVal;
            if (dfa.isFinal[i])
            {
                Action action = dfa.action[i];
                object stored = actionTable[action];
                if (stored == null)
                {
                    stored = lastAction++;
                    actionTable[action] = (int)stored;
                }
                newVal = (int)stored;
            }
            else
            {
                newVal = 0;
            }

            if (value == newVal)
            {
                count++;
            }
            else
            {
                if (count > 0) e.Emit(count, value);
                count = 1;
                value = newVal;
            }
        }

        if (count > 0) e.Emit(count, value);

        e.EmitUnpack();
        Println(e.ToString());
    }

    private void EmitActions()
    {
        Println("      switch (zzAction < 0 ? zzAction : ZZ_ACTION[zzAction]) {");

        int i = actionTable.Count + 1;
        IEnumerator actions = actionTable.Keys.GetEnumerator();
        while (actions.MoveNext())
        {
            Action action = (Action)actions.Current;
            int label = actionTable[action];

            Println("        case " + label + ": ");

            if (Options.emit_csharp)
            {
                Println("          if (ZZ_SPURIOUS_WARNINGS_SUCK)");
                Println("          {");

                if (scanner.debugOption)
                {
                    int @base = 0;

                    Print("            Console.WriteLine(\"");
                    if (scanner.lineCount) { Print("line: {" + @base + "} "); @base++; }
                    if (scanner.columnCount) { Print("col: {" + @base + "} "); @base++; }
                    Println("match: --{" + @base + "}--\",");
                    Print("              ");
                    if (scanner.lineCount) Print("yyline+1, ");
                    if (scanner.columnCount) Print("yycolumn+1, ");
                    Println("yytext());");

                    Print("            Console.WriteLine(\"action [" + action.priority + "] { ");
                    Print(Escapify(action.content));
                    Println(" }\");");
                }

                Println("#line " + action.priority + " \"" + Escapify(scanner.file) + "\"");
                Println(action.content);
                Println("#line default");
                Println("          }");
                Println("          break;");
            }
            else
            {
                if (scanner.debugOption)
                {
                    Print("          System.out.println(");
                    if (scanner.lineCount)
                        Print("\"line: \"+(yyline+1)+\" \"+");
                    if (scanner.columnCount)
                        Print("\"col: \"+(yycolumn+1)+\" \"+");
                    Println("\"match: --\"+yytext()+\"--\");");
                    Print("          System.out.println(\"action [" + action.priority + "] { ");
                    Print(Escapify(action.content));
                    Println(" }\");");
                }

                Println("          { " + action.content);
                Println("          }");
                Println("        case " + (i++) + ": break;");
            }
        }
    }

    private void EmitEOFVal()
    {
        EOFActions eofActions = parser.GetEOFActions();

        if (scanner.eofCode != null)
            Println("            zzDoEOF();");

        if (eofActions.NumActions > 0)
        {
            Println("            switch (zzLexicalState) {");

            IEnumerator stateNames = scanner.states.Names();

            // record lex states already emitted:
            PrettyHashtable<int, string> used = new PrettyHashtable<int, string>();

            // pick a start value for break case labels. 
            // must be larger than any value of a lex state:
            int last = dfa.numStates;

            while (stateNames.MoveNext())
            {
                string name = (string)stateNames.Current;
                int? num = scanner.states.GetNumber(name);
                if (num == null)
                {
                    throw new InvalidOperationException($"state of name {name} is not found");
                }
                else
                {
                    Action action = eofActions.GetAction(num.Value);

                    // only emit code if the lex state is not redundant, so
                    // that case labels don't overlap
                    // (redundant = points to the same dfa state as another one).
                    // applies only to scanners that don't use BOL, because
                    // in BOL scanners lex states get mapped at runtime, so
                    // case labels will always be unique.
                    bool unused = true;
                    if (!scanner.bolUsed)
                    {
                        int key = dfa.lexState[2 * num.Value];
                        unused = used[key] == null;

                        if (!unused)
                            Out.Warning("Lexical states <" + name + "> and <" + used[key] + "> are equivalent.");
                        else
                            used[key] = name;
                    }

                    if (action != null && unused)
                    {
                        if (Options.emit_csharp)
                        {
                            Println("            case " + name + ":");
                            Println("              if (ZZ_SPURIOUS_WARNINGS_SUCK)");
                            Println("              {");
                            Println("#line " + action.priority + " \"" + scanner.file + "\"");
                            Println(action.content);
                            Println("#line default");
                            Println("              }");
                            Println("              break;");
                        }
                        else
                        {
                            Println("            case " + name + ":");
                            Println("              { " + action.content + " }");
                            Println("            case " + (++last) + ": break;");
                        }
                    }
                }
            }

            Println("            default:");
        }

        if (eofActions.GetDefault() != null)
        {
            if (Options.emit_csharp)
            {
                Action dfl = eofActions.GetDefault();

                Println("              if (ZZ_SPURIOUS_WARNINGS_SUCK)");
                Println("              {");
                Println("#line " + dfl.priority + " \"" + scanner.file + "\"");
                Println(dfl.content);
                Println("#line default");
                Println("              }");
            }
            else
            {
                Println("              if (ZZ_SPURIOUS_WARNINGS_SUCK)");
                Println("              { " + eofActions.GetDefault().content + " }");
            }
            Println("              break;");
        }
        else if (scanner.eofVal != null)
        {
            Println("              if (ZZ_SPURIOUS_WARNINGS_SUCK)");
            Println("              { " + scanner.eofVal + " }");
            Println("              break;");
        }
        else if (scanner.isInteger)
            Println("            return YYEOF;");
        else
            Println("            return null;");

        if (eofActions.NumActions > 0)
            Println("            }");
    }

    private void EmitState(int state)
    {

        Println("            case " + state + ":");
        Println("              switch (zzInput) {");

        int defaultTransition = GetDefaultTransition(state);

        for (int next = 0; next < dfa.numStates; next++)
        {

            if (next != defaultTransition && table[state][next] != null)
            {
                EmitTransition(state, next);
            }
        }

        if (defaultTransition != DFA.NO_TARGET && noTarget[state] != null)
        {
            EmitTransition(state, DFA.NO_TARGET);
        }

        EmitDefaultTransition(state, defaultTransition);

        Println("              }");
        Println("");
    }

    private void EmitTransition(int state, int nextState)
    {

        CharSetEnumerator chars;

        if (nextState != DFA.NO_TARGET)
            chars = table[state][nextState].Characters;
        else
            chars = noTarget[state].Characters;

        Print("                case ");
        Print((int)chars.NextElement());
        Print(": ");

        while (chars.HasMoreElements)
        {
            Println();
            Print("                case ");
            Print((int)chars.NextElement());
            Print(": ");
        }

        if (nextState != DFA.NO_TARGET)
        {
            if (dfa.isFinal[nextState])
                Print("zzIsFinal = true; ");

            if (dfa.isPushback[nextState])
                Print("zzPushbackPosL = zzCurrentPosL; ");

            if (dfa.isLookEnd[nextState])
                Print("zzPushback = true; ");

            if (!isTransition[nextState])
                Print("zzNoLookAhead = true; ");

            if (Options.emit_csharp)
                Println("zzState = " + nextState + "; goto zzForNext;");
            else
                Println("zzState = " + nextState + "; break zzForNext;");
        }
        else
        {
            if (Options.emit_csharp)
                Println("goto zzForAction;");
            else
                Println("break zzForAction;");
        }
    }

    private void EmitDefaultTransition(int state, int nextState)
    {
        Print("                default: ");

        if (nextState != DFA.NO_TARGET)
        {
            if (dfa.isFinal[nextState])
                Print("zzIsFinal = true; ");

            if (dfa.isPushback[nextState])
                Print("zzPushbackPosL = zzCurrentPosL; ");

            if (dfa.isLookEnd[nextState])
                Print("zzPushback = true; ");

            if (!isTransition[nextState])
                Print("zzNoLookAhead = true; ");

            if (Options.emit_csharp)
                Println("zzState = " + nextState + "; goto zzForNext;");
            else
                Println("zzState = " + nextState + "; break zzForNext;");
        }
        else
        {
            if (Options.emit_csharp)
                Println("goto zzForAction;");
            else
                Println("break zzForAction;");
        }
    }

    private void EmitPushback()
    {
        Println("      if (zzWasPushback)");
        Println("        zzMarkedPos = zzPushbackPosL;");
    }

    private int GetDefaultTransition(int state)
    {
        int max = 0;

        for (int i = 0; i < dfa.numStates; i++)
        {
            if (table[state][max] == null)
                max = i;
            else
            if (table[state][i] != null && table[state][max].Size < table[state][i].Size)
                max = i;
        }

        if (table[state][max] == null) return DFA.NO_TARGET;
        if (noTarget[state] == null) return max;

        if (table[state][max].Size < noTarget[state].Size)
            max = DFA.NO_TARGET;

        return max;
    }

    // for switch statement:
    private void TransformTransitionTable()
    {

        int numInput = parser.GetCharClasses().ClassesCount + 1;

        int i;
        char j;

        table = new CharSet[dfa.numStates][];
        for (i = 0; i < table.Length; i++)
            table[i] = new CharSet[dfa.numStates];
        noTarget = new CharSet[dfa.numStates];

        for (i = 0; i < dfa.numStates; i++)
            for (j = (char)0; j < dfa.numInput; j++)
            {

                int nextState = dfa.table[i][j];

                if (nextState == DFA.NO_TARGET)
                {
                    if (noTarget[i] == null)
                        noTarget[i] = new CharSet(numInput, colMap[j]);
                    else
                        noTarget[i].Add(colMap[j]);
                }
                else
                {
                    if (table[i][nextState] == null)
                        table[i][nextState] = new CharSet(numInput, colMap[j]);
                    else
                        table[i][nextState].Add(colMap[j]);
                }
            }
    }

    private void FindActionStates()
    {
        isTransition = new bool[dfa.numStates];

        for (int i = 0; i < dfa.numStates; i++)
        {
            char j = (char)0;
            while (!isTransition[i] && j < dfa.numInput)
                isTransition[i] = dfa.table[i][j++] != DFA.NO_TARGET;
        }
    }


    private void ReduceColumns()
    {
        colMap = new int[dfa.numInput];
        colKilled = new bool[dfa.numInput];

        int i, j, k;
        int translate = 0;
        bool equal;

        numCols = dfa.numInput;

        for (i = 0; i < dfa.numInput; i++)
        {

            colMap[i] = i - translate;

            for (j = 0; j < i; j++)
            {

                // test for equality:
                k = -1;
                equal = true;
                while (equal && ++k < dfa.numStates)
                    equal = dfa.table[k][i] == dfa.table[k][j];

                if (equal)
                {
                    translate++;
                    colMap[i] = colMap[j];
                    colKilled[i] = true;
                    numCols--;
                    break;
                } // if
            } // for j
        } // for i
    }

    private void ReduceRows()
    {
        rowMap = new int[dfa.numStates];
        rowKilled = new bool[dfa.numStates];

        int i, j, k;
        int translate = 0;
        bool equal;

        numRows = dfa.numStates;

        // i is the state to add to the new table
        for (i = 0; i < dfa.numStates; i++)
        {

            rowMap[i] = i - translate;

            // check if state i can be removed (i.e. already
            // exists in entries 0..i-1)
            for (j = 0; j < i; j++)
            {

                // test for equality:
                k = -1;
                equal = true;
                while (equal && ++k < dfa.numInput)
                    equal = dfa.table[i][k] == dfa.table[j][k];

                if (equal)
                {
                    translate++;
                    rowMap[i] = rowMap[j];
                    rowKilled[i] = true;
                    numRows--;
                    break;
                } // if
            } // for j
        } // for i

    }


    /**
		 * Set up EOF code sectioin according to scanner.eofcode 
		 */
    private void SetupEOFCode()
    {
        if (scanner.eofclose)
        {
            scanner.eofCode = LexScan.Conc(scanner.eofCode, "  yyclose();");
            scanner.eofThrow = LexScan.ConcExc(scanner.eofThrow, "java.io.IOException");
        }
    }


    /**
		 * Main Emitter method.  
		 */
    public void Emit()
    {

        SetupEOFCode();

        if (scanner.functionName == null)
            scanner.functionName = "yylex";

        ReduceColumns();
        FindActionStates();

        EmitHeader();
        EmitUserCode();
        EmitClassName();

        skel.EmitNext();

        if (Options.emit_csharp)
        {
            Println("  private const int ZZ_BUFFERSIZE = " + scanner.bufferSize + ";");

            if (scanner.debugOption)
            {
                Println("  private static readonly string ZZ_NL = Environment.NewLine;");
            }

            Println("  /**");
            Println("   * This is used in 'if' statements to eliminate dead code");
            Println("   * warnings for 'break;' after the end of a user action");
            Println("   * block of code. The Java version does this by emitting");
            Println("   * a second 'case' which is impossible to reach. Since this");
            Println("   * is impossible for the compiler to deduce during semantic");
            Println("   * analysis, the warning is stifled. However, C# does not");
            Println("   * permit 'case' blocks to flow into each other, so the C#");
            Println("   * output mode needs a different approach. In this case,");
            Println("   * the entire user code is wrapped up in an 'if' statement");
            Println("   * whose condition is always true. No warning is emitted");
            Println("   * because the compiler doesn't strictly propagate the value");
            Println("   * of 'static readonly' fields, and thus does not semantically");
            Println("   * detect the fact that the 'if' will always be true.");
            Println("   */");
            Println("   public static readonly bool ZZ_SPURIOUS_WARNINGS_SUCK = true;");
        }
        else
        {
            Println("  private static final int ZZ_BUFFERSIZE = " + scanner.bufferSize + ";");

            if (scanner.debugOption)
            {
                Println("  private static final string ZZ_NL = System.getProperty(\"line.separator\");");
            }
        }

        skel.EmitNext();

        EmitLexicalStates();

        EmitCharMapArray();

        EmitActionTable();

        if (scanner.useRowMap)
        {
            ReduceRows();

            EmitRowMapArray();

            if (scanner.packed)
                EmitDynamicInit();
            else
                EmitZZTrans();
        }

        skel.EmitNext();

        if (scanner.useRowMap)
            EmitAttributes();

        skel.EmitNext();

        EmitClassCode();

        skel.EmitNext();

        EmitConstructorDecl();

        EmitCharMapInitFunction();

        skel.EmitNext();

        EmitScanError();

        skel.EmitNext();

        EmitDoEOF();

        skel.EmitNext();

        EmitLexFunctHeader();

        EmitNextInput();

        if (scanner.useRowMap)
            EmitGetRowMapNext();
        else
            EmitTransitionTable();

        if (scanner.lookAheadUsed)
            EmitPushback();

        skel.EmitNext();

        EmitActions();

        skel.EmitNext();

        EmitEOFVal();

        skel.EmitNext();

        EmitNoMatch();

        skel.EmitNext();

        EmitMain();

        skel.EmitNext();

        EmitEpilogue();

        writer.Close();
    }

}