using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Dataflow;
using Polly;
using System.Collections.Concurrent;

/**
learning notes:
Apache Beam is an open source, unified model for defining both batch and streaming data-parallel processing pipelines
Apache beam support python/java/go
Services: 
- apache spark runner
- google cloud dataflow runner
*/
public class JobQueueTPL : IJobQueue
{
    readonly ILogger<JobQueueTPL> _logger;
    IConfiguration _config;
    bool _usePriorityQueue = false;
    int _defaultCapacity = 2;
    int _defaultRetryCnt = 1;
    int _defaultJobQueueWaitInMillisec = (int)(1000 * 0.01);
    int _defaultBatchSize = 3;
    double _defaultJobPrioirty = 0.01;
    BufferBlock<JobItem> _poisonQueue;
    BufferBlock<JobItem> _fedexQueue;
    BufferBlock<JobItem> _upsQueue;
    BufferBlock<JobItem> _unknownQueue;
    BatchBlock<JobItem> _fedexBatcher;
    BatchBlock<JobItem> _upsBatcher;
    BatchBlock<JobItem> _unknownBatcher;
    TransformManyBlock<JobItem[], JobItem> _fedexProcessor;
    TransformManyBlock<JobItem[], JobItem> _upsProcessor;
    TransformManyBlock<JobItem[], JobItem> _unknownProcessor;
    TransformBlock<JobItem, JobItem> _finishQueue;
    ConcurrentBag<JobItem> _wastedItem;
    ConcurrentBag<JobItem> _finishedItem;
    BlockingCollection<JobItem> _priorityQueue;
    Dictionary<JobHandlerType, Func<JobItem[], IEnumerable<JobItem>>> _jobHandler;
    public JobQueueTPL(ILogger<JobQueueTPL> logger, IConfiguration config)
    {
        if (logger == null)
        {
            Console.WriteLine("Logger is null (Console), using NullLogger");
        }

        _logger = logger ?? NullLogger<JobQueueTPL>.Instance;
        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} Logger OK!");

        InitQueues(config);
    }

    /**
    /// DI does not work with two contructors somehow
    public JobQueueTPL(ILoggerFactory lf, IConfiguration config)
    {
        if (lf == null)
        {
            Console.WriteLine("ILoggerFactory is null (Console), using NullLoggerFactory");
        }

        _logger = lf?.CreateLogger<JobQueueTPL>() ?? NullLoggerFactory.Instance.CreateLogger<JobQueueTPL>();
        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} ILoggerFactory OK!");

        InitQueues(config);
    }
    */

    void InitConfig(IConfiguration config)
    {
        if (config != null)
        {
            _config = config;
            _usePriorityQueue = Convert.ToBoolean(_config.GetSection("JobQueue").GetSection("UsePriorityQueue").Value);
            _logger.LogDebug($"Iconfiguration UsePriorityQueue: {_usePriorityQueue}");
            _defaultCapacity = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultCapacity").Value);
            _logger.LogDebug($"Iconfiguration DefaultQueueCapacity: {_defaultCapacity}");
            _defaultJobQueueWaitInMillisec = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultJobQueueWaitInMillisec").Value);
            _logger.LogDebug($"Iconfiguration DefaultJobQueueWaitInMillisec: {_defaultJobQueueWaitInMillisec}");
            _defaultRetryCnt = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("RetryAsyncCnt").Value);
            _logger.LogDebug($"Iconfiguration RetryAsyncCnt: {_defaultRetryCnt}");
            _defaultBatchSize = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultBatchSize").Value);
            _logger.LogDebug($"Iconfiguration RetryAsyncCnt: {_defaultBatchSize}");
        }
    }
    void InitQueues(IConfiguration config)
    {
        InitConfig(config);
        _wastedItem = new ConcurrentBag<JobItem>();
        _finishedItem = new ConcurrentBag<JobItem>();
        _jobHandler = new Dictionary<JobHandlerType, Func<JobItem[], IEnumerable<JobItem>>>();
        _priorityQueue = new BlockingCollection<JobItem>();

        Func<JobItem[], Task<IEnumerable<JobItem>>> _pAsync = async (i) => await ProcessItemAsync(i);
        Func<JobItem, Task<JobItem>> _fAsync = async (i) => await FinishItemAsync(i);
        var dbo = new DataflowBlockOptions()
        {
            MaxMessagesPerTask = -1,
            BoundedCapacity = _defaultCapacity
        };

        _fedexQueue = new BufferBlock<JobItem>(dbo);
        _upsQueue = new BufferBlock<JobItem>(dbo);
        _unknownQueue = new BufferBlock<JobItem>(dbo);

        // !!! batchblock process only on the _defaultBatchSize of input items each time
        // if not enough of item from source block, it will wait!!!
        // https://stackoverflow.com/questions/32717337/data-propagation-in-tpl-dataflow-pipeline-with-batchblock-triggerbatch
        // https://stackoverflow.com/questions/35894878/batchblock-produces-batch-with-elements-sent-after-triggerbatch
        // _fedexBatcher.TriggerBatch() to handle it or create a conditional batch block
        var gbo = new GroupingDataflowBlockOptions()
        {
            Greedy = true
        };
        _fedexBatcher = new BatchBlock<JobItem>(_defaultBatchSize, gbo);
        _upsBatcher = new BatchBlock<JobItem>(_defaultBatchSize, gbo);
        _unknownBatcher = new BatchBlock<JobItem>(_defaultBatchSize, gbo);

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create out queue");
        _poisonQueue = new BufferBlock<JobItem>(new DataflowBlockOptions()
        {
            MaxMessagesPerTask = -1
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create fedex queue");
        _fedexProcessor = new TransformManyBlock<JobItem[], JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create  ups queue");
        _upsProcessor = new TransformManyBlock<JobItem[], JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create unknown queue");
        _unknownProcessor = new TransformManyBlock<JobItem[], JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity * 2
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} set up finish job");
        _finishQueue = new TransformBlock<JobItem, JobItem>(_fAsync, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} link all blocks");
        var linkOp = new DataflowLinkOptions() { PropagateCompletion = true };

        _fedexQueue.LinkTo(_fedexBatcher, linkOp);
        _upsQueue.LinkTo(_upsBatcher, linkOp);
        _unknownQueue.LinkTo(_unknownBatcher, linkOp);

        // batch block to handle priority or remove poison items
        _fedexBatcher.LinkTo(_fedexProcessor, linkOp);
        _upsBatcher.LinkTo(_upsProcessor, linkOp);
        _unknownBatcher.LinkTo(_unknownProcessor, linkOp);

        _fedexProcessor.LinkTo(_finishQueue, linkOp);
        _upsProcessor.LinkTo(_finishQueue, linkOp);
        _unknownProcessor.LinkTo(_finishQueue, linkOp);
    }
    public void RegisterJobHandler(JobHandlerType t, Func<JobItem[], IEnumerable<JobItem>> f)
    {
        _jobHandler[t] = f;
        _logger.LogInformation($"register a new job handler: {t}");

        var edbOp = new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        };

        if (t == JobHandlerType.FedexProcess)
        {
            _fedexProcessor = new TransformManyBlock<JobItem[], JobItem>(f, edbOp);
        }
        if (t == JobHandlerType.UPSProcess)
        {
            _upsProcessor = new TransformManyBlock<JobItem[], JobItem>(f, edbOp);
        }
        if (t == JobHandlerType.UnkownProcess)
        {
            _unknownProcessor = new TransformManyBlock<JobItem[], JobItem>(f, edbOp);
        }
    }
    public async Task SendJob(IJobItem job, CancellationToken ct)
    {
        var item = (JobItem)job;
        /**
        /// use priority queue here, but queue needs to have buffer to prioritize items
        /// use batch block as buffer region to pre-processed item
        if (_usePriorityQueue)
        {
            _priorityQueue.TryAdd(item);

            foreach (var i in _priorityQueue.GetConsumingEnumerable(ct))
            {
                await DispatchJob(i, ct);
            }
        }
        else await DispatchJob(item, ct);
        */
        await DispatchJob(item, ct);
    }
    async Task DispatchJob(JobItem item, CancellationToken ct)
    {

        _logger.LogInformation($"Dispactch job {item}");
        var executPolicy = Policy.Handle<Exception>().RetryAsync(_defaultRetryCnt);

        using (var jbCts = new CancellationTokenSource())
        {
            try
            {
                jbCts.CancelAfter(_defaultJobQueueWaitInMillisec / 1000);
                var isSendJob = true;
                var isToUnkown = true;
                await executPolicy.ExecuteAsync(async () =>
                {
                    //// overflow the queue

                    // if sendasync, overflow is controlled by CancellationToken due to bounded capacity
                    // it throws exception
                    // isSendJob = await _itemQueue.SendAsync(item, ct);

                    // if post, overflow is controlled by bounded capacity
                    // return false
                    // isSendJob = _itemQueue.Post(item);

                    if (item.ItemType == JobType.Fedex) isSendJob = _fedexQueue.Post(item);
                    else if (item.ItemType == JobType.UPS) isSendJob = _upsQueue.Post(item);
                    else isToUnkown = _unknownQueue.Post(item);

                    // fedex/ups queue is bounded.
                    if (!isSendJob)
                    {
                        _logger.LogError($"overflow to unknown queue {item.ToString()}");
                        isToUnkown = _unknownQueue.Post(item); ;
                    }
                    if (!isToUnkown)
                    {
                        _logger.LogError($"poison queue handle backpressure {item.ToString()}");
                        await Task.Run(() => _wastedItem.Add(item));
                        await _poisonQueue.SendAsync(item, jbCts.Token);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"sendAsync {ex}");
                _logger.LogError($"send to wasted bag {item.ToString()}");
                await Task.Run(() => _wastedItem.Add(item));
            }
        }
    }
    public async Task FinishJob(CancellationToken ct)
    {
        // it stops taking more jobs and close the queue
        // _itemQueue.Complete();
        // it nevers set queue complete since it waits for more jobs!!!
        // await _finishQueue.Completion;
        try
        {
            // hanlde the leftover item in source queue for batcher
            if (_fedexQueue.Count < _defaultBatchSize) _fedexBatcher.TriggerBatch();
            if (_upsQueue.Count < _defaultBatchSize) _upsBatcher.TriggerBatch();
            if (_unknownQueue.Count < _defaultBatchSize) _unknownBatcher.TriggerBatch();

            // no more jobs coming in. finish the job
            while (await _finishQueue.OutputAvailableAsync(ct)) if (ct.IsCancellationRequested) { return; }
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

    public ConcurrentBag<JobItem> GetWastedItems() => _wastedItem;
    public ConcurrentBag<JobItem> GetFinishedItems() => _finishedItem;

    async Task<IEnumerable<JobItem>> ProcessItemAsync(JobItem[] items)
    {
        await Task.Delay(1);
        foreach (var i in items) _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(ProcessItemAsync)}: {DateTime.Now} : {i.ToString()}");
        return PrioritizeItem(items);
    }
    IEnumerable<JobItem> PrioritizeItem(JobItem[] items)
    {
        var t = new ConcurrentBag<JobItem>();
        Parallel.ForEach(items, (i) =>
        {
            _logger.LogInformation($"Prioritize Item: {i.ToString()}");
            if (i.GetJobPriority() > _defaultJobPrioirty) t.Add(i);
        });
        return t.OrderBy(x => x.GetJobPriority());
    }

    async Task<JobItem> FinishItemAsync(JobItem item)
    {
        await Task.Run(() => _finishedItem.Add(item));
        _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(FinishItemAsync)}: {DateTime.Now} : {item.ToString()}");
        return item;
    }
}

public enum JobHandlerType { FedexProcess, UPSProcess, UnkownProcess, FinishProcess }