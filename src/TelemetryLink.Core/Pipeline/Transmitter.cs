using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Pipeline;

/// <summary>
/// Передавач: перетворює сеанс телеметрії на послідовність готових пакетів.
///
/// Порядок дій усередині одного пакета критично важливий — це зміст
/// розділу 3.5 специфікації:
///
///   1. береться ВІКНО КАНДИДАТІВ (не весь сеанс!), розмір — Tuning.WindowFrames;
///   2. для ВСЬОГО вікна будується ОДНА таблиця Гаффмана — ДО вибору кадрів;
///   3. вага кадру w_i — довжина його 12 байтів у бітах за цією таблицею;
///   4. місткість W = 8 × (max_payload_bytes − 14 − 2 × table_nsym);
///   5. рюкзак обирає кадри;
///   6. обрані кодуються ТІЄЮ САМОЮ таблицею — тоді фактичний розмір пакета
///      збігається з тим, що врахувало ДП;
///   7. кадри, що не потрапили, переходять у вікно наступного пакета.
///
/// Якщо будувати таблицю ПІСЛЯ вибору, вага залежатиме від вибору, а вибір
/// від ваги — задача про рюкзак стане невизначеною.
///
/// КРОК S4.
/// </summary>
public sealed class Transmitter
{
    private readonly TeamConfig _config;
    private readonly PipelineOptions _options;
    private readonly SessionKeys _keys;
    private readonly byte[] _salt;
    private readonly byte[] _noncePrefix;

    public Transmitter(TeamConfig config, PipelineOptions options, SessionKeys keys, byte[] salt, byte[] noncePrefix)
    {
        _config = config;
        _options = options;
        _keys = keys;
        _salt = salt;
        _noncePrefix = noncePrefix;
    }

    /// <summary>Класи, призначені останнім викликом BuildPackets — потрібні для звіту.</summary>
    public FrameClass[] LastClasses { get; private set; } = [];

    public IReadOnlyList<Packet> BuildPackets(IReadOnlyList<TelemetryFrame> frames)
    {
        // TODO(S4). Порядок:
        //
        //   класи = _options.Classifier.Classify(frames)
        //   цінність[i] = пріоритет(клас) + critical_бонус × Tuning.CriticalWeight × [critical]
        //   черга = усі індекси
        //   поки черга не порожня:
        //       вікно = перші Tuning.WindowFrames елементів черги
        //       побудувати таблицю, ваги, місткість (див. коментар класу)
        //       обрати кадри пакувальником
        //       зібрати навантаження і пакет
        //       ВИЛУЧИТИ обрані з черги
        //
        // КРАЙНІ ВИПАДКИ, які треба передбачити (критерій C2 §4):
        //   • порожній сеанс → один порожній пакет, без падіння (кейс C1 №2);
        //   • місткість W може стати ВІД'ЄМНОЮ, якщо таблиця велика:
        //     перейдіть у режим NO_COMPRESSION, а не падайте;
        //   • якщо пакувальник не обрав жодного кадру, наступна ітерація
        //     нічого не змінить — забезпечте вихід із циклу.
        //
        // ЕКСПЕРИМЕНТ ДЛЯ ЗВІТУ: на пакеті 512 байтів таблиця Гаффмана
        // (~130 символів = 258 байтів із 423 доступних) може коштувати
        // більше, ніж економить. Порівняйте, скільки СИРИХ байтів уміщається
        // в пакет із таблицею і без неї, і зробіть із цього висновок.
        throw new NotImplementedException("Transmitter.BuildPackets");
    }
}
