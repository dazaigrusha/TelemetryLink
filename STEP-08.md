# Частина 8 — S6: порівняння та звітність

Основа — TelemetryLink_part07_Scheduler.zip. .NET 10; рішення TelemetryLink.slnx.
Архів містить повний проєкт. Для оновлення Git перенесіть лише файли зі списків; папку .git збережіть власну.

## Що тепер робить compare
- Змінює один компонент: codec, packer, scheduler або classifier. --ablation замінює по одному блоку поточної конфігурації.
- Типово виконує 1 прогрів і 3 вимірювання кожного варіанта на кожному seed. Прогрів до статистики та CSV не входить.
- Використовує однакові дані, сценарії і seed для варіантів. У повторах змінює перший варіант за циклом, щоб не залишати всі початкові витрати одному алгоритму.
- Друкує середні S/R/T/Q, вибірковий SD Q, парну середню різницю ΔQ, кількість спостережень та середній час; показує текстову гістограму.
- Зберігає всі виміри, підсумкову таблицю й JSON параметрів, контрольних сум входів, середовища та протоколу.
- Не приховує невдалі виміри. Помилка точного відновлення дає код повернення 1; --strict також сигналізує про перевищення порога втрати critical. Помилкові аргументи дають код 2.
- Не перезаписує вхідний сеанс, конфігурацію чи модель файлами compare.

Числові параметри CLI більше не підміняються типовими при тексті, NaN, Infinity або відсутньому значенні: повертається зрозуміла помилка.
Окремий run теж експортує seed, параметри пакування, облік каналу й ознаки цілісності у CSV.

## Контроль збою приймача
Додано окрему ознаку ReceiverCrashed. Якщо декодер кидає виняток, точне відновлення вважається невдалим, Q=0, наступне декодування зупиняється, а облік спроб передавання каналом продовжується. Вміст пакета й текст винятку не записуються в звіт. Це перевірено навіть для збою ПІСЛЯ успішного приймання критичних кадрів.
Звичайна відмова E_HMAC/E_PAYLOAD тощо не позначається як аварія декодера, але порушення відновлення так само дає Q=0.

## Змінено
- `README.md`
- `src/TelemetryLink.Cli/CommandLineOptions.cs`
- `src/TelemetryLink.Cli/Commands/CompareCommand.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Cli/ReportRenderer.cs`
- `src/TelemetryLink.Core/Metrics/SessionReport.cs`
- `src/TelemetryLink.Core/Pipeline/Receiver.cs`
- `src/TelemetryLink.Core/Pipeline/SessionRunner.cs`
- `tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Додано
- `src/TelemetryLink.Cli/ComparisonSettings.cs`
- `src/TelemetryLink.Cli/ExperimentExporter.cs`
- `src/TelemetryLink.Core/Metrics/ComparisonExperiment.cs`
- `tests/TelemetryLink.Tests/ComparisonCliTests.cs`
- `tests/TelemetryLink.Tests/ExperimentTests.cs`
- `tests/TelemetryLink.Tests/QualityMetricTests.cs`
- `STEP-08.md`

- `EXPERIMENTS-S6/`: три комплекти прикладів packers, ablation, schedulers; у кожному .csv, .summary.csv, .manifest.json (9 файлів).

Файлів не видалено. Дані викладача, модель каналу, еталонні файли та готові AcceptanceCaseTests/ReferencePacketTests збережено. У TestsToWrite прибрано лише заготовки вже реалізованих тестів S6; тести macro-F1 залишилися для наступної частини. Тестовий .csproj також треба скопіювати: тепер він посилається на CLI для перевірки команд і експорту.

## Запуск із кореня рішення
```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- compare --vary packer=sequential,greedy,dp --scenario calm,working,emergency --out results/packers.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary scheduler=sequential,greedy --seeds 425243,425244 --repeats 3 --warmup 1 --out results/schedulers.csv
dotnet run --project src/TelemetryLink.Cli -- compare --ablation --out results/ablation.csv
dotnet run --project src/TelemetryLink.Cli -- verify
```
Для короткої перевірки:
```bash
dotnet run --project src/TelemetryLink.Cli -- compare --vary codec=none,huffman --scenario calm --repeats 1 --warmup 0 --out results/quick.csv
```

--repeats: 1..1000, типово 3. --warmup: 0..100, типово 1.
--seeds задає явний список різних цілих чисел; або можна вказати один --channel-seed. Разом ці два прапорці не приймаються.
--vary і --ablation також взаємовиключні. Варіанти, сценарії та seed не повинні повторюватися, порожні елементи не допускаються.
Для brute усім порівнюваним варіантам задайте однакове --window <= 20.
--out має закінчуватися на .csv.

## Що зберігається
Для --out results/packers.csv:

| Файл | Вміст |
|---|---|
| packers.csv | кожен вимір: варіант, seed, повтор, алгоритми, параметри, біти, відновлені кадри, втрати, S/R/T/Q, цілісність і пороги |
| packers.summary.csv | середні значення, SD Q, парне ΔQ, samples, measured_runs |
| packers.manifest.json | SHA-256 сеансу/конфігурації/моделі (якщо є), параметри всіх варіантів і сценаріїв, коефіцієнти метрики, runtime/OS/архітектура, час створення та протокол |

Числа CSV мають десяткову крапку; текст із комами, лапками чи переносами екранується. JSON не містить пароля чи ключів. Контрольні суми ідентифікують файли; для відтворення потрібні відповідні вхідні файли з комплекту й ця версія коду. Файли джерел із наданими хешами не створюються заново автоматично.
Перші колонки CSV звичайного run збережені, нові поля додані наприкінці. Формат compare тепер окремий і докладніший; старі приклади частин 5–7 не переписані.

## Як читати статистику
- N/samples — кількість рядків для варіанта. Наприклад, 2 seed * 3 повтори = 6.
- Повтори одного seed вимірюють насамперед розкид часу. Це НЕ незалежні реалізації випадкових втрат. Два seed не дають підстав узагальнювати результат на всі канали.
- SD — вибіркове стандартне відхилення з дільником n-1. При n=1 тут прийнято SD=0 за домовленістю, а не як доказ відсутності шуму.
- ΔQ рахується попарно з базою для тих самих scenario/seed/repeat. База — перший варіант --vary або baseline у --ablation.
- Інтервалів довіри чи тестів значущості ця частина не розраховує. Невелику різницю Q одного експерименту не слід оголошувати доведеною перевагою.
- Час обчислень, T та Q залежать від середовища. Прогрів і ротація зменшують окремі зміщення, але не усувають весь шум; для своїх вимірів не запускайте інші експерименти одночасно.

## Особливість поточного ablation
Поточний classifier — majority. Тому рядок majority-classifier не є новою конфігурацією: він бере базові спостереження з позначкою reused_baseline=true, measured_runs=0 і ΔQ=0. Це явно написано в консолі та manifest. Так само позначаються інші заміни, якщо обраний алгоритм уже був базовим.
Це не оцінка користі навченої мережі. Після S7 з --classifier mlp --model ... цей рядок стане справжнім порівнянням проти majority. Назва baseline означає поточну конфігурацію, а не завершений фінальний проєкт.
Якщо прогрів виявить порушення відновлення, експеримент переривається: недостовірні початкові дані не маскуються під успіх.

## Приклади у EXPERIMENTS-S6
- packers: 3 сценарії * 3 варіанти * 3 повтори = 27 вимірів, 9 підсумків; один відкритий seed 425243.
- ablation: 45 рядків, але лише 36 справжніх вимірів; 9 рядків majority-classifier повторно використовують базу. Прогрів виконується лише для справді різних конфігурацій.
- schedulers: 3 сценарії * 2 варіанти * 2 seed * 3 повтори = 36 вимірів, 6 підсумків.

У прикладі schedulers для emergency середнє R зросло з 0.3567 до 0.4050, середнє Q — приблизно з 0.56752 до 0.58326. Це опис результатів лише двох заданих seed. Аварійний поріг втрати critical досі не виконано.
Ablation також показує, що за поточного вікна й накладних витрат режим none може давати вищий Q за huffman. Цей результат не приховується й не означає, що кодек некоректний: таблиця, заголовок і HMAC повторюються у кожному обмеженому пакеті.

## Перевірено
SDK 10.0.100: збирання рішення без помилок і попереджень. 292 тести поточних частин пройшли, без пропусків; verify — 11/11.
Перевірені прогрів, кількість викликів/рядків, ротація порядку, парні seed, відомі середні та SD, no-op ablation, збереження невдалих вимірів, збої приймача, Q=0, порожні/крайні метрики, некоректні аргументи, екранування CSV, SHA-256, відсутність пароля в JSON, захист джерела від перезапису та --strict.
Окремо перевірено справжній CLI: помилковий --repeats повертає код 2 без створення вихідних файлів. Приклади CSV/JSON перевірені незалежно: кількість рядків, узгодження summary/manifest, хеш даних і позначки reused_baseline.

Після збирання:
```bash
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --no-build --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests|FullyQualifiedName~HuffmanTests|FullyQualifiedName~CompressionPacketTests|FullyQualifiedName~PipelineBaselineTests|FullyQualifiedName~PackerTests|FullyQualifiedName~TransmitterTests|FullyQualifiedName~SchedulerTests|FullyQualifiedName~ChannelTests|FullyQualifiedName~SchedulerPipelineTests|FullyQualifiedName~ExperimentTests|FullyQualifiedName~QualityMetricTests|FullyQualifiedName~ComparisonCliTests|(FullyQualifiedName~AcceptanceCaseTests&FullyQualifiedName!~Case09_)"
```
Повний dotnet test ще містить незавершений Case09 та тести MLP/GA. Ці тести викладача не переписувалися; це наступні етапи.

## Комміт після копіювання файлів
```bash
git add README.md src/TelemetryLink.Cli/CommandLineOptions.cs src/TelemetryLink.Cli/Commands/CompareCommand.cs src/TelemetryLink.Cli/Program.cs src/TelemetryLink.Cli/ReportRenderer.cs src/TelemetryLink.Core/Metrics/SessionReport.cs src/TelemetryLink.Core/Pipeline/Receiver.cs src/TelemetryLink.Core/Pipeline/SessionRunner.cs tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj tests/TelemetryLink.Tests/TestsToWrite.cs src/TelemetryLink.Cli/ComparisonSettings.cs src/TelemetryLink.Cli/ExperimentExporter.cs src/TelemetryLink.Core/Metrics/ComparisonExperiment.cs tests/TelemetryLink.Tests/ComparisonCliTests.cs tests/TelemetryLink.Tests/ExperimentTests.cs tests/TelemetryLink.Tests/QualityMetricTests.cs STEP-08.md EXPERIMENTS-S6
git commit -m "feat: add repeatable comparisons and experiment reports"
```

## Далі
S7 — ознаки телеметрії з вікном історії, власна MLP, навчання з трьома ініціалізаціями, macro-F1 та порівняння з majority. Після цього — оптимізація параметрів та фінальні експерименти.
