# Частина 1 — S0: читання і перевірка телеметрії

Це повна копія проєкту після першої частини. Подальші алгоритми залишаються заглушками.
Скопіюйте до свого репозиторію лише перелічені нижче файли, щоб зберегти власний .gitignore та інші налаштування.

## Додано
- tests/TelemetryLink.Tests/TelemetryFrameTests.cs
- tests/TelemetryLink.Tests/TelemetryReaderTests.cs
- STEP-01.md

## Змінено
- src/TelemetryLink.Core/InputOutput/TelemetryReader.cs — перевірка заголовка, кількості полів, чисел, міток класів і полів BIN.
- src/TelemetryLink.Cli/Commands/InspectCommand.cs — розподіл кадрів за mode.
- tests/TelemetryLink.Tests/TestsToWrite.cs — заготовки S0 перенесено в реалізовані тести.

## Перевірка
Запускати з кореня рішення:
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests"
dotnet run --project src/TelemetryLink.Cli -- inspect --session data/session.csv
dotnet run --project src/TelemetryLink.Cli -- inspect --session data/session.bin
```
Очікується 17 випадків тестів S0. Повний набір ще містить незавершені алгоритми.

Контрольні значення, незалежно пораховані за наданими даними:
Кадрів: 3000; критичних: 300; B_ref: 288000 біт.
Розподіл mode: {'0': 392, '1': 184, '2': 422, '3': 586, '4': 111, '5': 609, '6': 228, '7': 468}.
CSV і BIN побайтово відповідають один одному. BIN не містить міток label.

У середовищі підготовки немає .NET SDK: build і C#-тести не запускались.
Незалежна перевірка Python підтвердила контрольні значення й відповідність CSV/BIN.

## Комміт
```bash
git add src/TelemetryLink.Core/InputOutput/TelemetryReader.cs src/TelemetryLink.Cli/Commands/InspectCommand.cs tests/TelemetryLink.Tests/TestsToWrite.cs tests/TelemetryLink.Tests/TelemetryFrameTests.cs tests/TelemetryLink.Tests/TelemetryReaderTests.cs STEP-01.md
git commit -m "feat: validate telemetry input and complete S0 checks"
```
Наступна частина: S1 — бітове читання/запис та формат без стиснення.
