namespace TelemetryLink.Core.Configuration;

/// <summary>Закон затримки каналу.</summary>
public enum DelayKind
{
    /// <summary>Стала затримка.</summary>
    Constant,

    /// <summary>Рівномірна на відрізку.</summary>
    Uniform
}

/// <summary>
/// Параметри одного сценарію каналу (CHANNEL_SPEC §1).
///
/// Три сценарії: calm (спокійний), working (робочий), emergency (аварійний).
/// Саме transmission_budget_s робить канал справді вузьким: без нього
/// пропускна здатність лише змінювала б довідковий час.
/// </summary>
public sealed record ScenarioConfig(
    string Name,
    string UkrainianName,
    double LossProbability,
    double BurstMeanLength,
    DelayKind DelayKind,
    double DelayConstantMs,
    double DelayLowMs,
    double DelayHighMs,
    double BandwidthBitsPerSecond,
    double TransmissionBudgetSeconds)
{
    /// <summary>
    /// Імовірність переходу GOOD → BAD.
    ///     p_gb = loss / (loss + B × (1 − loss))
    /// Поширена помилка — loss / (B × (1 − loss)): вона дає 0,167 замість 0,15,
    /// бо не враховує, що перехід у BAD відбувається на пакеті, який теж втрачається.
    /// </summary>
    public double BadStateProbability =>
        LossProbability <= 0 ? 0.0
            : LossProbability / (LossProbability + BurstMeanLength * (1.0 - LossProbability));
}
