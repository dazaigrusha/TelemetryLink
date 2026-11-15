# Частина 12 — діагностика втрат та пріоритетна доставка critical

Це повний проєкт поверх частини 11 (.NET 10, TelemetryLink.slnx). Шляхи нижче від кореня TelemetryLink. Видалених файлів немає.

## Що зроблено

- Деталізація кінцевих станів усіх кадрів і critical: recovered / over-budget / channel-lost / receiver-rejected / receiver-failed / content-mismatch / not-packed. Вони взаємно виключні й у сумі дають число унікальних вхідних кадрів.
- Політика `--delivery critical-first`: окремі critical-пакети максимум по 8 кадрів (або менше, якщо --window < 8); передача critical перед звичайними пакетами. Обраний scheduler визначає порядок усередині черг.
- Класифікація відбувається один раз ДО перегрупування, на початковій часовій послідовності. Усі індекси й цінності збережені. Формат, прапорці, ключі, HMAC та nonce не змінені.
- `compare --vary delivery=balanced,critical-first` змінює політику цілком. Run/compare друкують та експортують причини втрат. До manifest варіантів додано delivery.
- Режим призначений для записаного сеансу. Він не реалізує потокові дедлайни або повторні передачі. Типовим лишається balanced.

## Перевірка

353 тести пройдено без пропусків; Release build без помилок і попереджень; verify 11/11. Тести покривають класифікацію в початковому порядку, всі індекси рівно один раз, цінність кадрів, межу 8 critical, розміри пакетів, унікальні nonce, сумісне декодування, пріоритет черг навіть при зворотному scheduler, усі причини втрат/помилок, порожні та крайні сеанси, CSV/manifest і помилки CLI. Strict mode перевірений окремо: повертає 1 при недосягненому порозі.

Дані, канал, приймальні й еталонні тести викладача та збережена MLP побайтово не змінені. TRAIN/valid не використовувалися для навчання чи вибору змін.

## Результат та обмеження

Діагностика початкової конфігурації на контрольному seed 425243: 97 critical доставлено, 173 не передано через бюджет, 30 втрачено каналом. Після змін: 232 доставлено, 0 поза бюджетом, 68 втрачено каналом. Втрата зменшилась 67.67% → 22.67%; поріг 20% для цього seed ще не виконано.

Після розробки на seed 1001–1010 остаточну стратегію перевірено на 20 нових seed 2001–2020, по три повтори та один прогрів у всіх трьох сценаріях. Усього 360 виміряних прогонів. Emergency: середня втрата critical 63.93% → 13.73%, Q mean 0.568962 → 0.744076; поріг виконаний на 13/20 seed замість 0/20. У working кількість проходжень погіршилася з 19/20 до 14/20. Це пріоритетна доставка, а не універсальна гарантія. Повтори одного seed не є незалежними каналами. Час і Q залежать від середовища.

Усі числа, найгірші випадки й компроміси — `CRITICAL-DELIVERY.md`; виміри — `EXPERIMENTS-S10/`. Результати S9 збережені історичними.

## Запуск

```bash
dotnet build TelemetryLink.slnx -c Release -m:1
dotnet test tests/TelemetryLink.Tests -c Release -m:1 --no-build
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- verify
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- run --scenario emergency --delivery critical-first --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/critical.csv
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- compare --vary delivery=balanced,critical-first --scenario calm,working,emergency --seeds 2001,2002,2003 --repeats 3 --warmup 1 --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/delivery.csv
```

Повна команда для 20 seed наведена у CRITICAL-DELIVERY.md. `--strict` залишено без послаблень. Best.json оптимізатора містить лише три числові гени — для його повторного застосування політику --delivery слід вказати окремо.

## Додані файли

- `CRITICAL-DELIVERY.md`
- `EXPERIMENTS-S10/control.csv`
- `EXPERIMENTS-S10/control.manifest.json`
- `EXPERIMENTS-S10/control.summary.csv`
- `EXPERIMENTS-S10/delivery.csv`
- `EXPERIMENTS-S10/delivery.manifest.json`
- `EXPERIMENTS-S10/delivery.summary.csv`
- `EXPERIMENTS-S10/diagnostics.summary.json`
- `EXPERIMENTS-S10/manifest.json`
- `src/TelemetryLink.Cli/DeliveryDiagnosticColumns.cs`
- `src/TelemetryLink.Core/Metrics/DeliveryDiagnostics.cs`
- `src/TelemetryLink.Core/Pipeline/DeliveryPolicy.cs`
- `tests/TelemetryLink.Tests/CriticalDeliveryTests.cs`
- `tests/TelemetryLink.Tests/DeliveryCliTests.cs`
- `STEP-12.md`

## Змінені файли — замінити попередні версії

- `DEFENSE.md`
- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Cli/Commands/CompareCommand.cs`
- `src/TelemetryLink.Cli/ComparisonSettings.cs`
- `src/TelemetryLink.Cli/ExperimentExporter.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Cli/ReportRenderer.cs`
- `src/TelemetryLink.Core/Metrics/SessionReport.cs`
- `src/TelemetryLink.Core/Pipeline/PipelineOptions.cs`
- `src/TelemetryLink.Core/Pipeline/SessionRunner.cs`
- `src/TelemetryLink.Core/Pipeline/Transmitter.cs`

## Комміт

```bash
git add src tests README.md DEFENSE.md STEP-12.md CRITICAL-DELIVERY.md EXPERIMENTS-S10
git commit -m "feat: diagnose frame losses and prioritize critical telemetry delivery"
```

Наступна частина — надійність за втрат самого каналу та оптимізація з контролем порогів на кількох seed. У цій частині не змінювались random/GA і не додавалися повторні передачі.
