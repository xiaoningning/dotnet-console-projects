using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.File;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
class JobQueueTPLApp1
{
    static readonly string _logFileName = "log-jobQueueTPLApp1.log";
    static readonly string _appSettingFileName = "appsettings.json";
    static async Task<int> Main(string[] args)
    {
        var builder = AppSetup(args);

        var _logger = builder.GetRequiredService<ILogger<JobQueueTPLApp1>>();
        var appconfig = builder.GetRequiredService<IConfiguration>();
        _logger.LogInformation("Iconfiguration read in main: {pv}", appconfig?["JobQueue:Capacity"]);

        var jq = builder.GetServices<JobQueueTPL>().First();

        var s = Stopwatch.StartNew();

        Parallel.ForEach(Enumerable.Range(1, 20), async (i) =>
        {
            string jobType = i % 2 == 0 ? "Fedex" : "UPS";
            var ti = new JobItem(jobType);
            await Task.Delay(1);
            await jq.SendJob(ti, CancellationToken.None);
        });
        var cts = new CancellationTokenSource();
        cts.CancelAfter(5 * 1000);
        await jq.FinishJob(cts.Token);

        var wis = jq.GetWastedItems();
        var fis = jq.GetFinishedItems();
        _logger.LogInformation($"1st wasted items: {wis.Count}");
        _logger.LogInformation($"1st finished items: {fis.Count}");

        Parallel.ForEach(Enumerable.Range(1, 13), async (i) =>
        {
            string jobType = i % 2 == 0 ? "Fedex" : "UPS";
            var ti = new JobItem(jobType);
            await jq.SendJob(ti, CancellationToken.None);
        });
        cts = new CancellationTokenSource();
        cts.CancelAfter(10 * 1000);
        await jq.FinishJob(cts.Token);
        s.Stop();

        wis = jq.GetWastedItems();
        fis = jq.GetFinishedItems();
        _logger.LogInformation($"2nd wasted items: {wis.Count}");
        _logger.LogInformation($"2nd finished items: {fis.Count}");

        _logger.LogInformation($"done: {s.Elapsed}");

        Func<JobItem[], IEnumerable<JobItem>> f = (jobs) => { return jobs; };
        jq.RegisterJobHandler(JobHandlerType.UnkownProcess, f);

        return Environment.ExitCode;
    }
    static ServiceProvider AppSetup(string[] args)
    {
        /**
        IConfiguration configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_appSettingFileName, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
*/
        var appsettingStr = JsonSerializer.Serialize(new
        {
            JobQueue = new
            {
                DefaultCapacity = 4,
                DefaultRetryCnt = 1,
                DefaultJobQueueWaitInMillisec = 2 * 1000,
                DefaultBatchSize = 3,
                UsePriorityQueue = false
            }
        });

        IConfiguration configBuilder = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettingStr)))
                .Build();

        // setup serilog config
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File(_logFileName)
            .CreateLogger();

        // setup serilog logger factory
        // DI does not work with two constructors with Ilogger or IloggerFactory
        /**
                var serilogLoggerFactory = LoggerFactory.Create(builder =>
                        builder
                        .AddSerilog(logger: Log.Logger, dispose: true));

        */

        // Setting up dependency injection
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configBuilder);
        // serviceCollection.AddSingleton(serilogLoggerFactory);

        serviceCollection.AddLogging(config =>
        {
            config
            .ClearProviders()
            .AddSerilog(logger: serilogLogger, dispose: true);
        });

        // Add service 
        serviceCollection.AddTransient<JobQueueTPL>();

        return serviceCollection.BuildServiceProvider(validateScopes: true);
    }
}
