using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
public class JobQueueBlockingCollection : IJobQueue
{
    readonly ILogger<JobQueueBlockingCollection> _logger;
    IConfiguration _config;
    int _defaultCapacity = 2;
    int _defaultRetryCnt = 1;
    BlockingCollection<JobItem> _fedexQueue;
    BlockingCollection<JobItem> _unknownQueue;
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

        _fedexQueue = new BlockingCollection<JobItem>(_defaultCapacity);
        _unknownQueue = new BlockingCollection<JobItem>(_defaultCapacity * 2);
    }
    public async Task SendJob(IJobItem item, CancellationToken ct)
    {
        await Task.Delay(1);
    }
    public async Task FinishJob(CancellationToken ct)
    {
        await Task.Delay(1);
    }
}