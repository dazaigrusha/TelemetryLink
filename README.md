# TelemetryLink

Навчальний проєкт захищеного передавання телеметрії через канал з обмеженням розміру пакетів, часу та з втратами. C# / .NET 10, консольний інтерфейс. Рішення: `TelemetryLink.slnx`.

Програма читає кадри, визначає їхній клас, обирає кадри для пакета, стискає, шифрує, підписує, передає через модель каналу та перевіряє відновлення. Реалізовано Гаффмана, рюкзак (перебір/DP/greedy), планувальники, MLP, random search і GA.

## Частина 14: підбір параметрів із контролем надійності

Новий режим `optimize --objective reliable` оцінює кожен набір генів на кількох сценаріях і seed. Допустимий кандидат мусить виконати поріг втрат та точне відновлення у КОЖНОМУ прогоні. Допустимі кандидати завжди випереджають недопустимі; серед допустимих обирається найбільша середня Q. Пошук і відкладена перевірка мають окремі seed. Обидва переможці фіксуються до початку validation.

```bash
dotnet run --project src/TelemetryLink.Cli -- optimize --objective reliable --optimizer random,ga --budget 24 --population 8 --search-seeds 4001,4002,4003 --validation-seeds 5001,5002,5003,5004,5005 --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --strict --out results/reliable.csv
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --tuning EXPERIMENTS-S12/reliable.ga.best.json --strict
```

Типово reliable перевіряє calm,working,emergency; search-seeds=4001,4002,4003; validation-seeds=5001..5005; fitness-repeats=1. Одна оцінка кандидата за цими параметрами коштує 9 повних прогонів. `--budget` — число кандидатів на кожен алгоритм, а не число сесій; додатково оцінюються спільний baseline і зафіксовані переможці на validation. Пошук не зупиняє сітку після першої невдачі та не кешує повторні точки. --fitness-repeats 1..10 множить витрати. Рівний бюджет збережений і за кількістю оцінок, і за кількістю прогонів пошуку.

Збережений експеримент: по 24 кандидати random/GA, по 216 прогонів пошуку, потім окрема перевірка на 20 seed 5001–5020. Обидва переможці виконали всі 60 комбінацій validation. Середня Q: baseline 0.705938, random 0.730954, GA 0.730410. Це не гарантія прихованих seed і не доказ загальної переваги random або GA. На цій невеликій search-grid всі 48 кандидатів були допустимими; відмови й захист від вибору за validation перевірені окремими тестами.

`--strict` повертає 1, якщо переможець не виконав вимоги пошуку чи validation. Best.json зберігається навіть при невдачі; його статус треба дивитися в summary. Він містить лише три гени — delivery/copies/gap/модель задаються окремо. Історія reliable зберігає **score**, а не Q; MeanQ і статуси наведені окремо. Старий режим одного Q доступний без --objective або з --objective q. Нові прапорці seed без reliable відхиляються, а не ігноруються.

394 тести без пропусків, verify 11/11, Release build без помилок/попереджень. Виміри — `EXPERIMENTS-S12/`, повний протокол і пояснення — `RELIABLE-OPTIMIZATION.md`, перелік файлів та комміт — `STEP-14.md`.

## Частина 13 (історія): рознесені повтори critical

Додано `--delivery critical-repeat`: перший раунд critical-пакетів → до 8 звичайних пакетів → другий раунд тих самих critical-пакетів → решта звичайних. Це заздалегідь визначений план без ACK чи знання стану каналу. MLP і формат пакета не змінені. Типово copies=2 (один повтор), gap=8; дозволені --critical-copies 1..3 і --repeat-gap 0..64. Якщо звичайних пакетів недостатньо, проміжок коротший.

```bash
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --delivery critical-repeat --critical-copies 2 --repeat-gap 8 --classifier mlp --model EXPERIMENTS-S7/mlp.json --strict --out results/repeat.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary delivery=balanced,critical-first,critical-repeat --scenario calm,working,emergency --seeds 3001,3002,3003 --repeats 2 --warmup 1 --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/repeats.csv
```

Копія використовує ті самі незмінені байти захищеного пакета. Повторного шифрування або нового plaintext зі старим nonce немає. Це повтор датаграма, а не дублювання кадрів усередині payload; прапорець Duplication не встановлюється. Кожна доставлена копія проходить перевірку. Унікальні кадри та цінність зараховуються один раз; витрати всіх фактично надісланих копій входять у SentBits та Q. Деталі обліку — `REPEATED-DELIVERY.md`.

На 20 нових seed 3001–3020: у critical-repeat середня аварійна втрата critical 2.47%, найгірша 13.33%; поріг 20% пройдено 20/20. Робочий поріг 5% також пройдено 20/20. Це результат для даної сесії та перевірених seed, а не гарантія для прихованого каналу. В аварійному режимі звичайних кадрів доставлено менше; у calm повтори знижують Q через зайві біти. Типовий balanced не змінений.

369 тестів без пропусків, verify 11/11, Release build без помилок і попереджень. Файли вимірів — `EXPERIMENTS-S11/`, результати — `REPEATED-DELIVERY.md`, усі додані й змінені файли — `STEP-13.md`. Контроль порогів у random/GA додано режимом reliable у частині 14. Для повторного використання best.json delivery/copies/gap задаються окремо від трьох генів.

## Частина 12 (історія): діагностика та пріоритет critical

Новий режим `--delivery critical-first` пакує критичні та звичайні кадри окремо й передає critical-пакети першими. У critical-пакеті не більше 8 кадрів (або менше, якщо --window < 8), щоб одна втрата не забирала великий блок критичних даних. Звичайне вікно задається --window без додаткового обмеження. Обраний scheduler визначає порядок усередині кожної черги. Це пакетна обробка **вже записаного сеансу**: кадри можуть групуватися з різних моментів часу; режим не є потоковим планувальником із дедлайнами. MLP спочатку обробляє початкову часову послідовність один раз, потім результати прив'язуються до оригінальних індексів. Label не використовується.

```bash
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --delivery critical-first --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/critical.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary delivery=balanced,critical-first --scenario calm,working,emergency --seeds 2001,2002,2003 --repeats 3 --warmup 1 --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/delivery.csv
```

Типовий режим лишається `balanced` для відтворення попередніх результатів. `--delivery` підтримується run, compare та optimize. Best.json оптимізатора зберігає тільки три числові параметри: політику доставки при повторному запуску треба вказувати окремо. `compare --vary delivery=...` порівнює всю політику (групування + пріоритет черг), а не лише scheduler.

У CSV run/compare додано delivery і причини недоставки critical: over_budget, channel_lost, receiver_rejected, receiver_failed, content_mismatch, not_packed. Разом із recovered_critical вони дають загальну кількість critical. NotPacked означає відсутність кадру серед сформованих пакетів; за штатного завершення всі кадри пакуються. ReceiverFailed також враховує пакети, що надійшли вже після аварії декодера.

Результати цієї частини — `CRITICAL-DELIVERY.md`, файли — `EXPERIMENTS-S10/`, повний список змін — `STEP-12.md`. Попередні `RESULTS-S9.md` і результати S9 залишені історичними. Новий режим усуває причину втрат через бюджет у перевірених аварійних запусках, але не гарантує порогу 20% за втрат самого каналу. У фінальному порівнянні emergency середня втрата critical зменшилася з 63.93% до 13.73%, але поріг виконаний лише на 13/20 нових seed; робочий сценарій може погіршуватися. Формат пакетів, канал та еталони не змінені. У режимі critical-first кожен пакет передається один раз; повтори додано окремою політикою critical-repeat у частині 13.

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
