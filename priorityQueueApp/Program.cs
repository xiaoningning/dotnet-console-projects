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

        var jobQueue = new PriorityQueue<Job, Job>(Comparer<Job>.Create((x, y) =>
        {
            var xLife = (x.Life + x.CreateTime) * x.SlowRate;
            var yLife = (y.Life + y.CreateTime) * y.SlowRate;
            return xLife.CompareTo(yLife);
        }));

        var j1 = new Job()
        {
            Id = Guid.NewGuid(),
            Name = "j1",
            SlowRate = 1.0,
            Life = 100,
            CreateTime = (DateTime.Now - DateTimeOffset.UnixEpoch).TotalSeconds
        };
        var j2 = new Job()
        {
            Id = Guid.NewGuid(),
            Name = "j2",
            SlowRate = 2.0,
            Life = 100,
            CreateTime = (DateTime.Now - DateTimeOffset.UnixEpoch).TotalSeconds
        };
        var j3 = new Job()
        {
            Id = Guid.NewGuid(),
            SlowRate = 3.0,
            Name = "j3",
            Life = 100,
            CreateTime = (DateTime.Now - DateTimeOffset.UnixEpoch).TotalSeconds
        };
        jobQueue.Enqueue(j1, j1);
        jobQueue.Enqueue(j2, j2);
        jobQueue.Enqueue(j3, j3);
        while (jobQueue.Count != 0)
        {
            var t = jobQueue.Dequeue();
            Console.WriteLine($"{t.Id} - {t.Name} - {t.SlowRate} - {(t.Life + t.CreateTime) * t.SlowRate}");
        }
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

public class Job
{
    public Guid Id { get; set; }
    public double SlowRate { get; set; }
    public int Life { get; set; }
    public double CreateTime { get; set; }
    public string Name { get; set; }
}