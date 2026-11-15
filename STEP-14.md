# Частина 14 — підбір із контролем порогів на кількох seed

Повний проєкт поверх частини 13, .NET 10 / TelemetryLink.slnx. Шляхи нижче від кореня TelemetryLink. Видалених файлів немає.

## Що додано

Режим optimize --objective reliable оцінює кожного кандидата на повній сітці сценаріїв/seed/повторів. Допустимість вимагається в кожному прогоні, а не в середньому. Пріоритет: цілісність, виконання порогів, потім Q серед допустимих наборів. Score відокремлений від фізичної метрики Q.

Новий сценарій експерименту фіксує переможців усіх алгоритмів до першої validation. Search та validation seed не можуть перетинатися; перевірка не змінює вибір. Відсутність допустимого кандидата або невдала validation явно відображається у звітах; --strict повертає 1. Best.json у такому випадку все одно може існувати і не є гарантією допустимості.

Обидва алгоритми викликають fitness рівно budget разів. Повна search-grid виконується завжди, без раннього виходу та кешування повторних кандидатів. Еліти GA як раніше не переоцінюються. Довідковий baseline та validation обліковуються окремо від бюджетів пошуку. Старі random/GA не змінені; попередній режим --objective q збережений типовим для сумісності.

## Перевірки

394 тести без пропусків, Release build без помилок/попереджень, verify 11/11. Перевірено ранжування допустимого проти високої Q з порушенням, окремі відмови попри гарне середнє, цілісність, нульовий поріг, повну сітку, рівний бюджет, відсутність вибору за validation, неперетин seed, CLI/CSV/manifest, захист входів і strict=1 при неможливому порозі в синтетичній конфігурації. Реальні дані та пороги викладача в цьому тесті не змінювалися — використано окремий тимчасовий файл.

Дані, канал, формат, криптографія, приймальні/еталонні тести та MLP побайтово не змінені. Збережений GA best.json завантажено й виконано з critical-repeat та --strict у контрольному emergency-прогоні.

## Виконаний експеримент

По 24 кандидати random/GA, GA population=8, optimizer seed=1. Search: три сценарії × seed 4001,4002,4003, отже 9 прогонів на кандидата, 216 на кожен алгоритм. Спільний baseline — ще 9. Validation: три конфігурації (baseline і два зафіксовані переможці) × три сценарії × 20 інших seed 5001–5020 = 180. Усього 621 повний прогін.

Обидва переможці виконали пороги в 60/60 комбінаціях validation. Mean Q: baseline 0.705938, random 0.730954, GA 0.730410. Найгірша аварійна втрата critical у кожного переможця — 8% при порозі 20%; working і calm — 0% на цій перевірці.

Це не гарантія прихованих seed, не глобальний оптимум і не доказ загальної переваги одного алгоритму. На цій search-grid всі 48 кандидатів були допустимими; поведінка при порушеннях перевірена контрприкладами в тестах. Приріст Q переважно в calm/working; emergency Q трохи нижча, ніж у baseline. Розгорнуті таблиці й точна формула score — RELIABLE-OPTIMIZATION.md.

## Використання

```bash
dotnet build TelemetryLink.slnx -c Release -m:1
dotnet test tests/TelemetryLink.Tests -c Release -m:1 --no-build
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- verify
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- optimize --objective reliable --optimizer random,ga --budget 24 --population 8 --search-seeds 4001,4002,4003 --validation-seeds 5001,5002,5003,5004,5005 --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --strict --out results/reliable.csv
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- run --scenario emergency --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --tuning EXPERIMENTS-S12/reliable.ga.best.json --strict
```

У скороченій команді validation має 5 seed. Повний список 20 seed для відтворення збереженого експерименту є у RELIABLE-OPTIMIZATION.md. Сценарії типово всі три. --fitness-repeats множить число прогонів. Best.json зберігає лише три гени; delivery/copies/gap/модель й алгоритми треба вказати такими самими, як під час пошуку.

## Додані файли

- `EXPERIMENTS-S12/manifest.json`
- `EXPERIMENTS-S12/reliable.candidates.csv`
- `EXPERIMENTS-S12/reliable.csv`
- `EXPERIMENTS-S12/reliable.ga.best.json`
- `EXPERIMENTS-S12/reliable.random.best.json`
- `EXPERIMENTS-S12/reliable.runs.csv`
- `EXPERIMENTS-S12/reliable.summary.json`
- `EXPERIMENTS-S12/validation.summary.csv`
- `RELIABLE-OPTIMIZATION.md`
- `src/TelemetryLink.Cli/Commands/ReliableOptimizeCommand.cs`
- `src/TelemetryLink.Cli/ReliableOptimizationSettings.cs`
- `src/TelemetryLink.Core/Optimization/ReliabilityFitness.cs`
- `src/TelemetryLink.Core/Optimization/ReliableOptimizationExperiment.cs`
- `tests/TelemetryLink.Tests/ReliabilityFitnessTests.cs`
- `tests/TelemetryLink.Tests/ReliableOptimizationCliTests.cs`
- `tests/TelemetryLink.Tests/ReliableOptimizationTests.cs`
- `STEP-14.md`

## Змінені файли — замінити попередні версії

- `DEFENSE.md`
- `README.md`
- `src/TelemetryLink.Cli/Commands/OptimizeCommand.cs`
- `src/TelemetryLink.Cli/Program.cs`

## Комміт

```bash
git add src tests README.md DEFENSE.md STEP-14.md RELIABLE-OPTIMIZATION.md EXPERIMENTS-S12
git commit -m "feat: optimize reliability across scenarios with separate validation seeds"
```
