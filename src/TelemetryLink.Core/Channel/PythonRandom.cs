namespace TelemetryLink.Core.Channel;

/// <summary>
/// Генератор випадкових чисел, бітово сумісний із <c>random.Random</c> у Python
/// (Mersenne Twister MT19937 + схема ініціалізації CPython).
///
/// НАВІЩО ЦЕ ТУТ. Еталонна модель каналу видається викладачем як
/// <c>channel.py</c>. Щоб реалізація на C# давала ТОЙ САМИЙ розклад втрат при
/// тому самому seed, потрібен той самий генератор: <c>System.Random</c> дає
/// іншу послідовність, і значення R та Q стали б непорівнянними між командами.
///
/// Цей файл, як і сам канал, видається готовим і не є предметом реалізації.
/// </summary>
public sealed class PythonRandom
{
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA = 0x9908b0dfU;
    private const uint UpperMask = 0x80000000U;
    private const uint LowerMask = 0x7fffffffU;

    private readonly uint[] _state = new uint[N];
    private int _index = N + 1;

    public PythonRandom(long seed) => InitByArray(ToKeyArray(Math.Abs(seed)));

    /// <summary>CPython перетворює цілий seed на масив 32-бітових слів.</summary>
    private static uint[] ToKeyArray(long seed)
    {
        if (seed == 0) return [0];

        var words = new List<uint>();
        ulong value = (ulong)seed;
        while (value > 0)
        {
            words.Add((uint)(value & 0xffffffffUL));
            value >>= 32;
        }
        return words.ToArray();
    }

    private void InitGenrand(uint s)
    {
        _state[0] = s;
        for (int i = 1; i < N; i++)
            _state[i] = unchecked(1812433253U * (_state[i - 1] ^ (_state[i - 1] >> 30)) + (uint)i);
        _index = N;
    }

    private void InitByArray(uint[] key)
    {
        InitGenrand(19650218U);

        int i = 1, j = 0;
        int k = Math.Max(N, key.Length);

        for (; k > 0; k--)
        {
            _state[i] = unchecked(
                (_state[i] ^ ((_state[i - 1] ^ (_state[i - 1] >> 30)) * 1664525U)) + key[j] + (uint)j);
            i++; j++;
            if (i >= N) { _state[0] = _state[N - 1]; i = 1; }
            if (j >= key.Length) j = 0;
        }

        for (k = N - 1; k > 0; k--)
        {
            _state[i] = unchecked(
                (_state[i] ^ ((_state[i - 1] ^ (_state[i - 1] >> 30)) * 1566083941U)) - (uint)i);
            i++;
            if (i >= N) { _state[0] = _state[N - 1]; i = 1; }
        }

        _state[0] = 0x80000000U;
        _index = N;
    }

    private uint NextUInt32()
    {
        if (_index >= N)
        {
            for (int i = 0; i < N; i++)
            {
                uint y = (_state[i] & UpperMask) | (_state[(i + 1) % N] & LowerMask);
                _state[i] = _state[(i + M) % N] ^ (y >> 1) ^ ((y & 1) != 0 ? MatrixA : 0U);
            }
            _index = 0;
        }

        uint value = _state[_index++];
        value ^= value >> 11;
        value ^= (value << 7) & 0x9d2c5680U;
        value ^= (value << 15) & 0xefc60000U;
        value ^= value >> 18;
        return value;
    }

    /// <summary>Дійсне число з [0, 1) — точна копія random.random(): 53 біти з двох слів.</summary>
    public double NextDouble()
    {
        uint a = NextUInt32() >> 5;   // 27 біт
        uint b = NextUInt32() >> 6;   // 26 біт
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Копія random.uniform(a, b).</summary>
    public double NextUniform(double low, double high) => low + (high - low) * NextDouble();
}
