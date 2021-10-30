using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

class PriorityBlockingCollectionApp1
{
    static async Task<int> Main()
    {
        await Task.Delay(1);
        var pb = new PriorityBlockingCollection<string>(Comparer<string>.Create((x, y) => x.CompareTo(y)));
        Parallel.ForEach(Enumerable.Range(1, 20), (i) =>
        {
            pb.TryAdd(Guid.NewGuid().ToString().Substring(0, i));
        });
        while (pb.Count != 0)
        {
            pb.TryTake(out string x);
            Console.WriteLine($"{x}");
        }

        var pbc = new BlockingCollection<string>(new PriorityBlockingCollection<string>(Comparer<string>.Create((x, y) => x.CompareTo(y))), 10);
        Parallel.ForEach(Enumerable.Range(1, 20), (i) =>
        {
            pbc.TryAdd("pbc-" + Guid.NewGuid().ToString().Substring(0, i));
        });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(1 * 1000);
        try
        {
            foreach (var c in pbc.GetConsumingEnumerable(cts.Token)) Console.WriteLine($"pbc: {c}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"done : {ex}");
            Console.WriteLine($"pbc complete : {pbc.IsCompleted}");
            Console.WriteLine($"pbc size : {pbc.Count}");
        }
        return Environment.ExitCode;
    }
}