# Частина 11 — S9: фінальні експерименти та захист

Це повний проєкт поверх частини 10, .NET 10 / TelemetryLink.slnx. Шляхи нижче від кореня TelemetryLink. Видалених файлів немає.

## Що змінилося

Реалізована остання заглушка OrderOfOperationsExperiment: обидва порядки реально виконують Гаффмана та AES-CTR, враховують таблицю й байтове доповнення і перевіряють зворотне відновлення. Використовується свіжий ключ і різні nonce для двох порядків. CLI experiment вміє експортувати CSV та захищає вхід від перезапису. Застарілий коментар TODO у CodeTable замінено посиланням на наявний декодер; алгоритм декодування не змінений.

Підготовлено п'ять таблиць: DP/greedy, ablation, порядок операцій, поведінка за втрат, збіжність GA/random. Додано графік, контрольні хеші й пояснення для захисту з картою файлів. README переписано під готовий консольний проєкт та чистий запуск. Готова модель частини 9 використовується без повторного навчання.

## Що відкрити

- `README.md` — як запустити й відтворити команди.
- `RESULTS-S9.md` — усі п'ять таблиць, джерела даних та обмеження.
- `DEFENSE.md` — де реалізовані алгоритми та короткі відповіді.
- `EXPERIMENTS-S9/` — фактичні виміри й графік.

## Результат перевірок

342 тести пройдено без пропусків. Чиста копія без bin/obj: restore/build/test успішні, verify 11/11, run calm виконаний. Release build без помилок і попереджень. Залишків NotImplementedException або TODO у src немає. Дані, канал і приймальні/еталонні тести викладача побайтово не змінені.

Приклад order: 288000 сирих біт; compress-first 196592 біти, encrypt-first 292096 біт. Обидва відновлюють дані точно. Порівняння виключає загальні накладні витрати пакета і не змінює робочий формат.

Важливо для захисту: базові програмні частини завершені, але це не заява про проходження всіх порогів якості. Аварійний сценарій у фінальних вимірах втратив 67.67% critical при порозі 20%. Оптимізація Q не накладає жорстке обмеження на цей поріг. ExactRestore=true означає точність доставлених пакетів. Прихований TEST викладача не перевірявся. Q залежить від часу й середовища; результати одного seed оптимізатора не доводять перевагу GA або random.

## Додані файли

- `DEFENSE.md`
- `EXPERIMENTS-S9/ablation.csv`
- `EXPERIMENTS-S9/ablation.manifest.json`
- `EXPERIMENTS-S9/ablation.summary.csv`
- `EXPERIMENTS-S9/convergence.png`
- `EXPERIMENTS-S9/manifest.json`
- `EXPERIMENTS-S9/optimization.csv`
- `EXPERIMENTS-S9/optimization.ga.best.json`
- `EXPERIMENTS-S9/optimization.random.best.json`
- `EXPERIMENTS-S9/optimization.summary.json`
- `EXPERIMENTS-S9/order.csv`
- `EXPERIMENTS-S9/packers.csv`
- `EXPERIMENTS-S9/packers.manifest.json`
- `EXPERIMENTS-S9/packers.summary.csv`
- `EXPERIMENTS-S9/run-calm.csv`
- `EXPERIMENTS-S9/run-emergency.csv`
- `EXPERIMENTS-S9/run-working.csv`
- `RESULTS-S9.md`
- `tests/TelemetryLink.Tests/OrderOfOperationsTests.cs`
- `STEP-11.md`

## Змінені файли — замінити попередні версії

- `README.md`
- `src/TelemetryLink.Cli/Commands/ExperimentCommand.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Core/Format/CodeTable.cs`
- `src/TelemetryLink.Core/Metrics/OrderOfOperationsExperiment.cs`

## Комміт

```bash
git add src tests README.md STEP-11.md RESULTS-S9.md DEFENSE.md EXPERIMENTS-S9
git commit -m "feat: complete operation-order experiment and final telemetry evaluation"
```

Для першого запуску після заміни файлів:

```bash
dotnet build TelemetryLink.slnx -c Release -m:1
dotnet test tests/TelemetryLink.Tests -c Release -m:1 --no-build
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- verify
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- experiment --name order --out results/order.csv
```
