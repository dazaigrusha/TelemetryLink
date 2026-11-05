namespace TelemetryLink.Core.Compression;

/// <summary>
/// Побудова довжин кодів Гаффмана через ЧЕРГУ З ПРІОРИТЕТАМИ.
///
/// Повертаються саме ДОВЖИНИ, а не дерево: у пакет пишуться тільки довжини,
/// а коди обидві сторони відновлюють канонічно (FORMAT_SPEC §3.2–3.3).
/// Дерево після побудови довжин більше нікому не потрібне.
///
/// КРОК S3.
/// </summary>
public static class HuffmanTreeBuilder
{
    /// <summary>
    /// Довжини кодів для 256 символів. Нуль означає, що символ не трапляється.
    /// </summary>
    /// <param name="frequencies">Частоти, індекс = символ.</param>
    public static byte[] BuildLengths(ReadOnlySpan<int> frequencies)
    {
        // TODO(S3). Порядок дій:
        //
        //  1. Порожній алфавіт  → усі довжини 0 (пакет із нуля кадрів).
        //  2. РІВНО ОДИН символ → довжина 1, код 0. Довжина 0 у цьому випадку
        //     є невідповідністю формату (FORMAT_SPEC §3.2). Це окремий
        //     приймальний кейс C1 №7 — не забудьте його.
        //  3. Інакше: класичний Гаффман через PriorityQueue.
        //     Нічиї розв'язуються ДЕТЕРМІНОВАНО: кладіть у чергу пару
        //     (частота, номер вставки), листки — у порядку зростання символу.
        //     Без цього однакові дані дадуть різні коди від запуску до запуску.
        //  4. Якщо найдовший код > 15 біт — CodeLengthLimiter.RaiseFloor
        //     і побудувати дерево заново (до 64 спроб).
        //
        // ЗЛИВАТИ рідкісні символи в один ЗАБОРОНЕНО: це руйнує побайтове
        // відновлення, а escape-механізму формат не передбачає.
        throw new NotImplementedException("HuffmanTreeBuilder.BuildLengths");
    }

    /// <summary>Частоти байтів у вибірці.</summary>
    public static int[] CountFrequencies(ReadOnlySpan<byte> data)
    {
        // TODO(S3)
        throw new NotImplementedException("HuffmanTreeBuilder.CountFrequencies");
    }
}
