/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * C# Flex 1.4                                                             *
 * Copyright (C) 2004-2005  Jonathan Gilbert <logic@deltaq.org>            *
 * All rights reserved.                                                    *
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

public class RuntimeException : Exception
{
    public RuntimeException()
    {
    }

    public RuntimeException(string msg)
      : base(msg)
    {
    }
}

public class File
{
    private readonly string name;

    public File(string parent, string child)
    {
        name = Path.Combine(parent, child);
    }

    public File(string filename)
    {
        name = filename;
    }

    public static implicit operator string(File file) => file.name;

    public string GetParent()
    {
        var ret = Path.GetDirectoryName(name);

        return ret.Length == 0 ? null : ret;
    }

    public bool Exists() => System.IO.File.Exists(name);

    public bool Delete()
    {
        var info = new FileInfo(name);

        try
        {
            info.Delete();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RenameTo(File dest)
    {
        FileInfo info = new FileInfo(name);

        try
        {
            info.MoveTo(dest.name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static readonly char separatorChar = Path.DirectorySeparatorChar;

    public bool CanRead()
    {
        FileStream stream = null;

        try
        {
            stream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (stream != null)
                stream.Close();
        }
    }

    public bool IsFile()
    {
        FileInfo info = new FileInfo(name);

        return (info.Attributes & FileAttributes.Directory) != FileAttributes.Directory;
    }

    public bool IsDirectory()
    {
        FileInfo info = new FileInfo(name);

        return (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
    }

    public bool Mkdirs()
    {
        FileInfo info = new FileInfo(name);

        System.Collections.Stack needed = new System.Collections.Stack();

        DirectoryInfo parent = info.Directory;

        try
        {
            while (!parent.Exists)
            {
                needed.Push(parent);
                parent = parent.Parent;
            }

            while (needed.Count > 0)
            {
                DirectoryInfo dir = (DirectoryInfo)needed.Pop();
                dir.Create();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString()
    {
        return name;
    }

}

public class IntegerUtil
{
    public static string ToOctalString(int c)
    {
        StringBuilder ret = new StringBuilder();

        while (c > 0)
        {
            int unit_place = (c & 7);
            c >>= 3;

            ret.Insert(0, (char)(unit_place + '0'));
        }

        if (ret.Length == 0)
            return "0";
        else
            return ret.ToString();
    }

    public static string ToHexString(int c)
    {
        StringBuilder ret = new StringBuilder();

        while (c > 0)
        {
            int unit_place = (c & 15);
            c >>= 4;

            if (unit_place >= 10)
                ret.Insert(0, (char)(unit_place + 'a' - 10));
            else
                ret.Insert(0, (char)(unit_place + '0'));
        }

        if (ret.Length == 0)
            return "0";
        else
            return ret.ToString();
    }

    public static int ParseInt(string s)
    {
        return ParseInt(s, 10);
    }

    public static int ParseInt(string s, int @base)
    {
        const string alpha = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        if ((@base < 2) || (@base > 36))
            throw new ArgumentException("Number base cannot be less than 2 or greater than 36", "base");

        s = s.ToUpper();

        int value = 0;

        for (int i = 0; i < s.Length; i++)
        {
            int idx = alpha.IndexOf(s[i]);

            if ((idx < 0) || (idx >= @base))
                throw new FormatException("'" + s[i] + "' is not a valid base-" + @base + " digit");

            value = (value * @base) + idx;
        }

        return value;
    }
}


public class PrettyList<T> : List<T>
{
    public PrettyList(ICollection<T> c)
      : base(c)
    {
    }

    public PrettyList(int capacity)
      : base(capacity)
    {
    }

    public PrettyList()
    {
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("[");

        for (int i = 0; i < Count; i++)
        {
            if (i > 0)
                builder.Append(", ");
            builder.Append(this[i]);
        }

        builder.Append("]");

        return builder.ToString();
    }
}

public class PrettyHashtable<K, T> : Dictionary<K, T>
{
    public PrettyHashtable(int capacity)
      : base(capacity)
    {
    }

    public PrettyHashtable()
    {
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("{");

        IDictionaryEnumerator enumerator = GetEnumerator();

        if (enumerator.MoveNext())
            builder.AppendFormat("{0}={1}", enumerator.Key, enumerator.Value);
        while (enumerator.MoveNext())
            builder.AppendFormat(",{0}={1}", enumerator.Key, enumerator.Value);

        builder.Append("}");

        return builder.ToString();
    }
}
