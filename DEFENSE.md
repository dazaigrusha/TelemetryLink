# Пояснення для захисту TelemetryLink

## Що робить програма

Передає телеметрію через обмежений канал: обирає важливі кадри, стискає, шифрує та підписує пакети. Приймач перевіряє справжність і відновлює доставлені кадри. Частина пакетів може загубитися або не вміститися в часовий бюджет — це модель каналу.

## Де шукати код

| Частина | Файл від кореня проєкту |
|---|---|
| Поля кадру, 12 байтів, big-endian | `src/TelemetryLink.Core/Domain/TelemetryFrame.cs` |
| Читання CSV/BIN | `src/TelemetryLink.Core/InputOutput/TelemetryReader.cs` |
| Дерево Гаффмана | `src/TelemetryLink.Core/Compression/HuffmanTreeBuilder.cs` |
| Кодування/декодування Гаффмана | `src/TelemetryLink.Core/Compression/HuffmanCodec.cs` |
| Канонічні коди | `src/TelemetryLink.Core/Format/CanonicalCodes.cs` |
| Обмеження довжин кодів | `src/TelemetryLink.Core/Compression/CodeLengthLimiter.cs` |
| Рюкзак DP | `src/TelemetryLink.Core/Packing/DynamicProgrammingPacker.cs` |
| Рюкзак greedy | `src/TelemetryLink.Core/Packing/GreedyRatioPacker.cs` |
| Планувальники | `src/TelemetryLink.Core/Scheduling/Schedulers.cs` |
| Формування пакетів і цінність кадрів | `src/TelemetryLink.Core/Pipeline/Transmitter.cs` |
| Наскрізний прогін | `src/TelemetryLink.Core/Pipeline/SessionRunner.cs` |
| Ознаки з історії | `src/TelemetryLink.Core/Prediction/FeatureExtractor.cs` |
| Scaler та застосування моделі | `src/TelemetryLink.Core/Prediction/PerceptronClassifier.cs` |
| Власна нейромережа і градієнти | `src/TelemetryLink.Core/Prediction/MultilayerPerceptron.cs` |
| Навчання та вибір моделі | `src/TelemetryLink.Core/Prediction/PerceptronTrainer.cs` |
| Macro-F1 та матриця | `src/TelemetryLink.Core/Metrics/ClassificationReport.cs` |
| PBKDF2 і два ключі | `src/TelemetryLink.Core/Security/SessionKeys.cs` |
| AES-CTR | `src/TelemetryLink.Core/Security/PacketCipher.cs` |
| HMAC | `src/TelemetryLink.Core/Security/PacketAuthenticator.cs` |
| Формат, порядок перевірок | `src/TelemetryLink.Core/Format/PacketCodec.cs` |
| Метрики S/R/T/Q | `src/TelemetryLink.Core/Metrics/SessionReport.cs` |
| Порядок стиснення/шифрування | `src/TelemetryLink.Core/Metrics/OrderOfOperationsExperiment.cs` |
| Три гени та межі | `src/TelemetryLink.Core/Configuration/Tuning.cs` |
| Random search | `src/TelemetryLink.Core/Optimization/RandomSearchOptimizer.cs` |
| Турнір, кросовер, мутація, елітизм | `src/TelemetryLink.Core/Optimization/GeneticOptimizer.cs` |
| Команди CLI | `src/TelemetryLink.Cli/Commands/` |

## Короткі відповіді

**Чому спочатку стиснення?** Повторювані байти мають короткі коди Гаффмана. Шифрування приховує ці закономірності, тому стиснення шифротексту майже не допомагає, а таблиця займає місце.

**Що означає канонічний код?** Код можна відновити за довжинами та порядком символів. Не потрібно передавати все дерево.

**Навіщо рюкзак?** Пакет має обмежений розмір. Треба обрати кадри з найбільшою сумарною цінністю. DP оптимальний для конкретних кандидатів, ваг і місткості; це не гарантія найкращої Q цілого сеансу.

**Чому greedy іноді кращий за DP у таблиці Q?** Q враховує час, накладні витрати пакета, порядок передавання та втрати. Це інша задача, ніж максимізація цінності одного фіксованого рюкзака.

**Навіщо MLP?** Класифікує кадри як норма, аномалія або перехідний стан. Клас впливає на цінність кадру. Ознаки враховують лише поточне та минуле, без майбутніх кадрів.

**Чому macro-F1, а не accuracy?** Нормальних кадрів найбільше. Постійно вгадуючи норму, можна мати велику accuracy, але пропускати інші класи. Macro-F1 дає кожному класу однакову вагу.

**Як уникнули витоку TEST?** Поділ TRAIN 80/20 у часовому порядку, scaler і балансування лише за 80%. Модель обрана за внутрішньою валідацією серед трьох ініціалізацій. Відкритий TEST оцінюється після відбору. У S9 використовується вже збережена модель.

**Навіщо два ключі?** Один для AES, другий для HMAC. Їх отримують одним PBKDF2 на сеанс і розділяють.

**Що захищає HMAC?** Від непомітної зміни пакета. Саме шифрування CTR не захищає від зміни байтів. Приймач перевіряє HMAC перед розшифруванням.

**З чого починається лічильник CTR?** З 1. Nonce має бути унікальним у межах ключа.

**Як працює GA?** Популяція параметрів → турнір батьків → кросовер → мутація → оцінка. Найкращі особини переходять без змін і повторної оцінки.

**Як забезпечено чесний бюджет?** Обидва алгоритми викликають fitness рівно budget разів. Це перевірено тестами для різних бюджетів, у тому числі менших за популяцію.

**Чи мусить GA вигравати?** Ні. Простір тут невеликий, а реальна Q має часовий шум. За тим самим бюджетом random може знайти кращу точку.

**Що означає ExactRestore=true?** Всі доставлені пакети відновлені точно. Це не означає, що канал доставив усі пакети.

**Чи виконані всі пороги?** Програмні тести та еталони проходять, відкрита F1 перевищує мінімум. Аварійний сценарій у результатах перевищує поріг втрат critical. Це обмеження поточної стратегії, а не прихований успішний результат.

## Що показати викладачу

1. `dotnet test tests/TelemetryLink.Tests -m:1` — усі тести.
2. `dotnet run --project src/TelemetryLink.Cli -- verify` — 11/11.
3. `run --scenario calm` — приклад наскрізного прогону.
4. `experiment --name order --out results/order.csv` — порядок операцій.
5. `RESULTS-S9.md` — п'ять таблиць і обмеження; `EXPERIMENTS-S7/mlp.training.json` — F1.
6. `OptimizerTests.cs` — точний бюджет та елітизм; `PerceptronTests.cs` — числовий градієнт.
