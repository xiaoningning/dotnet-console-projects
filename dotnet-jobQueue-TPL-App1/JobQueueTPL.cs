using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Dataflow;
using Polly;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

public class JobQueueTPL : IJobQueue
{
    readonly ILogger<JobQueueTPL> _logger;
    readonly IConfiguration _config;
    readonly bool _usePriorityQueue = false;
    readonly int _defaultCapacity = 2;
    readonly int _defaultRetryCnt = 1;
    readonly int _defaultJobQueueWaitInMillisec = (int)(1000 * 0.01);
    BufferBlock<JobItem> _poisonQueue;
    TransformBlock<JobItem, JobItem> _fedexQueue;
    TransformBlock<JobItem, JobItem> _upsQueue;
    TransformBlock<JobItem, JobItem> _unknownQueue;
    TransformBlock<JobItem, JobItem> _finishQueue;
    ConcurrentBag<JobItem> _wastedItem;
    ConcurrentBag<JobItem> _finishedItem;
    Dictionary<JobHandlerType, Func<JobItem, JobItem>> _jobHandler;
    public JobQueueTPL(ILogger<JobQueueTPL> logger, IConfiguration config)
    {
        if (logger == null)
        {
            Console.WriteLine("Logger is null (Console), using NullLogger");
        }

        _logger = logger ?? NullLogger<JobQueueTPL>.Instance;
        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} Logger OK!");

        if (config != null)
        {
            _config = config;
            _usePriorityQueue = Convert.ToBoolean(_config.GetSection("JobQueue").GetSection("UsePriorityQueue").Value);
            _logger.LogDebug($"Iconfiguration UsePriorityQueue: {_usePriorityQueue}");
            _defaultCapacity = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("DefaultQueueCapacity").Value);
            _logger.LogDebug($"Iconfiguration DefaultQueueCapacity: {_defaultCapacity}");
            _defaultJobQueueWaitInMillisec = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("SendAsyncTimeOutInMillisec").Value);
            _logger.LogDebug($"Iconfiguration SendAsyncTimeOutInMillisec: {_defaultJobQueueWaitInMillisec}");
            _defaultRetryCnt = Convert.ToInt32(_config.GetSection("JobQueue").GetSection("RetryAsyncCnt").Value);
            _logger.LogDebug($"Iconfiguration RetryAsyncCnt: {_defaultRetryCnt}");
        }

        _wastedItem = new ConcurrentBag<JobItem>();
        _finishedItem = new ConcurrentBag<JobItem>();
        _jobHandler = new Dictionary<JobHandlerType, Func<JobItem, JobItem>>();
        Func<JobItem, Task<JobItem>> _pAsync = async (i) => await ProcessItemAsync(i);
        Func<JobItem, Task<JobItem>> _fAsync = async (i) => await FinishItemAsync(i);

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create out queue");
        _poisonQueue = new BufferBlock<JobItem>(new DataflowBlockOptions()
        {
            MaxMessagesPerTask = -1
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create fedex queue");
        _fedexQueue = new TransformBlock<JobItem, JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create  ups queue");
        _upsQueue = new TransformBlock<JobItem, JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create unknown queue");
        _unknownQueue = new TransformBlock<JobItem, JobItem>(_pAsync,
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity * 2
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} set up finish job");
        _finishQueue = new TransformBlock<JobItem, JobItem>(_fAsync, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} link all blocks");
        var linkOp = new DataflowLinkOptions() { PropagateCompletion = true };
        _fedexQueue.LinkTo(_finishQueue, linkOp);
        _upsQueue.LinkTo(_finishQueue, linkOp);
        _unknownQueue.LinkTo(_finishQueue, linkOp);
    }

    public void RegisterJobHandler(JobHandlerType t, Func<JobItem, JobItem> f)
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
            _fedexQueue = new TransformBlock<JobItem, JobItem>(f, edbOp);
        }
        if (t == JobHandlerType.UPSProcess)
        {
            _upsQueue = new TransformBlock<JobItem, JobItem>(f, edbOp);
        }
        if (t == JobHandlerType.UnkownProcess)
        {
            _unknownQueue = new TransformBlock<JobItem, JobItem>(f, edbOp);
        }
        if (t == JobHandlerType.FinishProcess)
        {
            _finishQueue = new TransformBlock<JobItem, JobItem>(f, edbOp);
        }
    }
    public async Task SendJob(IJobItem job, CancellationToken ct)
    {
        var item = (JobItem)job;

        var executPolicy = Policy.Handle<Exception>().RetryAsync(_defaultRetryCnt);
        try
        {
            var jbCts = new CancellationTokenSource();
            jbCts.CancelAfter(_defaultJobQueueWaitInMillisec);
            var isSendJob = true;
            await executPolicy.ExecuteAsync(async () =>
            {
                //// overflow the queue

                // if sendasync, overflow is controlled by CancellationToken due to bounded capacity
                // isSendJob = await _itemQueue.SendAsync(item, ct);

                // if post, overflow is controlled by bounded capacity
                // isSendJob = _itemQueue.Post(item);

                if (item.ItemType == JobType.Fedex) isSendJob = await _fedexQueue.SendAsync(item, jbCts.Token);
                else if (item.ItemType == JobType.UPS) isSendJob = await _upsQueue.SendAsync(item, jbCts.Token);
                else await _unknownQueue.SendAsync(item, jbCts.Token);

                // fedex/ups queue is bounded.
                if (!isSendJob)
                {
                    _logger.LogError($"overflow to unknown queue {item.ToString()}");
                    await _unknownQueue.SendAsync(item, ct);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"sendAsync {ex}");
            _logger.LogError($"send to wasted bag {item.ToString()}");
            await Task.Run(() => _wastedItem.Add(item));
            _logger.LogError($"poison queue handle backpressure {item.ToString()}");
            await _poisonQueue.SendAsync(item);
        }
    }
    public async Task DoJob(CancellationToken ct)
    {
        await Task.Delay(1);
    }
    public async Task FinishJob(CancellationToken ct)
    {
        // it stops taking more jobs and close the queue
        // _itemQueue.Complete();
        // it nevers set queue complete since it waits for more jobs!!!
        // await _finishQueue.Completion;
        try
        {
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

    async Task<JobItem> ProcessItemAsync(JobItem item)
    {
        await Task.Delay(1);
        _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(ProcessItemAsync)}: {DateTime.Now} : {item.ToString()}");
        return item;
    }

    async Task<JobItem> FinishItemAsync(JobItem item)
    {
        await Task.Run(() => _finishedItem.Add(item));
        _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(FinishItemAsync)}: {DateTime.Now} : {item.ToString()}");
        return item;
    }
}

public enum JobHandlerType { FedexProcess, UPSProcess, UnkownProcess, FinishProcess }