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
    readonly int _defaultCapacity = 1;
    readonly int _defaultRetryCnt = 3;
    BroadcastBlock<JobItem> _itemQueue;
    TransformBlock<JobItem, JobItem> _fedexQueue;
    TransformBlock<JobItem, JobItem> _upsQueue;
    TransformBlock<JobItem, JobItem> _unknownQueue;
    ActionBlock<JobItem> _finishQueue;
    ConcurrentBag<JobItem> _wastedItem;
    ConcurrentBag<JobItem> _finishedItem;
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
            _logger.LogDebug($"Iconfiguration: {_usePriorityQueue}");
        }

        _wastedItem = new ConcurrentBag<JobItem>();
        _finishedItem = new ConcurrentBag<JobItem>();
        Func<JobItem, Task<JobItem>> _pAsync = async (i) => await ProcessItemAsync(i);
        Func<JobItem, Task> _fAsync = async (i) => await FinishItemAsync(i);

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create queue");
        _itemQueue = new BroadcastBlock<JobItem>(item => item, new DataflowBlockOptions()
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

        _finishQueue = new ActionBlock<JobItem>(_fAsync, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} link all blocks");

        var linkOp = new DataflowLinkOptions() { PropagateCompletion = true };
        _itemQueue.LinkTo(_fedexQueue, linkOp, item => item.ItemType == JobType.Fedex);
        _itemQueue.LinkTo(_upsQueue, linkOp, item => item.ItemType == JobType.UPS);
        _itemQueue.LinkTo(_unknownQueue, linkOp, item => item.ItemType == JobType.Unknown);

        _fedexQueue.LinkTo(_finishQueue, linkOp);
        _upsQueue.LinkTo(_finishQueue, linkOp);
        _unknownQueue.LinkTo(_finishQueue, linkOp);
    }
    public async Task SendJob(IJobItem job, CancellationToken ct)
    {
        var item = (JobItem)job;

        var executPolicy = Policy.Handle<Exception>().RetryAsync(_defaultRetryCnt);
        try
        {
            await executPolicy.ExecuteAsync(async () =>
            {
                // overflow the queue
                // var isOverflow = await _itemQueue.SendAsync(item, ct);
                var isOverflow = _itemQueue.Post(item);
                /// BUG: buffer is unbounded
                /// fedex/ups queue is bounded.
                if (!isOverflow)
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
        }
    }
    public async Task DoJob(CancellationToken ct)
    {
        await Task.Delay(1);
    }
    public async Task FinishJob(CancellationToken ct)
    {
        // it stops taking more jobs
        // _itemQueue.Complete();
        // it nevers set queue complete since it waits for more jobs!!!
        // await _finishQueue.Completion;
        try
        {
            // no more jobs coming in. finish the job
            while (await _itemQueue.OutputAvailableAsync(ct)) if (ct.IsCancellationRequested) { return; }
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

    async Task FinishItemAsync(JobItem item)
    {
        // await Task.Run(() => _finishedItem.Add(item));
        _finishedItem.Add(item);
        await Task.Delay(1);
        _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(FinishItemAsync)}: {DateTime.Now} : {item.ToString()}");
    }
}