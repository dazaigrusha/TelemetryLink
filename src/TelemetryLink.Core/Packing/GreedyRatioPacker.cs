using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>
/// Жадібний розв'язок рюкзака: кадри сортуються за відношенням
/// цінність / вага і беруться підряд, поки влазять.
///
/// Точного розв'язку НЕ гарантує. Це головна пара «жадібність проти ДП»
/// у проєкті: обидва алгоритми запускаються на тих самих даних, і різниця
/// ВИМІРЮЄТЬСЯ, а не припускається.
///
/// КРОК S4.
/// </summary>
public sealed class GreedyRatioPacker : IFramePacker
{
    public string Name => "greedy";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        // TODO(S4). Пастки:
        //   • нічиї у щільності розв'язуйте детерміновано, інакше результат
        //     «пливе» від запуску до запуску;
        //   • важкий кадр, що не влазить, — це continue, а НЕ break:
        //     легший кадр далі ще може вміститися;
        //   • повертати індекси у зростаючому порядку.
        throw new NotImplementedException("GreedyRatioPacker.Select");
    }
}
