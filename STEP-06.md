# Частина 6 — S4: задача рюкзака

Основа — TelemetryLink_part05_Pipeline.zip, .NET 10, TelemetryLink.slnx.
Архів містить повний проєкт. Для оновлення репозиторію копіюйте файли зі списків нижче; власну .git зберігайте.

## Реалізовано
- BruteForcePacker: повний перебір усіх підмножин до 20 кандидатів. Це точний еталон для перевірки ДП, не основний метод для 64 кадрів.
- DynamicProgrammingPacker: точний рюкзак 0/1, зворотний прохід місткостей і відновлення набору taken[row][capacity]. Усі повернуті індекси зростають, кадр не використовується двічі.
- GreedyRatioPacker: евристика за цінністю/вагою, точне порівняння перехресним множенням long, детерміновані нічиї. Завеликий кадр пропускається, а не зупиняє перебір решти.
- PackerInput: перевірка невід'ємних ваг і цінностей для трьох нових алгоритмів.
- Великі суми цінностей рахуються у long. ДП враховує спільний дільник ваг, щоб зменшити місткість без втрати точності, а коли все вміщується — повертає набір одразу.
- Пакувальник за замовчуванням у CLI тепер dp; планувальник залишається sequential, класифікатор — majority.

Тракт, формат, шифрування і модель каналу не потребували змін: алгоритми вже підключаються через IFramePacker та AlgorithmCatalog.
Контракт повертає позиції у списку candidates, а не значення FrameIndex.
При місткості <= 0 повертається порожній набір. За додатної місткості нульова вага з додатною цінністю допустима: такий предмет можна взяти лише один раз. Нульова цінність не потребує включення до оптимального набору.

## Змінено
- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Core/Packing/BruteForcePacker.cs`
- `src/TelemetryLink.Core/Packing/DynamicProgrammingPacker.cs`
- `src/TelemetryLink.Core/Packing/GreedyRatioPacker.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Додано
- `PACKER-COMPARISON-S4.csv`
- `PACKER-SMALL-WINDOW-S4.csv`
- `src/TelemetryLink.Core/Packing/PackerInput.cs`
- `tests/TelemetryLink.Tests/PackerTests.cs`
- `tests/TelemetryLink.Tests/TransmitterTests.cs`
- `STEP-06.md`

Файлів не видалено. З TestsToWrite.cs прибрано тільки перенесені у повні реалізації групи PackerTests і TransmitterTests. Дані, канал та готові приймальні тести залишилися незмінними.

## Запуск із кореня рішення
```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- run --scenario calm
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --packer dp
dotnet run --project src/TelemetryLink.Cli -- run --scenario working --packer greedy
dotnet run --project src/TelemetryLink.Cli -- compare --vary packer=sequential,greedy,dp --scenario calm,working,emergency --out results/packers.csv
dotnet run --project src/TelemetryLink.Cli -- compare --vary packer=brute,dp,greedy --window 8 --scenario calm --out results/small_window.csv
dotnet run --project src/TelemetryLink.Cli -- verify
```
Для brute потрібне --window <= 20; на типовому вікні 64 повертається зрозуміла помилка, а не зависання. Рекомендується 8–16 для демонстрації.
Для чесного порівняння brute з іншими задавайте ОДНАКОВЕ вікно всім алгоритмам.

## Перевірено
SDK 10.0.100: збирання без помилок і попереджень. 211 тестів поточних частин пройшли без пропусків; verify — 11/11.
- 200 відтворюваних малих наборів: ДП має ту саму оптимальну сумарну цінність, що й перебір.
- 500 наборів: цінність ДП не нижча за жадібну. Окремо проходить незмінений приймальний Case08 викладача.
- Всі набори вкладаються у місткість, індекси унікальні й зростають.
- Порожні дані, нульова/від'ємна місткість, завеликий предмет, нульові ваги, великі int-значення, майже однакові відношення та детермінованість.
- Пакування, HMAC та побайтове відновлення із новими алгоритмами, порожній/однокадровий сеанс, сирий fallback при малому ліміті, усі три сценарії для dp/greedy.

```bash
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests|FullyQualifiedName~HuffmanTests|FullyQualifiedName~CompressionPacketTests|FullyQualifiedName~PipelineBaselineTests|FullyQualifiedName~PackerTests|FullyQualifiedName~TransmitterTests|FullyQualifiedName~Case08_DynamicProgramming|FullyQualifiedName~EdgeCase4_ZeroCapacity"
```
Повний dotnet test ще містить наступні незавершені етапи. Приймальні тести з жадібним планувальником не переписувалися під sequential; планувальник буде наступною частиною.

## Приклад для пояснення на захисті
Місткість 50:

| Індекс | Вага | Цінність | Цінність/вага | Жадібний | ДП |
|---|---:|---:|---:|---|---|
| 0 | 10 | 60 | 6 | так | ні |
| 1 | 20 | 100 | 5 | так | так |
| 2 | 30 | 120 | 4 | ні | так |

Жадібний бере 0 і 1: вага 30, цінність 160. Предмет 2 у залишок вже не вміщується.
ДП та перебір беруть 1 і 2: вага 50, цінність 220.
Саме цей приклад перевіряється тестом Greedy_IsSuboptimal_OnKnownCounterexample.

## Вимірювання на повному сеансі
PACKER-COMPARISON-S4.csv: вікно 64, поріг 0.5, однакові codec=huffman, scheduler=sequential, classifier=majority та seed 425243.

| Сценарій | Q sequential | Q greedy | Q dp |
|---|---:|---:|---:|
| calm | 0.6909 | 0.6789 | 0.6754 |
| working | 0.6848 | 0.6680 | 0.6644 |
| emergency | 0.5684 | 0.5608 | 0.5566 |

Це фактичні результати, а не цільові константи. Час, T та Q залежать від комп'ютера й запуску. Зафіксована таблиця містить один вимір, не усереднення.
На calm усі алгоритми відновили всі 3000 кадрів. Для dp сформовано 127 пакетів, для sequential — 111: локальний вибір кадрів змінює наступні вікна й витрати на таблиці. Тому краща цінність одного рюкзака не гарантує кращого Q всього сеансу. Порівнювати твердження «DP >= greedy» можна на ТОЧНО ОДНАКОВИХ кандидатах та місткості; після різних виборів подальші вікна вже різні.

PACKER-SMALL-WINDOW-S4.csv: brute, dp та greedy на однаковому вікні 8. У цьому прогоні всі відновили всі кадри, мали однакові передані біти; невелика різниця Q спричинена часом обчислень.
Аварійний поріг втрати critical ще не досягнуто. Наступні кроки — планувальник, класифікація та підбір параметрів; алгоритми не підганяються під відкритий розклад втрат.

## Комміт після копіювання файлів
```bash
git add README.md src/TelemetryLink.Cli/CommandContext.cs src/TelemetryLink.Core/Packing/BruteForcePacker.cs src/TelemetryLink.Core/Packing/DynamicProgrammingPacker.cs src/TelemetryLink.Core/Packing/GreedyRatioPacker.cs tests/TelemetryLink.Tests/TestsToWrite.cs PACKER-COMPARISON-S4.csv PACKER-SMALL-WINDOW-S4.csv src/TelemetryLink.Core/Packing/PackerInput.cs tests/TelemetryLink.Tests/PackerTests.cs tests/TelemetryLink.Tests/TransmitterTests.cs STEP-06.md
git commit -m "feat: add exact and greedy knapsack packers"
```

## Далі
S5 — жадібний планувальник пакетів за цінністю на байт, перевірка порядку й бюджету, порівняння сценаріїв. Навчання MLP, оптимізатори та фінальні експерименти залишаються наступними етапами.
