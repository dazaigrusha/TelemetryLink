# Частина 13 — повтори critical-пакетів

Повний проєкт поверх частини 12. .NET 10, TelemetryLink.slnx. Шляхи нижче від кореня TelemetryLink. Видалених файлів немає.

## Зміни

Додано critical-repeat: рознесені раунди передачі critical-пакетів із проміжком зі звичайних пакетів. Типово 2 спроби загалом та gap=8. Дозволено 1..3 спроби і gap 0..64. План не знає seed, ACK чи стану каналу.

Повтор використовує ТОЧНО ті самі байти/sequence/ciphertext/nonce/HMAC. Нового шифрування не відбувається. Payload, прапорці та базовий формат не змінені. Це транспортний повтор, а не розширення Duplication всередині payload. Канал викликається для кожної спроби; фактично надіслані копії збільшують B_sent і витрачають часовий бюджет.

Виправлено облік унікального приймання: DeliveredValue та recovered кадри зараховуються один раз. Додано лічильники повторних спроб, їхніх бітів, дублів та critical, уперше відновлених повтором. Діагностика об'єднує свідчення кількох спроб; одна бюджетна відмова не приховує попередню втрату каналом. Успіх переводить кадр у Recovered, але порушення цілісності лишає Q=0.

## Перевірка

369 тестів без пропусків, Release build без помилок/попереджень, verify 11/11. Тести перевіряють план/раунди, незмінні байти, оплату всіх копій, унікальні кадри/цінність, відновлення повтором, бюджет, HMAC кожної копії, збереження помилок цілісності, діагностику, copies=1, порожні й безcritical сеанси, відтворюваність та CLI/CSV/manifest. Незмінність даних, каналу, криптографічного формату, приймальних/еталонних тестів і MLP перевірена побайтово.

## Виміри

20 нових seed 3001–3020, 3 сценарії, 3 режими, по 2 повтори після 1 прогріву: 360 виміряних прогонів. Заздалегідь зафіксовано copies=2, gap=8; після оцінки параметри не змінювалися. Повтори одного seed не є незалежними реалізаціями каналу.

Emergency: mean loss balanced 63.47%, critical-first 13.00%, critical-repeat 2.47%. Worst loss нового режиму 13.33%, поріг 20% виконано 20/20. Working: у critical-repeat mean 0.13%, worst 2.67%, поріг 5% виконано 20/20. Контрольний seed 425243: 67.67% → 22.67% → 8.00%; strict run нового режиму повернув 0.

Це не гарантія прихованих seed або інших сесій. В emergency mean усіх доставлених кадрів зменшилося з 954.45 у critical-first до 702.30 у critical-repeat: пріоритет критичних кадрів коштує звичайних. У calm повтори знижують Q. Типовий balanced збережений. Список фізичних і унікальних лічильників, таблиці та обмеження — `REPEATED-DELIVERY.md`.

## Запуск

```bash
dotnet build TelemetryLink.slnx -c Release -m:1
dotnet test tests/TelemetryLink.Tests -c Release -m:1 --no-build
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- verify
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- run --scenario emergency --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --strict --out results/repeat.csv
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- compare --vary delivery=balanced,critical-first,critical-repeat --scenario calm,working,emergency --seeds 3001,3002,3003 --repeats 2 --warmup 1 --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/repeats.csv
```

## Додані файли

- `EXPERIMENTS-S11/control.csv`
- `EXPERIMENTS-S11/control.manifest.json`
- `EXPERIMENTS-S11/control.summary.csv`
- `EXPERIMENTS-S11/manifest.json`
- `EXPERIMENTS-S11/reliability.summary.json`
- `EXPERIMENTS-S11/repeats.csv`
- `EXPERIMENTS-S11/repeats.manifest.json`
- `EXPERIMENTS-S11/repeats.summary.csv`
- `REPEATED-DELIVERY.md`
- `src/TelemetryLink.Core/Pipeline/TransmissionPlan.cs`
- `tests/TelemetryLink.Tests/CriticalRepeatCliTests.cs`
- `tests/TelemetryLink.Tests/CriticalRepeatTests.cs`
- `STEP-13.md`

## Змінені файли — замінити попередні версії

- `DEFENSE.md`
- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Cli/DeliveryDiagnosticColumns.cs`
- `src/TelemetryLink.Cli/ExperimentExporter.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Cli/ReportRenderer.cs`
- `src/TelemetryLink.Core/Metrics/DeliveryDiagnostics.cs`
- `src/TelemetryLink.Core/Metrics/SessionReport.cs`
- `src/TelemetryLink.Core/Pipeline/DeliveryPolicy.cs`
- `src/TelemetryLink.Core/Pipeline/PipelineOptions.cs`
- `src/TelemetryLink.Core/Pipeline/SessionRunner.cs`
- `src/TelemetryLink.Core/Pipeline/Transmitter.cs`

## Комміт

```bash
git add src tests README.md DEFENSE.md STEP-13.md REPEATED-DELIVERY.md EXPERIMENTS-S11
git commit -m "feat: add spaced critical packet repetition with unique delivery accounting"
```

Далі — підбір параметрів із контролем порогів на кількох seed. У цій частині random/GA не змінені; best.json зберігає лише три гени, тому delivery/copies/gap при запуску вказуються окремо.
