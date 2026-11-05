# Частина 2 — S1: біти та навантаження

Повна копія проєкту після частини 1. Для оновлення власного Git скопіюйте лише файли нижче.

## Реалізовано
- MSB-first запис і читання бітів, нульове доповнення, безпечний повторний ToArray.
- Запис і суворий розбір навантаження: big-endian, межі секцій, прапорці, таблиця, кількість символів, поля кадрів і класи.
- Режим NO_COMPRESSION: відновлення 0, 1 і 64 кадрів; готовий NoCompressionCodec використовується без змін.
- Канонічне призначення кодів і декодування реалізовано раніше S3, бо CodeTable.Empty вже викликає CanonicalCodes.Assign.
- Захист від перенасиченої таблиці кодів, повторних символів і зміни зовнішнього масиву довжин.

Побудова дерева Гаффмана, криптографія та повний тракт ще не реалізовані.
FORMAT_SPEC.md у матеріалах відсутній: розкладку взято з коментарів шаблону; class_table — один байт на кадр, як у JSON ref_06.

## Додано
- `tests/TelemetryLink.Tests/BitIoTests.cs`
- `tests/TelemetryLink.Tests/CanonicalCodesTests.cs`
- `tests/TelemetryLink.Tests/PayloadTests.cs`
- `STEP-02.md`

## Змінено
- `src/TelemetryLink.Core/Format/BitReader.cs`
- `src/TelemetryLink.Core/Format/BitWriter.cs`
- `src/TelemetryLink.Core/Format/CanonicalCodes.cs`
- `src/TelemetryLink.Core/Format/CanonicalDecoder.cs`
- `src/TelemetryLink.Core/Format/CodeTable.cs`
- `src/TelemetryLink.Core/Format/Payload.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Запуск
З кореня, де TelemetryLink.slnx:
```bash
dotnet build
dotnet test --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests"
dotnet run --project src/TelemetryLink.Cli -- inspect --session data/session.csv
```
Повний dotnet test і verify ще мають незавершені частини. Усі додані перевірки S0/S1 повинні пройти.
У цьому середовищі .NET SDK відсутній: C# build і тести не запускались.
Незалежна перевірка Python підтвердила канонічні коди всіх шести еталонних JSON та контрольний HEX навантаження одного кадру.

## Комміт
```bash
git add src/TelemetryLink.Core/Format/BitReader.cs src/TelemetryLink.Core/Format/BitWriter.cs src/TelemetryLink.Core/Format/CanonicalCodes.cs src/TelemetryLink.Core/Format/CanonicalDecoder.cs src/TelemetryLink.Core/Format/CodeTable.cs src/TelemetryLink.Core/Format/Payload.cs tests/TelemetryLink.Tests/TestsToWrite.cs tests/TelemetryLink.Tests/BitIoTests.cs tests/TelemetryLink.Tests/CanonicalCodesTests.cs tests/TelemetryLink.Tests/PayloadTests.cs STEP-02.md
git commit -m "feat: implement bit IO and telemetry payload format"
```
Наступна частина S2: PBKDF2, AES-256-CTR, HMAC та зовнішній формат пакета.
