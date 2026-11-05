using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>
/// Точка підключення «класифікація кадру».
///
/// Метод приймає ВЕСЬ сеанс, а не окремий кадр. Це не зручність, а вимога
/// задачі: клас «перехідний режим» принципово не видно в полях одного кадру,
/// він розпізнається лише за вікном історії.
/// </summary>
public interface IFrameClassifier
{
    /// <summary>Ім'я для CLI і звітів: mlp, majority.</summary>
    string Name { get; }

    FrameClass[] Classify(IReadOnlyList<TelemetryFrame> frames);
}

/// <summary>
/// Базова лінія: усі кадри — «норма».
///
/// Потрібна у двох ролях: тракт працює ще до появи мережі, і з нею
/// порівнюється macro-F1 навченої моделі. Мажоритарний класифікатор дає
/// macro-F1 близько 0,29 — саме тому поріг F1_min = 0,58 щось означає.
/// </summary>
public sealed class MajorityClassifier : IFrameClassifier
{
    public string Name => "majority";

    public FrameClass[] Classify(IReadOnlyList<TelemetryFrame> frames)
        => new FrameClass[frames.Count];        // FrameClass.Normal = 0
}
