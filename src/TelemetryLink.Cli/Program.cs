using System.Text;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core;

Console.OutputEncoding = Encoding.UTF8;

var options = new CommandLineOptions(args);

try
{
    return options.Command switch
    {
        "compress" => CompressCommand.Execute(options),
        "run" => RunCommand.Execute(options),
        "compare" => CompareCommand.Execute(options),
        "verify" => VerifyCommand.Execute(options),
        "train" => TrainCommand.Execute(options),
        "optimize" => OptimizeCommand.Execute(options),
        "experiment" => ExperimentCommand.Execute(options),
        "inspect" => InspectCommand.Execute(options),
        _ => Help()
    };
}
catch (Exception exception) when (exception is ArgumentException or FileNotFoundException
                                  or DirectoryNotFoundException or InvalidDataException)
{
    // Помилки умови запуску показуються користувачеві одним рядком.
    // Усе інше — помилки програміста, і стек викликів там потрібен.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Помилка: {exception.Message}");
    Console.Error.WriteLine();
    return 2;
}

static int Help()
{
    Console.WriteLine("""

        TelemetryLink — захищений канал телеметрії з обмеженою пропускною здатністю

        tlm <команда> [опції]

          compress     перевірка стиснення і точного відновлення сеансу
          run          один прогін тракту, друк метрики Q
          compare      кілька прогонів із різними алгоритмами, таблиця і гістограма
          verify       приймальні перевірки на еталонних пакетах викладача
          train        навчити класифікатор (80/20, три ініціалізації)
          optimize     підібрати параметри: ГА проти випадкового пошуку
          experiment   окремі експерименти звіту (--name order)
          inspect      розібрати один .bin або показати сеанс

        Спільні опції
          --config <шлях>      team_config.json            (типово data/team_config.json)
          --session <шлях>     session.csv або session.bin (типово data/session.csv)
          --scenario <імена>   calm | working | emergency, можна через кому
          --model <шлях>       навчена модель для --classifier mlp
          --password <рядок>   пароль сеансу
          --out <шлях>         куди записати CSV або JSON

        Порівняння
          --vary <вимір=a,b>   codec | packer | scheduler | classifier
          --ablation          вимкнення по одному блоку поточної конфігурації
          --repeats <n>       повторів на кожен seed, типово 3
          --warmup <n>        прогрівів, типово 1; не входять до CSV
          --seeds <a,b>       явний список seed (або один --channel-seed)
          --out <file.csv>    повні виміри + .summary.csv + .manifest.json
          --strict           ненульовий код при перевищенні порогу втрати critical

        Навчання MLP
          --train <csv>        TRAIN (типово data/train.csv)
          --valid <csv>        відкритий TEST, оцінюється після вибору моделі
          --no-test            навчати без читання відкритого TEST
          --window <n>         для train: історія ознак; для run: вікно пакування
          --hidden <32,16>     приховані шари; none — лінійний softmax
          --epochs <n>         епох, типово 12
          --learning-rate <x>  початковий крок SGD, типово 0.02
          --inits <n>          щонайменше 3 різні ініціалізації
          --seed <n>           перший seed навчання, типово 1
          --out <model.json>   модель + .training.json + .history.csv

        Вибір алгоритмів
          --codec       {CODECS}
          --packer      {PACKERS}
          --scheduler   {SCHEDULERS}
          --classifier  {CLASSIFIERS}
          --optimizer   {OPTIMIZERS}

        Оптимізація
          --budget <n>         рівно n викликів fitness на кожен алгоритм
          --seed <n>           seed оптимізатора, типово 1
          --population <n>     розмір популяції GA, типово 12
          --tournament <n>     турнір з поверненням, типово 3
          --mutation <p>       імовірність мутації гена, типово 0.2
          --mutation-scale <x> частка діапазону гена, типово 0.15
          --elite <n>          еліт без переоцінки, типово 1
          --out <file.csv>     історія + .summary.json + .ga/.random.best.json
          --tuning <file.json> збережені параметри для run/compare

        Параметри тракту
          --window <n>              розмір вікна кандидатів
          --threshold <біт/байт>    поріг відмови від стиснення
          --critical-weight <коеф>  множник бонусу за критичність

        Приклади
          tlm verify
          tlm run --scenario emergency --packer dp --scheduler greedy
          tlm compare --vary packer=dp,greedy --scenario calm,working,emergency
          tlm compare --ablation
          tlm train --out model.json
          tlm run --classifier mlp --model model.json --scenario emergency
          tlm optimize --optimizer ga,random --budget 60
          tlm experiment --name order
          tlm inspect --packet data/reference/ref_01_single_frame.bin

        """
        .Replace("{CODECS}", string.Join(" | ", AlgorithmCatalog.CodecNames))
        .Replace("{PACKERS}", string.Join(" | ", AlgorithmCatalog.PackerNames))
        .Replace("{SCHEDULERS}", string.Join(" | ", AlgorithmCatalog.SchedulerNames))
        .Replace("{CLASSIFIERS}", string.Join(" | ", AlgorithmCatalog.ClassifierNames))
        .Replace("{OPTIMIZERS}", string.Join(" | ", AlgorithmCatalog.OptimizerNames)));

    return 0;
}
