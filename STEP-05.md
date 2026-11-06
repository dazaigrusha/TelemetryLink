# Частина 5 — S3b: передавання через канал та метрика Q

Основа: TelemetryLink_part04_Huffman.zip, .NET 10, TelemetryLink.slnx. Це завершення першого наскрізного тракту S3. Архів містить повний проєкт; при оновленні Git копіюйте лише перелічені файли. Папку .git зберігайте власну.

## Що тепер працює
1. Вікно кандидатів (типово 64 кадри), таблиця кодів за всім вікном ДО вибору.
2. Послідовний вибір кадрів з урахуванням таблиці, полів payload, заголовка та HMAC. Кожен пакет <= 512 байтів у виданій конфігурації.
3. Кадри, що не вмістилися, переходять у наступне вікно. Немає втрат чи повторів під час формування.
4. Режим без стиснення, якщо таблиця не вміщується або поріг стиснення не виконується.
5. AES-CTR і HMAC. Ключі виводяться один раз на сеанс; у пакетів різні nonce.
6. Незмінена модель каналу викладача: втрати, пачки втрат, пропускна здатність і бюджет.
7. Приймач перевіряє пакет, рахує відмови та відновлює доставлені кадри. Вони звіряються побайтово з оригіналами, а не лише за кількістю.
8. S, R, T, Q, консольний звіт та CSV. Типові пакувальник і планувальник змінено на sequential: звичайна команда run вже працює без додаткових прапорців.

Поріг CompressionThreshold застосовано як мінімальне відношення кількості вибраних кадрів у стисненому режимі до кількості у сирому режимі для того самого вікна. При 1.0 стискаємо, коли вміщується не менше кадрів; при більшому порозі вимагаємо більший виграш. Якщо сирий режим не вміщує жодного кадру, але стиснений вміщує — використовуємо стиснений.

## Змінено
- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Cli/ReportRenderer.cs`
- `src/TelemetryLink.Core/Metrics/SessionReport.cs`
- `src/TelemetryLink.Core/Packing/SequentialPacker.cs`
- `src/TelemetryLink.Core/Pipeline/Receiver.cs`
- `src/TelemetryLink.Core/Pipeline/SessionRunner.cs`
- `src/TelemetryLink.Core/Pipeline/Transmitter.cs`

## Додано
- `tests/TelemetryLink.Tests/PipelineBaselineTests.cs`
- `BASELINE-S3.csv`
- `STEP-05.md`

Файлів не видалено. Модель каналу, data/, готові AcceptanceCaseTests.cs та ReferencePacketTests.cs не змінені.

## Запуск
Відкрийте термінал у корені рішення:
```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- run --scenario calm
dotnet run --project src/TelemetryLink.Cli -- run --scenario working --out results/working.csv
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --out results/emergency.csv
dotnet run --project src/TelemetryLink.Cli -- run --scenario calm --codec none
dotnet run --project src/TelemetryLink.Cli -- verify
```
Явна базова конфігурація:
```bash
dotnet run --project src/TelemetryLink.Cli -- run --scenario calm --packer sequential --scheduler sequential --classifier majority
```
Порівняння кодеків через вже наявну CLI-команду:
```bash
dotnet run --project src/TelemetryLink.Cli -- compare --vary codec=none,huffman --scenario calm --out results/codecs.csv
```
--strict повертає ненульовий код, якщо перевищено поріг втрати critical. Без --strict аварійний сценарій завершується штатно, навіть коли базова схема не досягає порогу. Порушення побайтового відновлення завжди дає ненульовий код.

## Перевірено
.NET SDK 10.0.100: збирання рішення без помилок і попереджень. 147 тестів реалізованих частин S0–S3 пройшли без пропусків; verify — 11/11.

Тести покривають обидва кодеки і три сценарії, порожній потік та один кадр, нуль критичних кадрів, однакові за значенням кадри з різними індексами, ліміт пакета, унікальність nonce, таблицю за всім вікном, нульовий бюджет, втрату всіх пакетів, приймання меншого пакета після відмови більшому за бюджетом, повторюваність доставки при тому самому seed та облік відмов приймача.

```bash
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests|FullyQualifiedName~HuffmanTests|FullyQualifiedName~CompressionPacketTests|FullyQualifiedName~PipelineBaselineTests"
```
Повний набір dotnet test ще містить заглушки наступних кроків. Приймальні тести з типовим dp/greedy залишаються для S4/S5; вони не переписані під спрощену схему.

## Базові результати на відкритому seed 425243
Всі 3000 кадрів потрапили у 111 пакетів. Налаштування: huffman, sequential, sequential, majority; window=64, threshold=0.5, critical-weight=1.

| Сценарій | Надіслано пакетів | Втрачено | Поза бюджетом | Відновлено кадрів | Відновлено critical | R | Q прикладу |
|---|---:|---:|---:|---:|---:|---:|---:|
| calm | 111 | 0 | 0 | 3000 | 300/300 | 1.00 | 0.6913 |
| working | 111 | 2 | 0 | 2950 | 294/300 | 0.98 | 0.6848 |
| emergency | 50 | 10 | 61 | 1073 | 108/300 | 0.36 | 0.5679 |

Доставлені пакети у всіх сценаріях відновлено точно. working: втрата critical 2% за дозволених 5%. emergency: втрата critical 64% за порогу 20%; поточна базова схема цей поріг не виконує. Це база для майбутньої оптимізації, а не фінальний результат всього проєкту.

BASELINE-S3.csv містить фактичні вимірювання цих запусків. Час, T та Q залежать від комп'ютера і запуску; це не еталонні константи. При тому самому seed розклад доставки й обсяг бітів відтворюються, хоча криптографічні сіль і префікс nonce випадкові.

Важлива відмінність від compress: там увесь сеанс вимірюється з однією таблицею, тут кожен обмежений пакет знову містить власну таблицю, 43 байти заголовка і 32 байти HMAC. Тому коефіцієнт повного пакета може бути < 1. У поточній базовій конфігурації він 0.6408. Виміряне порівняння calm показало, що none на цьому етапі дає вищий Q за huffman. Ці витрати не приховуються; далі покращуємо вибір кадрів і параметрів.

## Формули й облік
- B_ref = кількість вхідних кадрів * 12 * 8.
- B_sent — повні біти пакетів, які фактично зайняли канал. Втрачені враховуються, поза бюджетом — ні. Після відмови завеликому пакету продовжуємо пробувати наступні.
- S = B_ref / (B_ref + B_sent); при обох нулях прийнято S=1.
- R = відновлені critical / всі critical; якщо critical відсутні, R=1.
- T = t_ref / (t_ref + час обчислень). Симульований час каналу до часу обчислень НЕ додається; sleep не використовується.
- Q = alpha*S + beta*R + gamma*T. Якщо не відновлено жодного наявного critical або доставлені дані не збігаються з оригіналами, Q=0.
- ExactRestore стосується доставлених даних: втрата пакета не означає пошкодження решти пакетів.

## Комміт
```bash
git add README.md src/TelemetryLink.Cli/CommandContext.cs src/TelemetryLink.Cli/ReportRenderer.cs src/TelemetryLink.Core/Metrics/SessionReport.cs src/TelemetryLink.Core/Packing/SequentialPacker.cs src/TelemetryLink.Core/Pipeline/Receiver.cs src/TelemetryLink.Core/Pipeline/SessionRunner.cs src/TelemetryLink.Core/Pipeline/Transmitter.cs tests/TelemetryLink.Tests/PipelineBaselineTests.cs BASELINE-S3.csv STEP-05.md
git commit -m "feat: connect telemetry pipeline and calculate baseline quality"
```

## Наступна частина
S4 — повний перебір, динамічне програмування і жадібний алгоритм для рюкзака, перевірка DP проти перебору. Жадібний планувальник, MLP, GA, протоколи експериментів і фінальний звіт залишаються наступними етапами. Команди train, optimize, compare без --vary та --ablation поки звертаються до цих заглушок.
