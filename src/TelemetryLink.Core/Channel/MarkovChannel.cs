using TelemetryLink.Core.Configuration;

namespace TelemetryLink.Core.Channel;

/// <summary>
/// Двостанний ланцюг Маркова GOOD/BAD із бюджетом часу передавання.
///
/// ЦЕЙ КЛАС ВИДАЄТЬСЯ ГОТОВИМ І НЕ Є ПРЕДМЕТОМ РЕАЛІЗАЦІЇ.
/// Це точний переклад <c>channel.py</c> з комплекту викладача, включно з
/// генератором випадкових чисел: при тому самому seed послідовність втрат
/// збігається байт у байт. Змінювати файл не можна — результати команд
/// перестануть бути порівнянними.
///
/// Модель каналу не належить до тем дисципліни; її самостійна реалізація
/// нічого не додає до вивчення алгоритмів, зате руйнує порівнянність.
/// </summary>
public sealed class MarkovChannel : IChannel
{
    private readonly ScenarioConfig _scenario;
    private readonly PythonRandom _random;
    private readonly double _badStateProbability;

    private int _burstRemaining;
    private double _clockSeconds;
    private int _sent;
    private int _lost;
    private int _overBudget;

    public MarkovChannel(ScenarioConfig scenario, int seed)
    {
        _scenario = scenario;
        _random = new PythonRandom(seed);
        _badStateProbability = scenario.BadStateProbability;
    }

    public ChannelStatistics Statistics => new(
        _sent, _lost, _overBudget, _clockSeconds, _scenario.TransmissionBudgetSeconds);

    public TransmissionResult Send(ReadOnlySpan<byte> packet)
    {
        double required = packet.Length * 8.0 / _scenario.BandwidthBitsPerSecond;

        // Бюджет вичерпано: пакет не передається зовсім.
        if (_clockSeconds + required > _scenario.TransmissionBudgetSeconds)
        {
            _overBudget++;
            return new TransmissionResult(false, null);
        }

        // Смуга витрачається незалежно від того, чи пакет дійде.
        _clockSeconds += required;
        _sent++;

        bool lost;
        if (_burstRemaining > 0)
        {
            _burstRemaining--;
            lost = true;
        }
        else if (_scenario.LossProbability > 0 && _random.NextDouble() < _badStateProbability)
        {
            // Довжина пачки — геометричний розподіл із середнім B.
            int length = 1;
            if (_scenario.BurstMeanLength > 1.0)
                while (_random.NextDouble() > 1.0 / _scenario.BurstMeanLength) length++;

            _burstRemaining = length - 1;
            lost = true;
        }
        else
        {
            lost = false;
        }

        if (lost)
        {
            _lost++;
            return new TransmissionResult(false, null);
        }

        return new TransmissionResult(true, _clockSeconds + DelayMilliseconds() / 1000.0);
    }

    private double DelayMilliseconds() => _scenario.DelayKind == DelayKind.Constant
        ? _scenario.DelayConstantMs
        : _random.NextUniform(_scenario.DelayLowMs, _scenario.DelayHighMs);
}

/// <summary>
/// Ідеальний канал без втрат, затримок і бюджету. Потрібен у тестах тракту,
/// де перевіряється не поведінка каналу, а побайтове відновлення кадрів.
/// </summary>
public sealed class PerfectChannel : IChannel
{
    private int _sent;

    public ChannelStatistics Statistics => new(_sent, 0, 0, 0.0, double.PositiveInfinity);

    public TransmissionResult Send(ReadOnlySpan<byte> packet)
    {
        _sent++;
        return new TransmissionResult(true, 0.0);
    }
}
