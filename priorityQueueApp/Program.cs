using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine($"hello dotnet on apple m1 {DateTime.Now}");
        // C# PriorityQueue
        var pq = new PQ();
        pq.Add("mon");
        pq.Add("xyz");
        pq.Add("mon");
        pq.Add("abc");
        pq.Add("wx");
        Console.WriteLine($"PQ Peek {pq.Peek()}");
        // if fail on assert, program is stopped.
        Debug.Assert(pq.Peek() != "abc", "PQ peek should be wx");
        Debug.Assert(pq.Peek() == "wx", "PQ peek should be wx");
        pq.Get();
        Console.WriteLine($"PQ Peek {pq.Peek()}");
        Console.WriteLine("loop through PQ");
        while (pq.Any()) Console.WriteLine(pq.Get());
        // unit test
        Console.WriteLine("run test on PQ");
        PQtest.Test1();

        var q = new Queue<string>();
        q.Enqueue("abc");
        q.Enqueue("xby");
        q.Enqueue("abc");
        Console.WriteLine("loop through q");
        while (q.Any()) Console.WriteLine(q.Dequeue());
    }
}
public static class PQtest
{
    public static void Test1()
    {
        var pq = new PQ();
        pq.Add("mon");
        pq.Add("xyz");
        pq.Add("mon");
        pq.Add("abc");
        pq.Add("wx");
        Debug.Assert(pq.Peek() != "abc", "PQ peek should be abc now");
        pq.Get();
        Debug.Assert(pq.Peek() == "abc", "PQ peek should be abc now");
    }
}
public class PQ
{
    PriorityQueue<string, string> _pq;
    public PQ() => _pq = new PriorityQueue<string, string>(
            Comparer<string>.Create((x, y) =>
            {
                return x.Length == y.Length ? x.CompareTo(y) : x.Length - y.Length;
            })
        );
    public void Add(string s)
    {
        _pq.Enqueue(s, s);
    }

    public string Get()
    {
        return _pq.Dequeue();
    }

    public string Peek()
    {
        return _pq.Peek();
    }

    public int Count()
    {
        return _pq.Count;
    }

    public bool Any()
    {
        return _pq.Count != 0;
    }
}