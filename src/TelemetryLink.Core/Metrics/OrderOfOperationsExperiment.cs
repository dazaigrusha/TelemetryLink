using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Metrics;

/// <summary>Результат однієї схеми: скільки біт зайняли дані.</summary>
public readonly record struct OrderMeasurement(string Order, long RawBits, long ResultBits)
{
    public double Ratio => ResultBits == 0 ? 0.0 : (double)RawBits / ResultBits;
}

/// <summary>
/// Обов'язковий експеримент FORMAT_SPEC §3.5 і таблиця 3 звіту:
/// коефіцієнт стиснення при схемі «стиснути, потім зашифрувати» і при зворотній.
///
/// Очікуваний результат відомий заздалегідь, і саме тому експеримент корисний:
/// шифротекст статистично рівномірний, отже Гаффман на ньому дає коефіцієнт
/// близько 1,00 і ще витрачає біти на таблицю з 256 символів. Стиснення після
/// шифрування не просто марне — воно збільшує обсяг.
///
/// Цей експеримент НЕ проганяє дані через канал: питання стосується лише
/// розміру, тому й вимірюється лише розмір.
///
/// КРОК S9.
/// </summary>
public static class OrderOfOperationsExperiment
{
    public static IReadOnlyList<OrderMeasurement> Run(
        IReadOnlyList<TelemetryFrame> frames, string password)
    {
        // TODO(S9): порахувати розмір за обома схемами і повернути два виміри
        // з іменами "compress-first" і "encrypt-first".
        // Не забудьте врахувати розмір ТАБЛИЦІ, а не лише бітового потоку.
        throw new NotImplementedException("OrderOfOperationsExperiment.Run");
    }
}
