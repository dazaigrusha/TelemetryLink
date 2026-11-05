using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>
/// Найпростіший пакувальник: бере кадри в порядку надходження і зупиняється
/// на першому, який не вміщається. Нічого не оптимізує і не переставляє.
///
/// КРОК S3. Це ПЕРША робоча версія тракту: до Чекпоінту 1 система має бути
/// наскрізною і давати базове Q, а рюкзака ще немає — його місце тимчасово
/// займає цей клас. Оскільки він реалізує той самий IFramePacker, заміна на
/// ДП на кроці S4 не торкнеться ні передавача, ні формату, ні CLI.
///
/// Далі він лишається базовою лінією «без пакування взагалі»: різниця між
/// sequential і dp показує, скільки дає саме вибір кадрів.
/// </summary>
public sealed class SequentialPacker : IFramePacker
{
    public string Name => "sequential";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        // TODO(S3): брати кадри підряд, поки сумарна вага не перевищує місткість.
        // Зупинитися на першому, що не влазить (саме break, а не continue —
        // це наївна версія, і різниця з жадібним варіантом має бути видимою).
        throw new NotImplementedException("SequentialPacker.Select");
    }
}
