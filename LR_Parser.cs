namespace CSFlex;

using System;
using System.Text;
public abstract class LR_Parser
{
    protected bool _done_parsing;
    protected const int _error_sync_size = 3;
    private IScanner _scanner;
    protected short[][] action_tab;
    protected Symbol cur_token;
    protected Symbol[] lookahead;
    protected int lookahead_pos;
    protected short[][] production_tab;
    protected short[][] reduce_tab;
    protected ListStack<Symbol> stack;
    protected int tos;

    public LR_Parser()
    {
        this._done_parsing = false;
        this.stack = new ListStack<Symbol>();
    }

    public LR_Parser(IScanner s) : this()
    {
        this.SetScanner(s);
    }

    public abstract short[][] Action_table();
    protected bool Advance_lookahead()
    {
        this.lookahead_pos++;
        return (this.lookahead_pos < this.Error_sync_size());
    }

    protected Symbol Cur_err_token() =>
        this.lookahead[this.lookahead_pos];

    public virtual void Debug_message(string mess)
    {
        Console.Error.WriteLine(mess);
    }

    public Symbol Debug_parse()
    {
        Symbol item = null;
        this.production_tab = this.Production_table();
        this.action_tab = this.Action_table();
        this.reduce_tab = this.Reduce_table();
        this.Debug_message("# Initializing parser");
        this.Init_actions();
        this.User_init();
        this.cur_token = this.Scan();
        this.Debug_message("# Current Symbol is #" + this.cur_token.symbol);
        this.stack.Clear();
        this.stack.Push(new Symbol(0, this.Start_state()));
        this.tos = 0;
        this._done_parsing = false;
        while (!this._done_parsing)
        {
            if (this.cur_token.used_by_parser)
            {
                throw new Exception("Symbol recycling detected (fix your scanner).");
            }
            int num = this.Get_action(((Symbol)this.stack.Peek()).parse_state, this.cur_token.symbol);
            if (num > 0)
            {
                this.cur_token.parse_state = num - 1;
                this.cur_token.used_by_parser = true;
                this.Debug_shift(this.cur_token);
                this.stack.Push(this.cur_token);
                this.tos++;
                this.cur_token = this.Scan();
                this.Debug_message("# Current token is " + this.cur_token);
            }
            else
            {
                if (num < 0)
                {
                    item = this.Do_action(-num - 1, this, this.stack, this.tos);
                    short num3 = this.production_tab[-num - 1][0];
                    short num2 = this.production_tab[-num - 1][1];
                    this.Debug_reduce(-num - 1, num3, num2);
                    for (int i = 0; i < num2; i++)
                    {
                        this.stack.Pop();
                        this.tos--;
                    }
                    num = this.Get_reduce(((Symbol)this.stack.Peek()).parse_state, num3);
                    this.Debug_message(string.Concat(new object[] { "# Reduce rule: top state ", ((Symbol)this.stack.Peek()).parse_state, ", lhs sym ", num3, " -> state ", num }));
                    item.parse_state = num;
                    item.used_by_parser = true;
                    this.stack.Push(item);
                    this.tos++;
                    this.Debug_message("# Goto state #" + num);
                    continue;
                }
                if (num == 0)
                {
                    this.Syntax_error(this.cur_token);
                    if (!this.Error_recovery(true))
                    {
                        this.Unrecovered_syntax_error(this.cur_token);
                        this.Done_parsing();
                        continue;
                    }
                    item = (Symbol)this.stack.Peek();
                }
            }
        }
        return item;
    }

    public virtual void Debug_reduce(int prod_num, int nt_num, int rhs_size)
    {
        this.Debug_message(string.Concat(new object[] { "# Reduce with prod #", prod_num, " [NT=", nt_num, ", SZ=", rhs_size, "]" }));
    }

    public virtual void Debug_shift(Symbol shift_tkn)
    {
        this.Debug_message(string.Concat(new object[] { "# Shift under term #", shift_tkn.symbol, " to state #", shift_tkn.parse_state }));
    }

    public virtual void Debug_stack()
    {
        StringBuilder builder = new StringBuilder("## STACK:");
        for (int i = 0; i < this.stack.Count; i++)
        {
            Symbol symbol = (Symbol)this.stack.ElementAt(i);
            builder.AppendFormat(" <state {0}, sym {1}>", symbol.parse_state, symbol.symbol);
            if (((i % 3) == 2) || (i == (this.stack.Count - 1)))
            {
                this.Debug_message(builder.ToString());
                builder = new StringBuilder("         ");
            }
        }
    }

    public abstract Symbol Do_action(int act_num, LR_Parser parser, ListStack<Symbol> stack, int top);
    public void Done_parsing()
    {
        this._done_parsing = true;
    }

    public virtual void Dump_stack()
    {
        if (this.stack == null)
        {
            this.Debug_message("# Stack dump requested, but stack is null");
        }
        else
        {
            this.Debug_message("============ Parse Stack Dump ============");
            for (int i = 0; i < this.stack.Count; i++)
            {
                this.Debug_message(string.Concat(new object[] { "Symbol: ", ((Symbol)this.stack.ElementAt(i)).symbol, " State: ", ((Symbol)this.stack.ElementAt(i)).parse_state }));
            }
            this.Debug_message("==========================================");
        }
    }

    public abstract int EOF_sym();
    protected bool Error_recovery(bool debug)
    {
        if (debug)
        {
            this.Debug_message("# Attempting error recovery");
        }
        if (!this.Find_recovery_config(debug))
        {
            if (debug)
            {
                this.Debug_message("# Error recovery fails");
            }
            return false;
        }
        this.Read_lookahead();
        while (true)
        {
            if (debug)
            {
                this.Debug_message("# Trying to parse ahead");
            }
            if (this.Try_parse_ahead(debug))
            {
                break;
            }
            if (this.lookahead[0].symbol == this.EOF_sym())
            {
                if (debug)
                {
                    this.Debug_message("# Error recovery fails at EOF");
                }
                return false;
            }
            if (debug)
            {
                this.Debug_message("# Consuming Symbol #" + this.lookahead[0].symbol);
            }
            this.Restart_lookahead();
        }
        if (debug)
        {
            this.Debug_message("# Parse-ahead ok, going back to normal parse");
        }
        this.Parse_lookahead(debug);
        return true;
    }

    public abstract int Error_sym();
    protected int Error_sync_size() =>
        3;

    protected bool Find_recovery_config(bool debug)
    {
        if (debug)
        {
            this.Debug_message("# Finding recovery state on stack");
        }
        int right = ((Symbol)this.stack.Peek()).right;
        int left = ((Symbol)this.stack.Peek()).left;
        while (!this.Shift_under_error())
        {
            if (debug)
            {
                this.Debug_message("# Pop stack by one, state was # " + ((Symbol)this.stack.Peek()).parse_state);
            }
            left = ((Symbol)this.stack.Pop()).left;
            this.tos--;
            if (this.stack.IsEmpty)
            {
                if (debug)
                {
                    this.Debug_message("# No recovery state found on stack");
                }
                return false;
            }
        }
        int num = this.Get_action(((Symbol)this.stack.Peek()).parse_state, this.Error_sym());
        if (debug)
        {
            this.Debug_message("# Recover state found (#" + ((Symbol)this.stack.Peek()).parse_state + ")");
            this.Debug_message("# Shifting on error to state #" + (num - 1));
        }
        Symbol item = new Symbol(this.Error_sym(), left, right)
        {
            parse_state = num - 1,
            used_by_parser = true
        };
        this.stack.Push(item);
        this.tos++;
        return true;
    }

    protected short Get_action(int state, int sym)
    {
        int num4;
        short[] numArray = this.action_tab[state];
        if (numArray.Length < 20)
        {
            num4 = 0;
            while (num4 < numArray.Length)
            {
                short num = numArray[num4++];
                if ((num == sym) || (num == -1))
                {
                    return numArray[num4];
                }
                num4++;
            }
        }
        else
        {
            int num2 = 0;
            int num3 = ((numArray.Length - 1) / 2) - 1;
            while (num2 <= num3)
            {
                num4 = (num2 + num3) / 2;
                if (sym == numArray[num4 * 2])
                {
                    return numArray[(num4 * 2) + 1];
                }
                if (sym > numArray[num4 * 2])
                {
                    num2 = num4 + 1;
                }
                else
                {
                    num3 = num4 - 1;
                }
            }
            return numArray[numArray.Length - 1];
        }
        return 0;
    }

    protected short Get_reduce(int state, int sym)
    {
        short[] numArray = this.reduce_tab[state];
        if (numArray != null)
        {
            for (int i = 0; i < numArray.Length; i++)
            {
                short num = numArray[i++];
                if ((num == sym) || (num == -1))
                {
                    return numArray[i];
                }
            }
        }
        return -1;
    }

    public IScanner GetScanner() =>
        this._scanner;

    protected abstract void Init_actions();
    public Symbol Parse()
    {
        Symbol item = null;
        this.production_tab = this.Production_table();
        this.action_tab = this.Action_table();
        this.reduce_tab = this.Reduce_table();
        this.Init_actions();
        this.User_init();
        this.cur_token = this.Scan();
        this.stack.Clear();
        this.stack.Push(new Symbol(0, this.Start_state()));
        this.tos = 0;
        this._done_parsing = false;
        while (!this._done_parsing)
        {
            if (this.cur_token.used_by_parser)
            {
                throw new Exception("Symbol recycling detected (fix your scanner).");
            }
            int num = this.Get_action(((Symbol)this.stack.Peek()).parse_state, this.cur_token.symbol);
            if (num > 0)
            {
                this.cur_token.parse_state = num - 1;
                this.cur_token.used_by_parser = true;
                this.stack.Push(this.cur_token);
                this.tos++;
                this.cur_token = this.Scan();
            }
            else
            {
                if (num < 0)
                {
                    item = this.Do_action(-num - 1, this, this.stack, this.tos);
                    short sym = this.production_tab[-num - 1][0];
                    short num2 = this.production_tab[-num - 1][1];
                    for (int i = 0; i < num2; i++)
                    {
                        this.stack.Pop();
                        this.tos--;
                    }
                    num = this.Get_reduce(((Symbol)this.stack.Peek()).parse_state, sym);
                    item.parse_state = num;
                    item.used_by_parser = true;
                    this.stack.Push(item);
                    this.tos++;
                    continue;
                }
                if (num == 0)
                {
                    this.Syntax_error(this.cur_token);
                    if (!this.Error_recovery(false))
                    {
                        this.Unrecovered_syntax_error(this.cur_token);
                        this.Done_parsing();
                        continue;
                    }
                    item = (Symbol)this.stack.Peek();
                }
            }
        }
        return item;
    }

    protected void Parse_lookahead(bool debug)
    {
        Symbol item = null;
        this.lookahead_pos = 0;
        if (debug)
        {
            this.Debug_message("# Reparsing saved input with actions");
            this.Debug_message("# Current Symbol is #" + this.Cur_err_token().symbol);
            this.Debug_message("# Current state is #" + ((Symbol)this.stack.Peek()).parse_state);
        }
        while (!this._done_parsing)
        {
            int num = this.Get_action(((Symbol)this.stack.Peek()).parse_state, this.Cur_err_token().symbol);
            if (num > 0)
            {
                this.Cur_err_token().parse_state = num - 1;
                this.Cur_err_token().used_by_parser = true;
                if (debug)
                {
                    this.Debug_shift(this.Cur_err_token());
                }
                this.stack.Push(this.Cur_err_token());
                this.tos++;
                if (!this.Advance_lookahead())
                {
                    if (debug)
                    {
                        this.Debug_message("# Completed reparse");
                    }
                    break;
                }
                if (debug)
                {
                    this.Debug_message("# Current Symbol is #" + this.Cur_err_token().symbol);
                }
            }
            else
            {
                if (num < 0)
                {
                    item = this.Do_action(-num - 1, this, this.stack, this.tos);
                    short num3 = this.production_tab[-num - 1][0];
                    short num2 = this.production_tab[-num - 1][1];
                    if (debug)
                    {
                        this.Debug_reduce(-num - 1, num3, num2);
                    }
                    for (int i = 0; i < num2; i++)
                    {
                        this.stack.Pop();
                        this.tos--;
                    }
                    num = this.Get_reduce(((Symbol)this.stack.Peek()).parse_state, num3);
                    item.parse_state = num;
                    item.used_by_parser = true;
                    this.stack.Push(item);
                    this.tos++;
                    if (debug)
                    {
                        this.Debug_message("# Goto state #" + num);
                    }
                    continue;
                }
                if (num == 0)
                {
                    this.Report_fatal_error("Syntax error", item);
                    break;
                }
            }
        }
    }

    public abstract short[][] Production_table();
    protected void Read_lookahead()
    {
        this.lookahead = new Symbol[this.Error_sync_size()];
        for (int i = 0; i < this.Error_sync_size(); i++)
        {
            this.lookahead[i] = this.cur_token;
            this.cur_token = this.Scan();
        }
        this.lookahead_pos = 0;
    }

    public abstract short[][] Reduce_table();
    public virtual void Report_error(string message, object info)
    {
        Console.Error.Write(message);
        if (info is Symbol)
        {
            if (((Symbol)info).left != -1)
            {
                Console.Error.WriteLine(" at character {0} of input", ((Symbol)info).left);
            }
            else
            {
                Console.Error.WriteLine();
            }
        }
        else
        {
            Console.Error.WriteLine();
        }
    }

    public virtual void Report_fatal_error(string message, object info)
    {
        this.Done_parsing();
        this.Report_error(message, info);
        throw new Exception("Can't recover from previous error(s)");
    }

    protected void Restart_lookahead()
    {
        for (int i = 1; i < this.Error_sync_size(); i++)
        {
            this.lookahead[i - 1] = this.lookahead[i];
        }
        this.lookahead[this.Error_sync_size() - 1] = this.cur_token;
        this.cur_token = this.Scan();
        this.lookahead_pos = 0;
    }

    public virtual Symbol Scan()
    {
        Symbol symbol = this.GetScanner().Next_token();
        return ((symbol != null) ? symbol : new Symbol(this.EOF_sym()));
    }

    public void SetScanner(IScanner s)
    {
        this._scanner = s;
    }

    protected bool Shift_under_error() =>
        (this.Get_action(((Symbol)this.stack.Peek()).parse_state, this.Error_sym()) > 0);

    public abstract int Start_production();
    public abstract int Start_state();
    public virtual void Syntax_error(Symbol cur_token)
    {
        this.Report_error("Syntax error", cur_token);
    }

    protected bool Try_parse_ahead(bool debug)
    {
        VirtualParseStack<Symbol> _stack = new VirtualParseStack<Symbol>(this.stack);
        while (true)
        {
            int num = this.Get_action(_stack.Top(), this.Cur_err_token().symbol);
            if (num == 0)
            {
                return false;
            }
            if (num > 0)
            {
                _stack.Push(num - 1);
                if (debug)
                {
                    this.Debug_message(string.Concat(new object[] { "# Parse-ahead shifts Symbol #", this.Cur_err_token().symbol, " into state #", num - 1 }));
                }
                if (!this.Advance_lookahead())
                {
                    return true;
                }
            }
            else
            {
                if ((-num - 1) == this.Start_production())
                {
                    if (debug)
                    {
                        this.Debug_message("# Parse-ahead accepts");
                    }
                    return true;
                }
                short sym = this.production_tab[-num - 1][0];
                short num3 = this.production_tab[-num - 1][1];
                for (int i = 0; i < num3; i++)
                {
                    _stack.Pop();
                }
                if (debug)
                {
                    this.Debug_message(string.Concat(new object[] { "# Parse-ahead reduces: handle size = ", num3, " lhs = #", sym, " from state #", _stack.Top() }));
                }
                _stack.Push(this.Get_reduce(_stack.Top(), sym));
                if (debug)
                {
                    this.Debug_message("# Goto state #" + _stack.Top());
                }
            }
        }
    }

    protected static short[][] UnpackFromShorts(short[] sb)
    {
        int index = 0;
        int num2 = (sb[index] << 0x10) | ((ushort)sb[index + 1]);
        index += 2;
        short[][] numArray = new short[num2][];
        for (int i = 0; i < num2; i++)
        {
            int num4 = (sb[index] << 0x10) | ((ushort)sb[index + 1]);
            index += 2;
            numArray[i] = new short[num4];
            for (int j = 0; j < num4; j++)
            {
                numArray[i][j] = (short)(sb[index++] - 2);
            }
        }
        return numArray;
    }

    protected static short[][] UnpackFromStrings(string[] sa)
    {
        StringBuilder builder = new StringBuilder(sa[0]);
        for (int i = 1; i < sa.Length; i++)
        {
            builder.Append(sa[i]);
        }
        int num2 = 0;
        int num3 = (builder[num2] << 0x10) | builder[num2 + 1];
        num2 += 2;
        short[][] numArray = new short[num3][];
        for (int j = 0; j < num3; j++)
        {
            int num5 = (builder[num2] << 0x10) | builder[num2 + 1];
            num2 += 2;
            numArray[j] = new short[num5];
            for (int k = 0; k < num5; k++)
            {
                numArray[j][k] = (short)(builder[num2++] - '\x0002');
            }
        }
        return numArray;
    }

    public virtual void Unrecovered_syntax_error(Symbol cur_token)
    {
        this.Report_fatal_error("Couldn't repair and continue parse", cur_token);
    }

    public virtual void User_init()
    {
    }
}

