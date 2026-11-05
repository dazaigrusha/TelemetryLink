using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;

namespace TelemetryLink.Core.Pipeline;

/// <summary>
/// Повний прогін: кадри → пакети → канал → приймач → метрика Q.
///
/// Цей самий метод є функцією пристосованості для генетичного алгоритму:
/// він повертає готове Q. Іншої «спеціальної fitness» не потрібно — метрика
/// для порівняння команд і метрика для оптимізації мають збігатися, інакше
/// ГА оптимізує щось стороннє.
///
/// КРОК S6.
/// </summary>
public sealed class SessionRunner
{
    private readonly TeamConfig _config;
    private readonly PipelineOptions _options;

    public SessionRunner(TeamConfig config, PipelineOptions options)
    {
        _config = config;
        _options = options;
    }

    public SessionReport Run(TelemetrySession session, ScenarioConfig scenario, int channelSeed)
    {
        // TODO(S6). Порядок:
        //
        //   1. вивести ключі ОДИН раз на сеанс (не на кожен пакет!);
        //   2. Transmitter.BuildPackets;
        //   3. Scheduler.Order;
        //   4. для кожного пакета: channel.Send;
        //         • пакет, що не вмістився в бюджет, НЕ витрачає смугу
        //           і не входить у B_sent;
        //         • втрачений пакет смугу витрачає і в B_sent ВХОДИТЬ;
        //   5. доставлені пакети → Receiver.Accept, зібрати відновлені кадри;
        //   6. порахувати S, R, T і Q.
        //
        // Q = 0, якщо приймач аварійно завершився або не відновив жодного
        // критичного кадру (умова §4).
        //
        // Час t вимірюється секундоміром навколо ОБЧИСЛЕНЬ системи.
        // Час зайнятості каналу (channel.Statistics.ChannelTimeSeconds)
        // у компоненту T НЕ входить — це різні речі.
        throw new NotImplementedException("SessionRunner.Run");
    }
}
