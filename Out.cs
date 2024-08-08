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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CSFlex;


/**
 * In this class all output to the java console is filtered.
 *
 * Use the switches verbose, time and DUMP at compile time to determine
 * the verbosity of C# Flex output. There is no switch for
 * suppressing error messages. verbose and time can be overridden 
 * by command line paramters.
 *
 * Redirects output to a TextArea in GUI mode.
 *
 * Counts error and warning messages.
 *
 * @author Gerwin Klein
 * @version JFlex 1.4, $Revision: 2.8 $, $Date: 2004/04/12 10:07:47 $
 * @author Jonathan Gilbert
 * @version CSFlex 1.4
 */
public sealed class Out
{

    /** platform dependent newline sequence */
    public static readonly string NL = Environment.NewLine;

    /** count total warnings */
    private static int warnings;

    /** count total errors */
    private static int errors;

    /** output device */
    private static StdOutWriter writer = new ();


    /**
	 * Switches to GUI mode if <code>text</code> is not <code>null</code>
	 *
	 * @param text  the message RichTextBox of the C# Flex GUI
	 */
    public static void SetGUIMode(RichTextBox text)
    {
        writer.SetGUIMode(text);
    }

    /**
	 * Sets a new output stream and switches to non-gui mode.
	 * 
	 * @param stream  the new output stream
	 */
    public static void SetOutputStream(Stream stream)
    {
        writer = new StdOutWriter(stream);
        writer.SetGUIMode(null);
    }

    /**
	 * Report time statistic data.
	 *
	 * @param message  the message to be printed
	 * @param time     elapsed time
	 */
    public static void Time(ErrorMessages message, Timer time)
    {
        if (Options.time)
        {
            string msg = ErrorMessages.Get(message, time.ToString());
            writer.WriteLine(msg);
        }
    }
    public static void Time(ErrorMessages message, Stopwatch time)
    {
        if (Options.time)
        {
            string msg = ErrorMessages.Get(message, time.ToString());
            writer.WriteLine(msg);
        }
    }
    /**
	 * Report time statistic data.
	 *
	 * @param message  the message to be printed
	 */
    public static void Time(string message)
    {
        if (Options.time)
        {
            writer.WriteLine(message);
        }
    }

    /**
	 * Report generation progress.
	 *
	 * @param message  the message to be printed
	 */
    public static void Println(string message)
    {
        if (Options.verbose)
            writer.WriteLine(message);
    }

    /**
	 * Report generation progress.
	 *
	 * @param message  the message to be printed
	 * @param data     data to be inserted into the message
	 */
    public static void Println(ErrorMessages message, string data)
    {
        if (Options.verbose)
        {
            writer.WriteLine(ErrorMessages.Get(message, data));
        }
    }

    /**
	 * Report generation progress.
	 *
	 * @param message  the message to be printed
	 * @param data     data to be inserted into the message
	 */
    public static void Println(ErrorMessages message, int data)
    {
        if (Options.verbose)
        {
            writer.WriteLine(ErrorMessages.Get(message, data));
        }
    }

    /**
	 * Report generation progress.
	 *
	 * @param message  the message to be printed
	 */
    public static void Print(string message)
    {
        if (Options.verbose) writer.Write(message);
    }

    /**
	 * Dump debug information to System.out
	 *
	 * Use like this 
	 *
	 * <code>if (Out.DEBUG) Out.debug(message)</code>
	 *
	 * to save performance during normal operation (when DEBUG
	 * is turned off).
	 */
    public static void Debug(string message)
    {
        if (Options.DEBUG) Console.WriteLine(message);
    }


    /**
	 * All parts of C# Flex that want to provide dump information
	 * should use this method for their output.
	 *
	 * @message the message to be printed 
	 */
    public static void Dump(string message)
    {
        if (Options.dump) writer.WriteLine(message);
    }


    /**
	 * All parts of C# Flex that want to report error messages
	 * should use this method for their output.
	 *
	 * @message  the message to be printed
	 */
    private static void Err(string message)
    {
        writer.WriteLine(message);
    }


    /**
	 * throws a GeneratorException if there are any errors recorded
	 */
    public static void CheckErrors()
    {
        if (errors > 0) throw new GeneratorException();
    }


    /**
	 * print error and warning statistics
	 */
    public static void Statistics()
    {
        StringBuilder line = new StringBuilder(errors + " error");
        if (errors != 1) line.Append("s");

        line.AppendFormat(", {0} warning", warnings);
        if (warnings != 1) line.Append("s");

        line.Append(".");
        Err(line.ToString());
    }


    /**
	 * reset error and warning counters
	 */
    public static void ResetCounters()
    {
        errors = 0;
        warnings = 0;
    }


    /**
	 * print a warning without position information
	 *
	 * @param message   the warning message
	 */
    public static void Warning(string message)
    {
        warnings++;

        Err(NL + "Warning : " + message);
    }


    /**
	 * print a warning with line information
	 *
	 * @param message  code of the warning message
	 * @param line     the line information
	 *
	 * @see ErrorMessages
	 */
    public static void Warning(ErrorMessages message, int line)
    {
        warnings++;

        string msg = NL + "Warning";
        if (line > 0) msg = msg + " in line " + (line + 1);

        Err(msg + ": " + ErrorMessages.Get(message));
    }


    /**
	 * print warning message with location information
	 *
	 * @param file     the file the warning is issued for
	 * @param message  the code of the message to print
	 * @param line     the line number of the position
	 * @param column   the column of the position
	 */
    public static void Warning(File file, ErrorMessages message, int line, int column)
    {

        string msg = NL + "Warning";
        if (file != null) msg += " in file \"" + file + "\"";
        if (line >= 0) msg = msg + " (line " + (line + 1) + ")";

        try
        {
            Err(msg + ": " + NL + ErrorMessages.Get(message));
        }
        catch (IndexOutOfRangeException)
        {
            Err(msg);
        }

        warnings++;

        if (line >= 0)
        {
            if (column >= 0)
                ShowPosition(file, line, column);
            else
                ShowPosition(file, line);
        }
    }


    /**
	 * print error message (string)
	 *
	 * @param message  the message to print
	 */
    public static void Error(string message)
    {
        errors++;
        Err(NL + message);
    }


    /**
	 * print error message (code)
	 *  
	 * @param message  the code of the error message
	 *
	 * @see ErrorMessages   
	 */
    public static void Error(ErrorMessages message)
    {
        errors++;
        Err(NL + "Error: " + ErrorMessages.Get(message));
    }


    /**
	 * print error message with data 
	 *  
	 * @param data     data to insert into the message
	 * @param message  the code of the error message
	 *
	 * @see ErrorMessages   
	 */
    public static void Error(ErrorMessages message, string data)
    {
        errors++;
        Err(NL + "Error: " + ErrorMessages.Get(message, data));
    }


    /**
	 * IO error message for a file (displays file 
	 * name in parentheses).
	 *
	 * @param message  the code of the error message
	 * @param file     the file it occurred for
	 */
    public static void Error(ErrorMessages message, File file)
    {
        errors++;
        Err(NL + "Error: " + ErrorMessages.Get(message) + " (" + file + ")");
    }


    /**
	 * print error message with location information
	 *
	 * @param file     the file the error occurred for
	 * @param message  the code of the error message to print
	 * @param line     the line number of error position
	 * @param column   the column of error position
	 */
    public static void Error(File file, ErrorMessages message, int line, int column)
    {

        string msg = NL + "Error";
        if (file != null) msg += " in file \"" + file + "\"";
        if (line >= 0) msg = msg + " (line " + (line + 1) + ")";

        try
        {
            Err(msg + ": " + NL + ErrorMessages.Get(message));
        }
        catch (IndexOutOfRangeException)
        {
            Err(msg);
        }

        errors++;

        if (line >= 0)
        {
            if (column >= 0)
                ShowPosition(file, line, column);
            else
                ShowPosition(file, line);
        }
    }


    /**
	 * prints a line of a file with marked position.
	 *
	 * @param file    the file of which to show the line
	 * @param line    the line to show
	 * @param column  the column in which to show the marker
	 */
    public static void ShowPosition(File file, int line, int column)
    {
        try
        {
            string ln = GetLine(file, line);
            if (ln != null)
            {
                Err(ln);

                if (column < 0) return;

                string t = "^";
                for (int i = 0; i < column; i++) t = " " + t;

                Err(t);
            }
        }
        catch (IOException)
        {
            /* silently ignore IO errors, don't show anything */
        }
    }


    /**
	 * print a line of a file
	 *
	 * @param file  the file to show
	 * @param line  the line number 
	 */
    public static void ShowPosition(File file, int line)
    {
        try
        {
            string ln = GetLine(file, line);
            if (ln != null) Err(ln);
        }
        catch (IOException)
        {
            /* silently ignore IO errors, don't show anything */
        }
    }


    /**
	 * get one line from a file 
	 *
	 * @param file   the file to read
	 * @param line   the line number to get
	 *
	 * @throw IOException  if any error occurs
	 */
    private static string GetLine(File file, int line)
    {
        StreamReader reader = new StreamReader(file);

        string msg = "";

        for (int i = 0; i <= line; i++)
            msg = reader.ReadLine();

        reader.Close();

        return msg;
    }


    /**
	 * Print system information (e.g. in case of unexpected exceptions)
	 */
    public static void PrintSystemInfo()
    {
        Err(".NET version:    " + Environment.Version);
        Err("OS platform:     " + Environment.OSVersion.Platform);
        Err("OS version:      " + Environment.OSVersion.Version);
        Err("Encoding:        " + Encoding.Default.EncodingName);
        Err("C# Flex version: " + MainClass.version);
        /*
		err("Java version:  "+System.getProperty("java.version"));
		err("Runtime name:  "+System.getProperty("java.runtime.name"));
		err("Vendor:        "+System.getProperty("java.vendor")); 
		err("VM version:    "+System.getProperty("java.vm.version")); 
		err("VM vendor:     "+System.getProperty("java.vm.vendor"));
		err("VM name:       "+System.getProperty("java.vm.name"));
		err("VM info:       "+System.getProperty("java.vm.info"));
		err("OS name:       "+System.getProperty("os.name"));
		err("OS arch:       "+System.getProperty("os.arch"));
		err("OS version:    "+System.getProperty("os.version"));
		err("Encoding:      "+System.getProperty("file.encoding"));
		err("JFlex version: "+Main.version);
		/* */
    }


    /**
	 * Request a bug report for an unexpected Exception/Error.
	 */
    public static void RequestBugReport(Exception e)
    {
        Err("An unexpected error occurred. Please file a report at");
        Err("http://sourceforge.net/projects/csflex/ , and include the");
        Err("following information:");
        Err("");
        PrintSystemInfo();
        Err("Exception:");
        writer.WriteLine(e.ToString());
        Err("");
        Err("Please also include a specification (as small as possible)");
        Err("that triggers this error. You may also want to check at");
        Err("http://sourceforge.net/projects/csflex/ if there is a newer");
        Err("version available that doesn't have this problem.");
        Err("");
        Err("Thanks for your support.");
    }
}