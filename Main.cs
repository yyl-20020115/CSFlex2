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
using System.Diagnostics;
using System.Collections.Generic;

namespace CSFlex;


/**
	 * This is the main class of C# Flex controlling the scanner generation process. 
	 * It is responsible for parsing the commandline, getting input files,
	 * starting up the GUI if necessary, etc. 
	 *
	 * @author Gerwin Klein
	 * @version JFlex 1.4, $Revision: 2.18 $, $Date: 2004/04/12 10:34:10 $
	 * @author Jonathan Gilbert
	 * @version CSFlex 1.4
	 */
public class MainClass
{
    //command line: -nested -cs LexScan.flex
    /** C# Flex version */
    public const string version = "1.4"; //$NON-NLS-1$

    /**
		 * Generates a scanner for the specified input file.
		 *
		 * @param inputFile  a file containing a lexical specification
		 *                   to generate a scanner for.
		 */
    public static void Generate(File inputFile)
    {

        Out.ResetCounters();

        var totalTime = new Stopwatch();
        var time = new Stopwatch();

        LexScan scanner = null;
        LexParse parser = null;
        TextReader inputReader = null;

        totalTime.Start();

        try
        {
            Out.Println(ErrorMessages.READING, inputFile.ToString());
            inputReader = new StreamReader(inputFile);
            scanner = new LexScan(inputReader);
            scanner.SetFile(inputFile);
            parser = new LexParse(scanner);
        }
        catch (FileNotFoundException)
        {
            Out.Error(ErrorMessages.CANNOT_OPEN, inputFile.ToString());
            throw new GeneratorException();
        }

        try
        {
            NFA nfa = (NFA)parser.Parse().value;

            Out.CheckErrors();

            if (Options.dump) Out.Dump(ErrorMessages.Get(ErrorMessages.NFA_IS) +
                                       Out.NL + nfa + Out.NL);

            if (Options.dot)
                nfa.WriteDot(Emitter.Normalize("nfa.dot", null));       //$NON-NLS-1$

            Out.Println(ErrorMessages.NFA_STATES, nfa.numStates);

            time.Start();
            DFA dfa = nfa.GetDFA();
            time.Stop();
            Out.Time(ErrorMessages.DFA_TOOK, time);

            dfa.CheckActions(scanner, parser);

            nfa = null;

            if (Options.dump) Out.Dump(ErrorMessages.Get(ErrorMessages.DFA_IS) +
                                       Out.NL + dfa + Out.NL);

            if (Options.dot)
                dfa.WriteDot(Emitter.Normalize("dfa-big.dot", null)); //$NON-NLS-1$

            time.Start();
            dfa.Minimize();
            time.Stop();

            Out.Time(ErrorMessages.MIN_TOOK, time);

            if (Options.dump)
                Out.Dump(ErrorMessages.Get(ErrorMessages.MIN_DFA_IS) +
                                           Out.NL + dfa);

            if (Options.dot)
                dfa.WriteDot(Emitter.Normalize("dfa-min.dot", null)); //$NON-NLS-1$

            time.Start();

            Emitter e = new Emitter(inputFile, parser, dfa);
            e.Emit();

            time.Stop();

            Out.Time(ErrorMessages.WRITE_TOOK, time);

            totalTime.Stop();

            Out.Time(ErrorMessages.TOTAL_TIME, totalTime);
        }
        catch (ScannerException e)
        {
            Out.Error(e.file, e.message, e.line, e.column);
            throw new GeneratorException();
        }
        catch (MacroException e)
        {
            Out.Error(e.Message);
            throw new GeneratorException();
        }
        catch (IOException e)
        {
            Out.Error(ErrorMessages.IO_ERROR, e.ToString());
            throw new GeneratorException();
        }
        catch (OutOfMemoryException)
        {
            Out.Error(ErrorMessages.OUT_OF_MEMORY);
            throw new GeneratorException();
        }
        catch (GeneratorException)
        {
            throw new GeneratorException();
        }
        catch (Exception e)
        {
            Out.Error(e.ToString());
            throw new GeneratorException();
        }

    }

    public static List<File> ParseOptions(string[] argv)
    {
        List<File> files = new PrettyList<File>();

        for (int i = 0; i < argv.Length; i++)
        {

            if ((argv[i] == "-d") || (argv[i] == "--outdir"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                if (++i >= argv.Length)
                {
                    Out.Error(ErrorMessages.NO_DIRECTORY);
                    throw new GeneratorException();
                }
                Options.SetDir(argv[i]);
                continue;
            }

            if ((argv[i] == "--skel") || (argv[i] == "-skel"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                if (++i >= argv.Length)
                {
                    Out.Error(ErrorMessages.NO_SKEL_FILE);
                    throw new GeneratorException();
                }

                Options.SetSkeleton(new File(argv[i]));
                continue;
            }

            if ((argv[i] == "--nested-default-skeleton") || (argv[i] == "-nested"))
            {
                Options.SetSkeleton(new File("<nested>"));
                continue;
            }

            if ((argv[i] == "-jlex") || (argv[i] == "--jlex"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.jlex = true;
                continue;
            }

            if ((argv[i] == "-v") || (argv[i] == "--verbose") || (argv[i] == "-verbose"))
            { //$NON-NLS-1$ //$NON-NLS-2$ //$NON-NLS-3$
                Options.verbose = true;
                Options.progress = true;
                continue;
            }

            if ((argv[i] == "-q") || (argv[i] == "--quiet") || (argv[i] == "-quiet"))
            { //$NON-NLS-1$ //$NON-NLS-2$ //$NON-NLS-3$
                Options.verbose = false;
                Options.progress = false;
                continue;
            }

            if ((argv[i] == "--dump") || (argv[i] == "-dump"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.dump = true;
                continue;
            }

            if ((argv[i] == "--time") || (argv[i] == "-time"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.time = true;
                continue;
            }

            if ((argv[i] == "--version") || (argv[i] == "-version"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Out.Println(ErrorMessages.THIS_IS_CSFLEX, version);
                throw new SilentExit();
            }

            if ((argv[i] == "--dot") || (argv[i] == "-dot"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.dot = true;
                continue;
            }

            if ((argv[i] == "--help") || (argv[i] == "-h") || (argv[i] == "/h"))
            { //$NON-NLS-1$ //$NON-NLS-2$ //$NON-NLS-3$
                PrintUsage();
                throw new SilentExit();
            }

            if ((argv[i] == "--info") || (argv[i] == "-info"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Out.PrintSystemInfo();
                throw new SilentExit();
            }

            if ((argv[i] == "--nomin") || (argv[i] == "-nomin"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.no_minimize = true;
                continue;
            }

            if ((argv[i] == "--pack") || (argv[i] == "-pack"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.gen_method = Options.PACK;
                continue;
            }

            if ((argv[i] == "--table") || (argv[i] == "-table"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.gen_method = Options.TABLE;
                continue;
            }

            if ((argv[i] == "--switch") || (argv[i] == "-switch"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.gen_method = Options.SWITCH;
                continue;
            }

            if ((argv[i] == "--nobak") || (argv[i] == "-nobak"))
            { //$NON-NLS-1$ //$NON-NLS-2$
                Options.no_backup = true;
                continue;
            }

            if ((argv[i] == "--csharp") || (argv[i] == "-cs"))
            {
                Options.emit_csharp = true;
                continue;
            }

            if (argv[i].StartsWith("-"))
            { //$NON-NLS-1$
                Out.Error(ErrorMessages.UNKNOWN_COMMANDLINE, argv[i]);
                PrintUsage();
                throw new SilentExit();
            }

            // if argv[i] is not an option, try to read it as file 
            File f = new File(argv[i]);
            if (f.IsFile() && f.CanRead())
                files.Add(f);
            else
            {
                Out.Error("Sorry, couldn't open \"" + f + "\""); //$NON-NLS-2$
                throw new GeneratorException();
            }
        }

        return files;
    }


    public static void PrintUsage()
    {
        Out.Println(""); //$NON-NLS-1$
        Out.Println("Usage: csflex <options> <input-files>");
        Out.Println("");
        Out.Println("Where <options> can be one or more of");
        Out.Println("-d <directory>   write generated file to <directory>");
        Out.Println("--skel <file>    use external skeleton <file>");
        Out.Println("--switch");
        Out.Println("--table");
        Out.Println("--pack           set default code generation method");
        Out.Println("--jlex           strict JLex compatibility");
        Out.Println("--nomin          skip minimization step");
        Out.Println("--nobak          don't create backup files");
        Out.Println("--dump           display transition tables");
        Out.Println("--dot            write graphviz .dot files for the generated automata (alpha)");
        Out.Println("--nested-default-skeleton");
        Out.Println("-nested          use the skeleton with support for nesting (included files)");
        Out.Println("--csharp         ***");
        Out.Println("-csharp          * Important: Enable C# code generation");
        Out.Println("--verbose        ***");
        Out.Println("-v               display generation progress messages (default)");
        Out.Println("--quiet");
        Out.Println("-q               display errors only");
        Out.Println("--time           display generation time statistics");
        Out.Println("--version        print the version number of this copy of C# Flex");
        Out.Println("--info           print system + JDK information");
        Out.Println("--help");
        Out.Println("-h               print this message");
        Out.Println("");
        Out.Println(ErrorMessages.THIS_IS_CSFLEX, version);
        Out.Println("Have a nice day!");
    }

    //[System.Runtime.InteropServices.DllImport("kernel32")]
    //private static extern bool FreeConsole();

    public static void Generate(string[] argv)
    {
        var files = ParseOptions(argv);

        if (files.Count > 0)
        {
            for (int i = 0; i < files.Count; i++)
                Generate((File)files[i]);
        }
        //else
        //{
        //	FreeConsole();
        //	try
        //	{
        //		Application.Run(new MainFrame());
        //	}
        //	catch { }
        //}
    }


    /**
		 * Starts the generation process with the files in <code>argv</code> or
		 * pops up a window to choose a file, when <code>argv</code> doesn't have
		 * any file entries.
		 *
		 * @param argv the commandline.
		 */
    public static int Main(string[] argv)
    {
        int ret = 0;
        try
        {
            Generate(argv);
        }
        catch (GeneratorException)
        {
            Out.Statistics();
            ret = 1;
        }
        catch (SilentExit)
        {
            ret = 1;
        }
        return ret;
    }
}
