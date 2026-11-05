# Частина 4 — S3a: дерево Гаффмана та стиснення

Основа — TelemetryLink_part03_S2.zip (.NET 10, TelemetryLink.slnx), до адаптації під VS 2022.
Архів містить повний проєкт. Відносно S2 переносіть лише файли зі списків нижче.

## Реалізовано
- Підрахунок частот 256 байтів та побудова дерева через PriorityQueue.
- Детерміновані нічиї: (частота, номер вставки), листки додаються за зростанням символу.
- Порожня таблиця для порожнього вводу; один символ має код 0 довжини 1.
- Довжини до 15 біт: повторне згладжування частот зі строгою перевіркою результату; жодні символи не зливаються і не зникають.
- long для внутрішніх ваг: суми великих частот не переповнюють int.
- Кодування канонічними кодами MSB-first; декодування використовує реалізацію з S1.
- Команда compress для вимірювання кодека й побайтової перевірки відновлення.

Згладжування — дозволений планом базовий метод, а не оптимальний алгоритм package-merge. Повторне підняття лише незмінної межі max >> 14 могло б не дати прогресу: тому мінімальна додатна частота також подвоюється, а дерево перевіряється після кожного кроку (до 64 спроб).

## Змінено
- `README.md`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Core/Compression/CodeLengthLimiter.cs`
- `src/TelemetryLink.Core/Compression/HuffmanCodec.cs`
- `src/TelemetryLink.Core/Compression/HuffmanTreeBuilder.cs`
- `tests/TelemetryLink.Tests/HuffmanTests.cs`

## Додано
- `src/TelemetryLink.Cli/Commands/CompressCommand.cs`
- `tests/TelemetryLink.Tests/CompressionPacketTests.cs`
- `STEP-04.md`

Файлів не видалено. Дані викладача, модель каналу та готові приймальні тести не змінено.

## Запуск із кореня рішення
Потрібен .NET 10 SDK.
```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- compress
dotnet run --project src/TelemetryLink.Cli -- compress --session data/session.bin --out compression.json
dotnet run --project src/TelemetryLink.Cli -- verify
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests|FullyQualifiedName~HuffmanTests|FullyQualifiedName~CompressionPacketTests"
```
--out зберігає JSON зі статистикою. Це звіт, а не файл зашифрованого пакета.

## Результат перевірки
SDK 10.0.100: збирання без помилок і попереджень. 120 тестів реалізованих частин пройшли, без пропусків. verify: 11/11.
Тести включають 100 випадкових наборів, повний алфавіт, частоти Фібоначчі, int.MaxValue, відсутній символ, відомий оптимальний приклад і відповідність таблиць еталонам. Стиснені й зашифровані пакети з 0, 1, 64 кадрами відновлюються побайтово.

На session.csv:
- 3000 кадрів; сирі дані: 36000 байтів.
- Закодований потік: 192496 бітів / 24062 байти.
- Таблиця: 512 байтів; фіксовані поля: 14 байтів.
- Повне навантаження: 24588 байтів замість 36014 байтів у сирому режимі.
- Економія навантаження: 11426 байтів; відновлення точне.

Це вимірювання кодека на всьому сеансі як на одному навантаженні. Обмежені пакети, вікна, канал і метрика Q тут ще не виконуються. Воно не є оцінкою результату повного передавання. Повний S3 ще не завершений: наступна частина — початковий наскрізний тракт, послідовне пакування та базова метрика Q. Повний dotnet test поки містить незавершені наступні етапи.

## Якщо вже застосовували адаптацію VS 2022
Для повернення до цієї гілки .NET 10 найпростіше взяти повний проєкт із архіву. Збережіть власну папку .git.
Крім змін S3a, відновіть з цього архіву три .csproj, Format/PacketHeader.cs, Cli/CommandContext.cs, .gitignore і TelemetryLink.slnx. Видаліть файли адаптації TelemetryLink.sln, global.json, VS2022.md та src/TelemetryLink.Cli/Properties/launchSettings.json. Не додавайте старі bin/obj у Git.

## Комміт відносно попереднього S2 на .NET 10
```bash
git add README.md src/TelemetryLink.Cli/Program.cs src/TelemetryLink.Core/Compression/CodeLengthLimiter.cs src/TelemetryLink.Core/Compression/HuffmanCodec.cs src/TelemetryLink.Core/Compression/HuffmanTreeBuilder.cs tests/TelemetryLink.Tests/HuffmanTests.cs src/TelemetryLink.Cli/Commands/CompressCommand.cs tests/TelemetryLink.Tests/CompressionPacketTests.cs STEP-04.md
git commit -m "feat: implement deterministic Huffman compression"
```
Якщо комміт адаптації VS 2022 вже є у вашій історії, повернення до .NET 10 також має ввійти до комміту: перед фіксацією перевірте git status та додайте відновлені й видалені файли.
