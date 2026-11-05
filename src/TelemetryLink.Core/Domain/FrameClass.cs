namespace TelemetryLink.Core.Domain;

/// <summary>
/// Клас кадру, який визначає перцептрон (FORMAT_SPEC §3.4).
/// Значення 3..255 зарезервовані.
/// </summary>
public enum FrameClass : byte
{
    /// <summary>Норма. Близько 78 % кадрів.</summary>
    Normal = 0,

    /// <summary>Аномалія. Найменший клас (~10 %) — саме його карає macro-F1.</summary>
    Anomaly = 1,

    /// <summary>
    /// Перехідний режим (~12 %). Не розпізнається за полями одного кадру:
    /// потрібні ознаки з вікна історії.
    /// </summary>
    Transition = 2
}
