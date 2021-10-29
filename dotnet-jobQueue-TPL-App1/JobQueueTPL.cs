using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Dataflow;
using Polly;
using System.Collections.Concurrent;

public class JobQueueTPL
{
    readonly ILogger<JobQueueTPL> _logger;
    readonly IConfiguration _config;
    readonly bool _usePriorityQueue = false;
    readonly int _defaultCapacity = 5;
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

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create queue");
        _itemQueue = new BroadcastBlock<JobItem>(item => item, new DataflowBlockOptions()
        {
            MaxMessagesPerTask = -1
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create fedex queue");
        _fedexQueue = new TransformBlock<JobItem, JobItem>(async (i) => await ProcessItemAsync(i),
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create  ups queue");
        _upsQueue = new TransformBlock<JobItem, JobItem>(async (i) => await ProcessItemAsync(i),
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} create unknown queue");
        _unknownQueue = new TransformBlock<JobItem, JobItem>(async (i) => await ProcessItemAsync(i),
        new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = _defaultCapacity * 2
        });

        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} set up finish job");

        _finishQueue = new ActionBlock<JobItem>(async (i) => await FinishItemAsync(i), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });
    }
    public async Task SendJob(JobItem item, CancellationToken ct)
    {
        _logger.LogDebug($"{typeof(JobQueueTPL).FullName} link all blocks");

        _itemQueue.LinkTo(_fedexQueue, item => item.ItemType == JobType.Fedex);
        _itemQueue.LinkTo(_upsQueue, item => item.ItemType == JobType.UPS);
        _itemQueue.LinkTo(_unknownQueue, item => item.ItemType == JobType.Unknown);

        _fedexQueue.LinkTo(_finishQueue);
        _upsQueue.LinkTo(_finishQueue);
        _unknownQueue.LinkTo(_finishQueue);

        var executPolicy = Policy.Handle<Exception>().RetryAsync(_defaultRetryCnt);
        try
        {
            await executPolicy.ExecuteAsync(async () => await _itemQueue.SendAsync(item, ct));
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
        await _finishQueue.Completion;
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
        await Task.Run(() => _finishedItem.Add(item));
        _logger.LogInformation($"{typeof(JobQueueTPL).FullName}.{nameof(FinishItemAsync)}: {DateTime.Now} : {item.ToString()}");
    }
}