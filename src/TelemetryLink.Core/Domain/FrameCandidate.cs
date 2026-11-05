namespace TelemetryLink.Core.Domain;

/// <summary>
/// Кандидат на потрапляння в пакет: вага в бітах і цінність.
///
/// Тип існує, щоб розірвати залежність «пакувальник → Гаффман → телеметрія».
/// Алгоритм рюкзака нічого не знає ні про кадри, ні про коди — він бачить
/// лише три числа. Завдяки цьому тест на рюкзак пишеться на трьох числах.
/// </summary>
/// <param name="FrameIndex">Індекс кадру у вікні кандидатів.</param>
/// <param name="WeightBits">
/// Довжина 12 байтів кадру в бітах за ЄДИНОЮ таблицею Гаффмана, побудованою
/// для всього вікна до вибору (FORMAT_SPEC §3.5). Під час вибору не змінюється.
/// </param>
/// <param name="Value">v = пріоритет(клас) + critical_бонус × [critical == 1].</param>
public readonly record struct FrameCandidate(int FrameIndex, int WeightBits, int Value);
