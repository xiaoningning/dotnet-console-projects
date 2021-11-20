using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Linq;

/**
https://leetcode.com/discuss/interview-question/system-design/124558/Uber-or-Rate-Limiter
questions:
1. deny if they've made more than 100 requests in the past second 
or if they have exceeded a rate of 100 requests per second?
2. Two concepts of time here: the host and the requestor time stamp. 
Do we deny if more than 100 requests in the past second for the host ?

An optimized solution is to use a pooling technique. 
For each clientID, has a pool of 100 elements. Instead of a queue, use a circular queue. T
hat way you don't waste time creating and deleting objects. 
When you add a new timestamp, just move the head pointer back one and modify. 
When you want to check the timestamp, just check the one behind the head pointer.

*/
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
        if (curCnt >= _capacity)
        {
            while (curCnt > _capacity)
            {
                lock (_lock)
                {
                    _buckets[apiId].Dequeue();
                    curCnt = _buckets[apiId].Count;
                }
            }
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
        int curCnt = times.Count;
        if (curCnt >= _capacity)
        {
            // BUG: first is not neccessary 1st time stamp 
            // :=> queue should priority queue based on time
            // dequeue the older time stamp
            while (curCnt > _capacity)
            {
                times.TryDequeue(out _);
                curCnt = times.Count;
            }
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


public class BucketRateLimiterCircularQueue
{
    public class Bucket
    {
        //store the actual epoch time for this bucket
        //we only care about seconds granularity 
        public double secondsEpoch = -1;
        //a map of clientIds to the number of requests made for this second
        public Dictionary<string, int> requestCounts = new Dictionary<string, int>();
    }
    private Bucket[] seconds;

    private int _limitCnt;
    private int _windowInSec;
    private object _syncLock = new object();
    public BucketRateLimiterCircularQueue(int requestLimitCnt)
    {
        _limitCnt = requestLimitCnt;
        seconds = new Bucket[60];
    }

    public bool IsAllowed(string clientId)
    {
        // per second bucket if per minute, then totalminutes
        var nowSecond = (DateTime.UtcNow - DateTimeOffset.UnixEpoch).TotalSeconds;
        var nowBucket = seconds[(int)(nowSecond % 60)];
        lock (_syncLock)
        {
            if (nowBucket.secondsEpoch != nowSecond)
            {
                nowBucket.secondsEpoch = nowSecond;
                nowBucket.requestCounts.Clear();
            }
            if (!nowBucket.requestCounts.ContainsKey(clientId)) nowBucket.requestCounts[clientId] = 0;
            int nowCnt = nowBucket.requestCounts[clientId];
            if (nowCnt > _limitCnt) return false;
            else
            {
                nowBucket.requestCounts[clientId]++;
                return true;
            }
        }
    }

}
// Not a thread safe
public class BucketRateLimiterBasedonRate
{
    int _maxCapacity;
    int _refillTimeInSec;
    int _refillCntPerSec;
    int _curCnt;
    double _lastUpdateTime;
    public BucketRateLimiterBasedonRate(int capacity, int refillTime = 60, int refillCnt = 5)
    {
        _maxCapacity = capacity;
        _refillTimeInSec = refillTime;
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
