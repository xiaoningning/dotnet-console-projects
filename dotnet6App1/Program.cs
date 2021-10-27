using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace MyApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            using ILoggerFactory loggerFactory =
                LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    }));
            var logger = loggerFactory.CreateLogger<Program>();

            var d = new Dictionary<(int, int), int>()
            {
                [(1, 1)] = 0,
                [(2, 3)] = 1
            };
            Console.WriteLine(d[(2, 3)]);
            Console.WriteLine(d.ContainsKey((1, 0)));
            logger.LogInformation("logger here");

            var hs = new HashSet<(int, int)>() { (1, 2), (2, 3), (1, 2) };
            Console.WriteLine(hs.Contains((1, 1)));
            Console.WriteLine(hs.Contains((1, 2)));

            var lst1 = new List<(int, int)>() { (1, 2), (2, 3), (1, 2) };
            var lst2 = new List<(int, int)>() { (1, 2), (2, 3), (1, 2) };
            Console.WriteLine($"list<(int,int)> compare: {lst1.SequenceEqual(lst2)}");

            for (int i = 0; i < 10; i++)
            {
                Consume(CryptoConfig.CreateFromName("RSA"));
                Console.WriteLine($"{DateTime.Now}");
            }
            Console.WriteLine("All done on .NET6");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Consume<T>(in T _) { }
    }
}
