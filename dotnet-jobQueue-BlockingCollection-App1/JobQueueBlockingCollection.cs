using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Polly;

public class JobQueueBlockingCollection : IJobQueue
{
    readonly ILogger<JobQueueBlockingCollection> _logger;
    IConfiguration _config;
    int _defaultCapacity = 2;
    int _defaultRetryCnt = 1;
    int _degreeOfParallelism = 1;
    int _defaultJobQueueWaitInMillisec = 2 * 1000;
    BlockingCollection<JobItem> _fedexQueue;
    BlockingCollection<JobItem> _upsQueue;
    BlockingCollection<JobItem> _unknownQueue;
    BlockingCollection<JobItem> _poisonQueue;
    BlockingCollection<JobItem> _finishQueue;
    ConcurrentBag<JobItem> _wastedItem;
    ConcurrentBag<JobItem> _finishedItem;
    Dictionary<JobHandlerType, BlockingCollection<JobItem>> _mapQueue;
    public JobQueueBlockingCollection(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory != null ?
                loggerFactory.CreateLogger<JobQueueBlockingCollection>()
                : NullLoggerFactory.Instance.CreateLogger<JobQueueBlockingCollection>();

        _logger.LogInformation($"{typeof(JobQueueBlockingCollection).FullName} logger OK!");

        InitQueues(config);
    }
    void InitQueues(IConfiguration config)
    {
        if (config != null)
        {
            _config = config;
            _defaultCapacity = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultCapacity").Value);
            _logger.LogDebug($"Iconfiguration defaultCapacity: {_defaultCapacity}");
            _defaultRetryCnt = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultRetryCnt").Value);
            _logger.LogDebug($"Iconfiguration defaultRetryCnt: {_defaultRetryCnt}");
        }

        // create a blockingcollection with priorityqueue with IProducerConsumerCollection
        IProducerConsumerCollection<JobItem> q = new ConcurrentQueue<JobItem>();
        _fedexQueue = new BlockingCollection<JobItem>(q, _defaultCapacity);
        _upsQueue = new BlockingCollection<JobItem>(q, _defaultCapacity);
        _unknownQueue = new BlockingCollection<JobItem>(q, _defaultCapacity * 2);
        _poisonQueue = new BlockingCollection<JobItem>();
        _finishQueue = new BlockingCollection<JobItem>();

        _wastedItem = new ConcurrentBag<JobItem>();
        _finishedItem = new ConcurrentBag<JobItem>();

        _mapQueue = new Dictionary<JobHandlerType, BlockingCollection<JobItem>>()
        {
            [JobHandlerType.FedexProcess] = _fedexQueue,
            [JobHandlerType.UPSProcess] = _upsQueue,
            [JobHandlerType.UnkownProcess] = _unknownQueue,
        };

    }
    public async Task SendJob(IJobItem job, CancellationToken ct)
    {
        var item = (JobItem)job;
        await Task.Delay(1);
        _logger.LogInformation($"receive a new job: {item.ToString()}");
        for (int i = 0; i < _degreeOfParallelism; i++)
        {
            await Task.Run(() => DispatchJob(item));
        }
        await Parallel.ForEachAsync(_mapQueue.Values, async (q, ct) =>
        {
            try
            {
                foreach (var i in q.GetConsumingEnumerable(ct)) await FinishItemAsync(i);
            }
            catch (OperationCanceledException ocEx)
            {
                _logger.LogDebug($"FinishItemAsync: {ocEx}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"FinishItemAsync unhandled exception: {ex}");
            }
            finally
            {
                _logger.LogDebug($"FinishItemAsync {q.GetType().FullName}: {job}");
            }
        });
    }
    public async Task FinishJob(CancellationToken ct)
    {
        try
        {
            // no more jobs coming in. finish the job
            foreach (var i in _finishQueue.GetConsumingEnumerable(ct))
            {
                if (ct.IsCancellationRequested) return;
                else await FinishItemAsync(i);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"wait for more jobs {ex}");
        }
        finally
        {
            _logger.LogInformation("No more jobs within set time");
        }
    }
    public void RegisterJobHandler(JobHandlerType t, Func<JobItem, JobItem> f)
    {
        throw new NotImplementedException();
    }
    public ConcurrentBag<JobItem> GetWastedItems() => _wastedItem;
    public ConcurrentBag<JobItem> GetFinishedItems() => _finishedItem;
    void DispatchJob(JobItem item)
    {
        var executPolicy = Policy.Handle<Exception>().Retry(_defaultRetryCnt);
        try
        {
            var jbCts = new CancellationTokenSource();
            jbCts.CancelAfter(_defaultJobQueueWaitInMillisec);
            var isSendJob = true;
            executPolicy.Execute(() =>
            {
                //// overflow the queue
                if (item.ItemType == JobType.Fedex) isSendJob = _fedexQueue.TryAdd(item, _defaultJobQueueWaitInMillisec, jbCts.Token);
                else if (item.ItemType == JobType.UPS) isSendJob = _upsQueue.TryAdd(item, _defaultJobQueueWaitInMillisec, jbCts.Token);
                else _unknownQueue.TryAdd(item, _defaultJobQueueWaitInMillisec, jbCts.Token);

                // fedex/ups queue is bounded.
                if (!isSendJob)
                {
                    _logger.LogError($"overflow to unknown queue {item.ToString()}");
                    _unknownQueue.TryAdd(item, _defaultJobQueueWaitInMillisec, jbCts.Token);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"sendAsync {ex}");
            _logger.LogError($"send to wasted bag {item.ToString()}");
            _wastedItem.Add(item);
            _logger.LogError($"poison queue handle backpressure {item.ToString()}");
            _poisonQueue.TryAdd(item, _defaultCapacity);
        }
    }
    async Task ProcessItemAsync(JobItem item)
    {
        try
        {
            _logger.LogInformation($"{typeof(JobQueueBlockingCollection).FullName}.{nameof(ProcessItemAsync)}: {DateTime.Now} : {item.ToString()}");
            await Task.Run(() => _finishQueue.TryAdd(item));
        }
        catch (Exception ex)
        {
            _logger.LogError($"sendAsync {ex}");
            _logger.LogError($"send to wasted bag {item.ToString()}");
            _wastedItem.Add(item);
            _logger.LogError($"poison queue handle backpressure {item.ToString()}");
            _poisonQueue.TryAdd(item, _defaultCapacity);
        }
    }
    async Task<JobItem> FinishItemAsync(JobItem item)
    {
        await Task.Run(() => _finishedItem.Add(item));
        _logger.LogInformation($"{typeof(JobQueueBlockingCollection).FullName}.{nameof(FinishItemAsync)}: {DateTime.Now} : {item.ToString()}");
        return item;
    }
}

public class TC<T>
{
    public T Input { get; set; }
    public TaskCompletionSource<T> TaskCompletionSource { get; set; }
}

public enum JobHandlerType { FedexProcess, UPSProcess, UnkownProcess, FinishProcess }