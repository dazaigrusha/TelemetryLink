namespace TelemetryLink.Core.Compression;

/// <summary>Побудова довжин кодів через чергу з пріоритетами. S3.</summary>
public static class HuffmanTreeBuilder
{
    private sealed record Node(int Symbol, Node? Left = null, Node? Right = null);

    public static byte[] BuildLengths(ReadOnlySpan<int> frequencies)
    {
        if (frequencies.Length != 256)
            throw new ArgumentException("Очікується рівно 256 частот.", nameof(frequencies));
        var weights = new long[256];
        var symbols = new List<int>();
        for (int symbol = 0; symbol < 256; symbol++)
        {
            if (frequencies[symbol] < 0)
                throw new ArgumentException("Частоти не можуть бути від'ємними.", nameof(frequencies));
            weights[symbol] = frequencies[symbol];
            if (weights[symbol] > 0) symbols.Add(symbol);
        }
        if (symbols.Count == 0) return new byte[256];
        if (symbols.Count == 1)
        {
            var lengths = new byte[256];
            lengths[symbols[0]] = 1;
            return lengths;
        }

        for (int attempt = 0; attempt < 64; attempt++)
        {
            var lengths = BuildTree(symbols, weights);
            if (lengths.Max() <= Format.CanonicalCodes.MaxCodeLength) return lengths;
            CodeLengthLimiter.RaiseFloor(symbols, weights, Format.CanonicalCodes.MaxCodeLength);
        }
        throw new InvalidOperationException("Не вдалося обмежити довжини кодів за 64 спроби.");
    }

    private static byte[] BuildTree(IReadOnlyList<int> symbols, long[] weights)
    {
        // Номер вставки однозначно розв'язує нічиї, включно з внутрішніми вузлами.
        var queue = new PriorityQueue<Node, (long Weight, int Order)>();
        int order = 0;
        foreach (int symbol in symbols)
            queue.Enqueue(new Node(symbol), (weights[symbol], order++));
        while (queue.Count > 1)
        {
            queue.TryDequeue(out var left, out var leftPriority);
            queue.TryDequeue(out var right, out var rightPriority);
            queue.Enqueue(new Node(-1, left, right),
                (checked(leftPriority.Weight + rightPriority.Weight), order++));
        }
        var result = new byte[256];
        var stack = new Stack<(Node Node, int Depth)>();
        stack.Push((queue.Dequeue(), 0));
        while (stack.TryPop(out var item))
        {
            if (item.Node.Symbol >= 0) result[item.Node.Symbol] = checked((byte)item.Depth);
            else
            {
                stack.Push((item.Node.Right!, item.Depth + 1));
                stack.Push((item.Node.Left!, item.Depth + 1));
            }
        }
        return result;
    }

    public static int[] CountFrequencies(ReadOnlySpan<byte> data)
    {
        var frequencies = new int[256];
        foreach (byte symbol in data) frequencies[symbol]++;
        return frequencies;
    }
}
