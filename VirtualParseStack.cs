using System;
namespace CSFlex;

public class VirtualParseStack<T>
{
    protected int real_next;
    protected ListStack<T> real_stack;
    protected ListStack<int> vstack;

    public VirtualParseStack(ListStack<T> shadowing_stack)
    {
        this.real_stack = shadowing_stack ?? throw new Exception("Internal parser error: attempt to create null virtual stack");
        this.vstack = new ListStack<int>();
        this.real_next = 0;
        this.Get_from_real();
    }

    public bool IsEmpty =>
        this.vstack.IsEmpty;

    protected void Get_from_real()
    {
        if (this.real_next < this.real_stack.Count)
        {
            Symbol symbol = (Symbol)this.real_stack.ElementAt((this.real_stack.Count - 1) - this.real_next);
            this.real_next++;
            this.vstack.Push(symbol.parse_state);
        }
    }

    public void Pop()
    {
        if (this.IsEmpty)
        {
            throw new Exception("Internal parser error: pop from empty virtual stack");
        }
        this.vstack.Pop();
        if (this.IsEmpty)
        {
            this.Get_from_real();
        }
    }

    public void Push(int state_num)
    {
        this.vstack.Push(state_num);
    }

    public int Top()
    {
        if (this.IsEmpty)
        {
            throw new Exception("Internal parser error: top() called on empty virtual stack");
        }
        return (int)this.vstack.Peek();
    }
}

