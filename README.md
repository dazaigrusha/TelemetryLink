# TelemetryLink

Навчальний проєкт захищеного передавання телеметрії через канал з обмеженням розміру пакетів, часу та з втратами. C# / .NET 10, консольний інтерфейс. Рішення: `TelemetryLink.slnx`.

Програма читає кадри, визначає їхній клас, обирає кадри для пакета, стискає, шифрує, підписує, передає через модель каналу та перевіряє відновлення. Реалізовано Гаффмана, рюкзак (перебір/DP/greedy), планувальники, MLP, random search і GA.

## Частина 12: діагностика та пріоритет critical

Новий режим `--delivery critical-first` пакує критичні та звичайні кадри окремо й передає critical-пакети першими. У critical-пакеті не більше 8 кадрів (або менше, якщо --window < 8), щоб одна втрата не забирала великий блок критичних даних. Звичайне вікно задається --window без додаткового обмеження. Обраний scheduler визначає порядок усередині кожної черги. Це пакетна обробка **вже записаного сеансу**: кадри можуть групуватися з різних моментів часу; режим не є потоковим планувальником із дедлайнами. MLP спочатку обробляє початкову часову послідовність один раз, потім результати прив'язуються до оригінальних індексів. Label не використовується.

```bash
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --delivery critical-first --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/critical.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary delivery=balanced,critical-first --scenario calm,working,emergency --seeds 2001,2002,2003 --repeats 3 --warmup 1 --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/delivery.csv
```

Типовий режим лишається `balanced` для відтворення попередніх результатів. `--delivery` підтримується run, compare та optimize. Best.json оптимізатора зберігає тільки три числові параметри: політику доставки при повторному запуску треба вказувати окремо. `compare --vary delivery=...` порівнює всю політику (групування + пріоритет черг), а не лише scheduler.

У CSV run/compare додано delivery і причини недоставки critical: over_budget, channel_lost, receiver_rejected, receiver_failed, content_mismatch, not_packed. Разом із recovered_critical вони дають загальну кількість critical. NotPacked означає відсутність кадру серед сформованих пакетів; за штатного завершення всі кадри пакуються. ReceiverFailed також враховує пакети, що надійшли вже після аварії декодера.

Результати цієї частини — `CRITICAL-DELIVERY.md`, файли — `EXPERIMENTS-S10/`, повний список змін — `STEP-12.md`. Попередні `RESULTS-S9.md` і результати S9 залишені історичними. Новий режим усуває причину втрат через бюджет у перевірених аварійних запусках, але не гарантує порогу 20% за втрат самого каналу. У фінальному порівнянні emergency середня втрата critical зменшилася з 63.93% до 13.73%, але поріг виконаний лише на 13/20 нових seed; робочий сценарій може погіршуватися. Формат пакетів, канал та еталони не змінені. Критичні пакети не дублюються й не передаються повторно.

## Запуск з чистого каталогу

Встановіть .NET 10 SDK. Розпакуйте проєкт і відкрийте термінал у папці, де `TelemetryLink.slnx`. Дані вже лежать у `data/`, готова модель — у `EXPERIMENTS-S7/mlp.json`.

```bash
dotnet --version
dotnet restore TelemetryLink.slnx
dotnet build TelemetryLink.slnx -c Release -m:1
dotnet test tests/TelemetryLink.Tests -c Release -m:1 --no-build
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- verify
dotnet run --project src/TelemetryLink.Cli -c Release --no-build -- run --scenario calm
```

Це варіант під .NET 10, а не попередня адаптація для VS 2022. Запуск із термінала не вимагає Visual Studio. Перший restore потребує доступу до NuGet. Версію SDK показує `dotnet --version`; якщо команда не знаходиться, перевідкрийте термінал після встановлення SDK.

## Основні команди

```bash
dotnet run --project src/TelemetryLink.Cli -- inspect --session data/session.csv
dotnet run --project src/TelemetryLink.Cli -- verify
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/run.csv
dotnet run --project src/TelemetryLink.Cli -- experiment --name order --out results/order.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary packer=dp,greedy --classifier mlp --model EXPERIMENTS-S7/mlp.json --scenario calm,working,emergency --seeds 1001,1002,1003 --repeats 3 --warmup 1 --out results/packers.csv
dotnet run --project src/TelemetryLink.Cli -- compare --ablation --classifier mlp --model EXPERIMENTS-S7/mlp.json --scenario calm,working,emergency --seeds 1001,1002,1003 --repeats 3 --warmup 1 --out results/ablation.csv
dotnet run --project src/TelemetryLink.Cli -- optimize --optimizer random,ga --budget 200 --seed 1 --scenario emergency --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/optimization.csv
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --classifier mlp --model EXPERIMENTS-S7/mlp.json --tuning EXPERIMENTS-S8/optimization.ga.best.json
```

`run` виконує один сценарій; для трьох сценаріїв запускайте його окремо з calm, working, emergency. У `compare` можна перелічити їх через кому. `--strict` повертає ненульовий код, якщо перевищено поріг втрат critical. Без strict це попередження; неточне відновлення завжди є помилкою. ExactRestore перевіряє доставлені пакети, а не відсутність втрат каналу.

Без явних прапорців: huffman, dp, greedy scheduler, majority classifier. Для MLP потрібні обидва прапорці `--classifier mlp --model ...`. `--tuning` завантажує три параметри з best.json; явні --window/--threshold/--critical-weight їх перевизначають. Поріг стиснення порівнює кількість обраних кадрів при стисненні та без нього; це не значення у відсотках.

## Навчання MLP

```bash
dotnet run --project src/TelemetryLink.Cli -- train --no-test --out results/internal.json
dotnet run --project src/TelemetryLink.Cli -- train --out results/mlp.json
```

TRAIN ділиться хронологічно 80/20. Scaler та балансування рахуються тільки за першими 80%. Ознаки причинні: поточний і попередні кадри. Перебираються щонайменше три ініціалізації та обирається найкраща внутрішня валідація. valid.csv читається лише після вибору. Для підбору параметрів використовуйте --no-test; підсумковий TEST не використовуйте для перебору моделей.

Збережений результат частини 9: internal macro-F1=0.8989, open TEST macro-F1=0.8778 при порозі 0.58. У фінальній частині модель повторно не навчалася. Прихований holdout викладача не перевірявся. `--window` для train — історія ознак, для run — вікно пакування.

## Результати та обмеження

П'ять фінальних таблиць — `RESULTS-S9.md`, короткі відповіді й карта коду — `DEFENSE.md`. Файли свіжих вимірів — `EXPERIMENTS-S9/`. Повний список змін цієї частини — `STEP-11.md`; попередні STEP та EXPERIMENTS збережені як історія.

Q = 0.5*S + 0.35*R + 0.15*T. S=B_ref/(B_ref+B_sent), R — частка відновлених critical, T=20/(20+t). Пошкодження/неточне відновлення або R=0 дає Q=0. За відсутності critical R=1. Коефіцієнт стиснення й S — різні метрики.

Час, T та Q залежать від комп'ютера. Повтори того самого seed не є незалежними реалізаціями каналу. GA не гарантує перевагу над random. Оптимізація Q не накладає жорсткого обмеження на втрату critical. Аварійний сценарій у збережених результатах перевищує допустимі 20%; це зазначено у таблицях.

Експеримент order порівнює потік із байтовим доповненням та таблицю Гаффмана. Загальні витрати заголовка/salt/nonce/HMAC виключені з обох схем. Це лабораторне вимірювання обсягу, а не зміна робочого формату пакета. Salt і nonce випадкові; encrypt-first може мати трохи інший розмір при повторному запуску.

## Структура

- `src/TelemetryLink.Core/` — алгоритми, формат, криптографія, тракт і метрики.
- `src/TelemetryLink.Cli/` — команди, параметри й експорт.
- `tests/TelemetryLink.Tests/` — модульні та приймальні перевірки.
- `data/` — комплект даних і еталонів викладача.
- `EXPERIMENTS-S7/` — готова модель MLP та протокол навчання.
- `EXPERIMENTS-S8/` — приклад оптимізації частини 10.
- `EXPERIMENTS-S9/` — фінальні виміри.

Сторонні ML-бібліотеки не використовуються. AES/SHA/HMAC/PBKDF2 беруться з System.Security.Cryptography; схема CTR, формат та порядок перевірок реалізовані в проєкті.
