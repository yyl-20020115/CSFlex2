using System.Collections.Generic;

namespace CSFlex;
public class ListStack<T>
{
    private readonly List<T> back = [];

    public void Clear()
    {
        this.back.Clear();
    }

    public object ElementAt(int idx) =>
        this.back[idx];

    public bool IsEmpty =>
        (this.back.Count == 0);

    public T Peek() =>
        this.back[this.back.Count - 1];

    public T Pop()
    {
        T obj2 = default;
        try
        {
            obj2 = this.Peek();
        }
        finally
        {
            this.back.RemoveAt(this.back.Count - 1);
        }
        return obj2;
    }

    public void Push(T item)
    {
        this.back.Add(item);
    }

    public void SetElementAt(T new_item, int idx)
    {
        this.back[idx] = new_item;
    }

    public int Count =>
        this.back.Count;
}

