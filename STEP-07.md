# Частина 7 — S5: жадібний планувальник пакетів

Основа — TelemetryLink_part06_Knapsack.zip, .NET 10, TelemetryLink.slnx. Архів містить повний проєкт. У свій Git перенесіть лише файли зі списків нижче; власну папку .git збережіть.

## Що додано
ValueDensityScheduler визначає порядок готових пакетів за спаданням сумарної цінності кадрів на байт. При однаковій щільності першим іде менший packet_seq; при повному збігу — попередній вхідний порядок. Порівняння використовує long і перехресне множення замість ділення double.

Повертаються ті самі об'єкти пакетів. Байти заголовка, sequence, nonce, шифротекст та HMAC не переписуються. Пакети не зникають і не дублюються. Порожній список і один пакет підтримуються; для нульової довжини збережено доменне правило ValueDensity=0 (реальні пакети мають щонайменше 75 байтів).

Типовий планувальник CLI тепер greedy. Повна поточна конфігурація: huffman + dp + greedy + majority. Sequential лишився доступним як базова лінія.
Модель втрат, затримок і бюджету не змінена — це виданий код викладача.

## Змінено
- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Core/Scheduling/Schedulers.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Додано
- `tests/TelemetryLink.Tests/SchedulerTests.cs`
- `tests/TelemetryLink.Tests/ChannelTests.cs`
- `tests/TelemetryLink.Tests/SchedulerPipelineTests.cs`
- `SCHEDULER-COMPARISON-S5.csv`
- `STEP-07.md`

Файлів не видалено. З TestsToWrite.cs перенесено реалізовані групи SchedulerTests та ChannelTests. Дані, Channel/*, AcceptanceCaseTests.cs і ReferencePacketTests.cs залишилися побайтово незмінними.

## Запуск із кореня рішення
```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- run --scenario calm
dotnet run --project src/TelemetryLink.Cli -- run --scenario working
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --out results/emergency.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary scheduler=sequential,greedy --scenario calm,working,emergency --out results/schedulers.csv
dotnet run --project src/TelemetryLink.Cli -- verify
```
Для порівняння типово однакові codec=huffman, packer=dp, classifier=majority, window=64, threshold=0.5, critical-weight=1 і channel-seed=425243. Змінюється лише планувальник.

## Перевірено
.NET SDK 10.0.100: збирання рішення без помилок і попереджень. 254 тести пройшли без пропусків; verify — 11/11.
Пройшли також незмінені приймальні перевірки викладача 1–8, 10–12 і крайні випадки C2. Case09, який потребує навчання MLP і macro-F1, ще не входить до поточного етапу.

Тести перевіряють спадання щільності, нічиї, стабільність, збереження перестановки та вхідних даних на 200 наборах, незмінність байтів зашифрованих пакетів, точне приймання після перестановки, усі три сценарії, обидва кодеки, обидва пакувальники dp/greedy, облік втрат і over-budget. Для каналу перевірені формула ймовірності BAD, відтворення за seed, невитрачання смуги при over-budget та витрачання смуги втраченими пакетами.

Фільтр усіх уже реалізованих частин і приймальних тестів без майбутнього класифікатора:
```bash
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests|FullyQualifiedName~HuffmanTests|FullyQualifiedName~CompressionPacketTests|FullyQualifiedName~PipelineBaselineTests|FullyQualifiedName~PackerTests|FullyQualifiedName~TransmitterTests|FullyQualifiedName~SchedulerTests|FullyQualifiedName~ChannelTests|FullyQualifiedName~SchedulerPipelineTests|(FullyQualifiedName~AcceptanceCaseTests&FullyQualifiedName!~Case09_)"
```
Повний dotnet test ще містить заглушки метрик класифікації, MLP і оптимізаторів; весь проєкт ще не завершено.

## Приклад для пояснення
Є два пакети по 10 байтів, цінності 1 і 9. Бюджету вистачає лише на один.
Sequential відправляє перший і доставляє цінність 1. Greedy ставить пакет цінності 9 першим і доставляє 9. Цей приклад перевіряється тестом LimitedBudget_ExampleDeliversHigherValueWithGreedy.
Це ілюстрація користі евристики, а не доказ оптимальності для довільного набору, втрат чи Q.

## Фактичне порівняння на відкритому seed
SCHEDULER-COMPARISON-S5.csv містить один вимір для кожної комбінації. У всіх сформовано ті самі 127 пакетів; порядок може змінювати, які з них вмістяться в бюджет і які саме потраплять під втрати.

| Сценарій | Планувальник | Доставлена цінність | Відновлено critical | R | Q прикладу |
|---|---|---:|---:|---:|---:|
| calm | sequential | 4500 | 300/300 | 1.0000 | 0.6741 |
| calm | greedy | 4500 | 300/300 | 1.0000 | 0.6757 |
| working | sequential | 4411 | 291/300 | 0.9700 | 0.6645 |
| working | greedy | 4404 | 291/300 | 0.9700 | 0.6644 |
| emergency | sequential | 1458 | 98/300 | 0.3267 | 0.5568 |
| emergency | greedy | 1679 | 114/300 | 0.3800 | 0.5746 |

На calm усі 3000 кадрів відновлені в обох випадках; різниця Q тут пояснюється часом обчислень. У working втрачено 3% critical за порогу 5%; жадібний порядок у цьому одиночному прогоні не дав виграшу. У emergency жадібний порядок підвищив доставлену цінність і відновив на 16 critical більше.

Аварійний поріг втрати critical досі не досягнуто: greedy втрачає 62% за дозволених 20%. Типовий emergency-прогін: 50 пакетів зайняли смугу, 10 втрачено, 77 не вмістилися у бюджет, 40 прийнято; відновлено 1109 кадрів і 114 critical, доставлені дані точні. Без --strict запуск завершується штатно й друкує попередження про поріг. Із --strict код повернення сигналізує про перевищення порогу.

Час, T та Q залежать від комп'ютера та запуску. Не можна трактувати невелику різницю Q в одному запуску як статистично доведений виграш. Greedy не гарантує глобальний максимум Q. Параметри не підганялися під розклад втрат; офіційна перевірка використовує приховані seed.

## Комміт
```bash
git add README.md src/TelemetryLink.Cli/CommandContext.cs src/TelemetryLink.Core/Scheduling/Schedulers.cs tests/TelemetryLink.Tests/TestsToWrite.cs tests/TelemetryLink.Tests/SchedulerTests.cs tests/TelemetryLink.Tests/ChannelTests.cs tests/TelemetryLink.Tests/SchedulerPipelineTests.cs SCHEDULER-COMPARISON-S5.csv STEP-07.md
git commit -m "feat: add value-density packet scheduling"
```

## Далі
S6 — порівняння й звітність: перевірка експериментальних команд, результати та відтворюваність вимірювань. Далі — класифікатор MLP, оптимізація параметрів і фінальні таблиці звіту.
