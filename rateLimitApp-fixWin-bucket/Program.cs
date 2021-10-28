using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Linq;

class RateLimitApp
{
    static void Main()
    {
        Console.WriteLine("rate limiter");
        var epoch = DateTimeOffset.UnixEpoch;
        var t1 = DateTime.Now;
        var t2 = t1.AddMinutes(1);
        Console.WriteLine(Convert.ToInt64((t1 - epoch).TotalSeconds));
        Console.WriteLine(Convert.ToInt64((t2 - epoch).TotalSeconds));
        var ut = new RateLimitUT();
        ut.RateLimitTest1();
        ut.RateLimitTest2();
    }
}
public class RateLimitUT
{
    public void RateLimitTest1()
    {
        var rl1 = new RateLimiterConcurrent(2, 10);
        var res = new List<bool>();
        res.Add(rl1.CallApi("u1", 1));
        res.Add(rl1.CallApi("u1", 1));
        res.Add(rl1.CallApi("u1", 4));
        res.Add(rl1.CallApi("u1", 8));
        res.Add(rl1.CallApi("u1", 12));
        res.ForEach(x => Console.WriteLine($"{x}"));
        var exp = new List<bool>() { true, true, false, false, true };
        Debug.Assert(res.SequenceEqual(exp), "rate limit concurrent");
        Console.WriteLine("rate limiter done");
    }
    public void RateLimitTest2()
    {
        Console.WriteLine("rate limiter parall");
        var rl = new RateLimiter(2, 5);
        var res = new ConcurrentBag<bool>();
        Parallel.ForEach(
            new int[] { 1, 4, 2, 8, 12 },
            (x) =>
            {
                res.Add(rl.CallApi("u1", x));
                Console.WriteLine(res.Last());
            }
        );
        var exp = new List<bool>() { true, true, true, true, true };
        // it should be faile due to parallel without concurrent queue
        Debug.Assert(!res.SequenceEqual(exp), "rate limit 2");
        Console.WriteLine("rate limiter done");
    }
}
// API rate limi:= # of calls within x seconds
public class RateLimiter
{
    int _capacity;
    int _window;
    Dictionary<string, Queue<long>> _buckets;
    object _lock = new object();
    public RateLimiter(int cnt, int windowInSec)
    {
        _capacity = cnt;
        _window = windowInSec;
        _buckets = new Dictionary<string, Queue<long>>();
    }
    public bool CallApi(string apiId, long time)
    {
        if (!_buckets.ContainsKey(apiId)) lock (_lock) _buckets[apiId] = new Queue<long>();
        int curCnt = 0;
        lock (_lock) curCnt = _buckets[apiId].Count;
        if (curCnt == _capacity)
        {
            bool ret = true;
            long t1 = Int64.MinValue;
            lock (_lock) t1 = _buckets[apiId].Peek();
            if (time - t1 > _window)
            {
                lock (_lock)
                {
                    _buckets[apiId].Dequeue();
                    _buckets[apiId].Enqueue(time);
                }
            }
            else ret = false;
            return ret;
        }
        else
        {
            lock (_lock) _buckets[apiId].Enqueue(time);
            return true;
        }
    }
}

public class RateLimiterConcurrent
{
    int _capacity;
    int _window;
    SemaphoreSlim _semaphoreSlim;
    ConcurrentDictionary<string, ConcurrentQueue<long>> buckets;
    public RateLimiterConcurrent(int cnt, int windowInSec)
    {
        // semaphore controls # of threads to access the resource
        _semaphoreSlim = new SemaphoreSlim(0, 5);
        _capacity = cnt;
        _window = windowInSec;
        buckets = new ConcurrentDictionary<string, ConcurrentQueue<long>>();
    }
    public bool CallApi(string userId, long time)
    {
        if (!buckets.ContainsKey(userId)) buckets.TryAdd(userId, new ConcurrentQueue<long>());
        ConcurrentQueue<long> times;
        buckets.TryGetValue(userId, out times);
        if (times.Count == _capacity)
        {
            // BUG: first is not neccessary 1st time stamp
            times.TryPeek(out long t1);
            if (time - t1 > _window)
            {
                times.TryDequeue(out _);
                times.Enqueue(time);
                return true;
            }
            else return false;
        }
        else
        {
            // concurrent enqueue could be out of order!!!
            times.Enqueue(time);
            return true;
        }
    }
}

// Not a thread safe
public class BucketRateLimiter
{
    int _maxCapacity;
    int _refillTimeInSec;
    int _refillCntPerSec;
    int _curCnt;
    double _lastUpdateTime;
    public BucketRateLimiter(int capacity, int refillTime = 10, int refillCnt = 5)
    {
        _maxCapacity = capacity;
        _refillCntPerSec = refillTime;
        _refillCntPerSec = refillCnt;
    }
    void Reset()
    {
        _curCnt = _maxCapacity;
        _lastUpdateTime = (DateTime.Now - DateTimeOffset.UnixEpoch).TotalSeconds;
    }
    int RefillCnt()
    {
        return (int)((DateTime.Now - DateTime.UnixEpoch).TotalSeconds - _lastUpdateTime) / _refillTimeInSec;
    }
    public int GetCurrentCapacity()
    {
        return Math.Min(_maxCapacity, _curCnt + RefillCnt() * _refillCntPerSec);
    }
    public bool GetRateTokens(int requestedCntTokens)
    {
        int refillCnt = RefillCnt();
        _curCnt += refillCnt * _refillCntPerSec;
        _lastUpdateTime += refillCnt * _refillTimeInSec;

        if (_curCnt > _maxCapacity) Reset();
        if (requestedCntTokens > _curCnt) return false;
        else
        {
            _curCnt -= requestedCntTokens;
            return true;
        }
    }
}
