using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>Налаштування навчання. Кожен параметр — потенційний ген хромосоми ГА.</summary>
public sealed record TrainingOptions(
    int WindowSize = 8,
    int[]? HiddenLayers = null,
    int Epochs = 12,
    double LearningRate = 0.02,
    int Initializations = 3,
    double ValidationShare = 0.2,
    int Seed = 1)
{
    public int[] Hidden => HiddenLayers ?? [32, 16];
}

/// <summary>Підсумок навчання однієї ініціалізації.</summary>
public sealed record TrainingAttempt(int Seed, double ValidationMacroF1);

/// <summary>
/// Навчання за протоколом із team_config.json, секція «протокол_навчання»:
///
/// 1. TRAIN ділиться 80/20 на власне навчальну і валідаційну частини;
/// 2. навчання повторюється щонайменше з ТРЬОХ різних ініціалізацій;
/// 3. модель обирається за macro-F1 на валідаційній частині;
/// 4. підсумковий macro-F1 рахує викладач на прихованій holdout.csv.
///
/// Пункт 2 — не формальність. На тих самих даних одна й та сама мережа дає
/// macro-F1 від 0,30 до 0,90 залежно від початкових ваг: інколи навчання
/// збігається в поганий локальний мінімум і мережа згортається до
/// мажоритарного класу. Вибір за валідацією знімає цю лотерею.
///
/// КРОК S7.
/// </summary>
public sealed class PerceptronTrainer
{
    public IReadOnlyList<TrainingAttempt> Attempts { get; private set; } = [];

    public ClassifierModel Train(
        IReadOnlyList<TelemetryFrame> frames,
        IReadOnlyList<int> labels,
        TrainingOptions options)
    {
        // TODO(S7). Порядок:
        //
        //  1. витягти ознаки для ВСІХ кадрів;
        //  2. розбити 80/20 ПО ЧАСУ, а не випадково: ознаки беруться з вікна
        //     історії, і випадкове перемішування до розбиття підмішало б
        //     сусідні кадри валідації в навчальну частину;
        //  3. порахувати нормалізацію ПО НАВЧАЛЬНІЙ частині і застосувати до всіх;
        //  4. для кожної з options.Initializations ініціалізацій навчити мережу
        //     і виміряти macro-F1 на валідації;
        //  5. повернути модель із найкращим macro-F1, заповнивши Attempts.
        //
        // Порада: класи розподілені як 78/10/12 %. Без вагування класів мережа
        // швидко «вивчає» мажоритарну відповідь і macro-F1 лишається низьким.
        throw new NotImplementedException("PerceptronTrainer.Train");
    }
}
