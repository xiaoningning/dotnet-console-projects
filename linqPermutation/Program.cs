class LinqPermutationApp
{
    static void Main()
    {
        var lst = new List<int>() { 1, 2, 3 };

        var iep = new IEnumerablePermutation();
        var r1 = iep.Permutate(lst);
        foreach (var r in r1) Console.WriteLine(string.Join("|", r));

        var str = "ABC".ToList();
        var r2 = iep.Permutate(str);
        foreach (var r in r2) Console.WriteLine(string.Join(",", r));

        var r3 = iep.PermutateStringArray(new string[] { "a", "b", "c" });
        foreach (var r in r3) Console.WriteLine(string.Join(",", r));

        var r4 = iep.PermutateIntArray(new int[] { 1, 2, 3 });
        foreach (var r in r4) Console.WriteLine(string.Join(",", r));

        Console.WriteLine("uenumerable permutation old framework");
        var permOld = new IEnumerablePermutationNetFramework();
        var ro4 = permOld.PermutateIntArray(new int[] { 1, 2, 3 });
        foreach (var r in ro4) Console.WriteLine(string.Join(",", r));
    }
}
public class IEnumerablePermutation
{
    public IEnumerable<T[]> Permutate<T>(IEnumerable<T> source)
    {
        return permutate(source, Enumerable.Empty<T>());

        IEnumerable<T[]> permutate(IEnumerable<T> reminder, IEnumerable<T> prefix) =>
            !reminder.Any() ?
            new[] { prefix.ToArray() } :
            reminder.SelectMany(
                (c, i) => permutate(
                    reminder.Take(i).Concat(reminder.Skip(i + 1)).ToArray(),
                    prefix.Append(c)
                )
            );
    }

    public IEnumerable<string[]> PermutateStringArray(string[] source)
    {
        return permutate(source, Enumerable.Empty<string>());

        IEnumerable<string[]> permutate(IEnumerable<string> reminder, IEnumerable<string> prefix) =>
            !reminder.Any() ?
            new[] { prefix.ToArray() } :
            reminder.SelectMany(
                (c, i) => permutate(
                    reminder.Take(i).Concat(reminder.Skip(i + 1)).ToArray(),
                    prefix.Append(c)
                )
            );
    }

    public IEnumerable<int[]> PermutateIntArray(int[] source)
    {
        return permutate(source, Enumerable.Empty<int>());

        IEnumerable<int[]> permutate(IEnumerable<int> reminder, IEnumerable<int> prefix) =>
            !reminder.Any() ?
            new[] { prefix.ToArray() } :
            reminder.SelectMany(
                (c, i) => permutate(
                    reminder.Take(i).Concat(reminder.Skip(i + 1)).ToArray(),
                    prefix.Append(c)
                )
            );
    }
}

public class IEnumerablePermutationNetFramework
{
    public IEnumerable<T[]> Permutate<T>(IEnumerable<T> source)
    {
        return Permutation(source, Enumerable.Empty<T>());
    }
    IEnumerable<T[]> Permutation<T>(IEnumerable<T> reminder, IEnumerable<T> prefix)
    {
        return !reminder.Any() ?
        new[] { prefix.ToArray() } :
        reminder.SelectMany(
            (c, i) => Permutation(
                reminder.Take(i).Concat(reminder.Skip(i + 1)).ToArray(),
                prefix.Append(c)
            )
        );
    }
    public IEnumerable<string[]> PermutateStringArray(string[] source)
    {
        return Permutation(source, Enumerable.Empty<string>());
    }

    public IEnumerable<int[]> PermutateIntArray(int[] source)
    {
        return Permutation(source, Enumerable.Empty<int>());
    }
}
