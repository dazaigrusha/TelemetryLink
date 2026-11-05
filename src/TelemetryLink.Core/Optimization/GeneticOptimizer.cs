using TelemetryLink.Core.Configuration;

namespace TelemetryLink.Core.Optimization;

/// <summary>Параметри генетичного алгоритму.</summary>
public sealed record GeneticOptions(
    int PopulationSize = 12,
    int TournamentSize = 3,
    double MutationProbability = 0.2,
    double MutationScale = 0.15,
    int EliteCount = 1);

/// <summary>
/// Генетичний алгоритм з турнірною селекцією, рівномірним кросовером,
/// мутацією і елітизмом.
///
/// ПРО ЕЛІТИЗМ. Найкраща особина переноситься в наступне покоління без змін.
/// Звідси випливає перевірка коректності реалізації: найкращий результат
/// останнього покоління НЕ МОЖЕ бути гіршим за найкращий результат нульового.
/// Якщо він погіршився — помилка в коді, а не властивість алгоритму.
/// Це саме той інваріант, який перевіряє приймальний кейс D1 №16.
///
/// Вимоги «ГА обов'язково знайде кращі параметри, ніж підібрані вручну» немає.
///
/// КРОК S8.
/// </summary>
public sealed class GeneticOptimizer : IParameterOptimizer
{
    private readonly GeneticOptions _options;

    public GeneticOptimizer(GeneticOptions? options = null) => _options = options ?? new GeneticOptions();

    public string Name => "ga";

    public OptimizationResult Optimize(Func<Tuning, double> fitness, int evaluationBudget, int seed)
    {
        // TODO(S8). Схема:
        //
        //   покоління 0: PopulationSize випадкових особин, оцінити кожну
        //   поки не вичерпано бюджет:
        //       перенести EliteCount найкращих БЕЗ ЗМІН і БЕЗ ПЕРЕОЦІНКИ
        //       решту заповнити: турнір → кросовер → мутація → оцінити
        //       записати GenerationRecord(покоління, найкраща, середня)
        //
        // КРИТИЧНО: загальна кількість викликів fitness має дорівнювати
        // evaluationBudget. Порівняння з випадковим пошуком за різного
        // бюджету нічого не означає, і це перевіряється тестом.
        throw new NotImplementedException("GeneticOptimizer.Optimize");
    }

    /// <summary>
    /// Турнірна селекція: беремо k випадкових особин і повертаємо найкращу.
    /// Чим більший k, тим сильніший тиск відбору і швидше втрачається
    /// різноманітність популяції.
    /// </summary>
    public int SelectByTournament(double[] scores, Random random)
    {
        // TODO(S8)
        throw new NotImplementedException("GeneticOptimizer.SelectByTournament");
    }

    /// <summary>Рівномірний кросовер: кожен ген незалежно береться в одного з батьків.</summary>
    public static Tuning Crossover(Tuning first, Tuning second, Random random)
    {
        // TODO(S8)
        throw new NotImplementedException("GeneticOptimizer.Crossover");
    }

    /// <summary>
    /// Мутація: ген зсувається на частку свого діапазону і ЗАТИСКАЄТЬСЯ в межі.
    /// Без затискання популяція швидко виходить за область визначення
    /// і прогін просто падає.
    /// </summary>
    public Tuning Mutate(Tuning tuning, Random random)
    {
        // TODO(S8)
        throw new NotImplementedException("GeneticOptimizer.Mutate");
    }
}
