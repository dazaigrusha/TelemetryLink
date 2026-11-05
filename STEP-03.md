# Частина 3 — S2: захищений пакет

Повна копія після частини 2. Для свого репозиторію скопіюйте лише файли нижче; збережіть власну папку .git.

## Реалізовано
PBKDF2-HMAC-SHA256 (100000 ітерацій, 64 байти з розділенням ключів), AES-256-CTR з лічильником від 1, побудова nonce, HMAC-SHA256 з порівнянням у постійний час. Пакет складається із заголовка, шифротексту й HMAC. Приймач перевіряє HMAC до дешифрування та повертає коди відмови на поганих даних.

## Додано
- `tests/TelemetryLink.Tests/SecurityTests.cs`
- `tests/TelemetryLink.Tests/PacketTests.cs`
- `STEP-03.md`

## Змінено
- `src/TelemetryLink.Core/Security/SessionKeys.cs`
- `src/TelemetryLink.Core/Security/PacketCipher.cs`
- `src/TelemetryLink.Core/Security/PacketAuthenticator.cs`
- `src/TelemetryLink.Core/Format/PacketCodec.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

Готові ReferencePacketTests.cs та AcceptanceCaseTests.cs не змінені. У TestsToWrite.cs перенесено тільки реалізовані групи SecurityTests і PacketTests.

## Перевірено у .NET SDK 10.0.100
- Збирання рішення: 0 помилок, 0 попереджень.
- 97 тестів S0/S1/S2 та еталонних пакетів пройшли, 0 помилок.
- Команда verify: 11/11.
- inspect правильно показує еталонний ref_01.
- Усі шість пакетів перебудовуються побайтово однаково з оригіналами.
- Незалежна перевірка Python підтвердила HMAC, дешифрування та всі поля кадрів шести еталонів.
Повний dotnet test ще містить заглушки майбутніх алгоритмів.

## Запуск із кореня рішення
```bash
dotnet build -m:1
dotnet test tests/TelemetryLink.Tests/TelemetryLink.Tests.csproj --filter "FullyQualifiedName~TelemetryFrameTests|FullyQualifiedName~TelemetryReaderTests|FullyQualifiedName~BitIoTests|FullyQualifiedName~CanonicalCodesTests|FullyQualifiedName~PayloadTests|FullyQualifiedName~SecurityTests|FullyQualifiedName~PacketTests|FullyQualifiedName~ReferencePacketTests"
dotnet run --project src/TelemetryLink.Cli -- verify
dotnet run --project src/TelemetryLink.Cli -- inspect --packet data/reference/ref_01_single_frame.bin
```
-m:1 задає послідовне збирання проєктів: цей варіант перевірений у робочому середовищі.

## Комміт
```bash
git add src/TelemetryLink.Core/Security/SessionKeys.cs src/TelemetryLink.Core/Security/PacketCipher.cs src/TelemetryLink.Core/Security/PacketAuthenticator.cs src/TelemetryLink.Core/Format/PacketCodec.cs tests/TelemetryLink.Tests/TestsToWrite.cs tests/TelemetryLink.Tests/SecurityTests.cs tests/TelemetryLink.Tests/PacketTests.cs STEP-03.md
git commit -m "feat: implement AES-CTR authenticated packet format"
```
Наступна частина S3 — дерево Гаффмана та стиснення. Інтеграція, пакування, класифікація та оптимізація ще попереду.
