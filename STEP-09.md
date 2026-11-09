# Частина 9 — S7: MLP та macro-F1

Це повний проєкт поверх частини 8, а не лише патч. Потрібен .NET 10 SDK; адаптація під VS 2022 сюди не включена.

## Що зроблено

- Причинні ознаки: зміни висоти/температури, вібрація, статистика історії, зміни mode, дрейф висоти, critical. Мітки label до ознак не входять.
- Власний MLP: ReLU, стабільний softmax, cross-entropy, backprop та SGD. Підтримка лінійної моделі через `--hidden none`.
- TRAIN 80/20 у часовому порядку; scaler лише з префікса. Рівна кількість семплів на кожен наявний клас із поверненням, тільки з навчального префікса. Крок SGD зменшується за формулою `lr/(1+0.15*(epoch-1))`.
- Мінімум 3 ініціалізації. Для кожної зберігається найкраща епоха за внутрішнім macro-F1, потім найкраща модель серед ініціалізацій. За рівності залишається раніша. Без повторного навчання на всьому TRAIN після відбору.
- Open TEST читається після відбору. Його оцінка не впливає на вибір. Немає заяви про результат на прихованому TEST викладача.
- Модель містить ваги, scaler, схему ознак, історію вікна й seed. Окремий звіт містить SHA-256 входів і моделі, параметри, історію та оцінку. Паролі конфігурації у звіт не копіюються.

## Результати

Збережена модель 10–32–16–3, seed=2, epoch=3. Internal macro-F1 0.8989. Open TEST: macro-F1 0.8778, accuracy 93.40%, majority macro-F1 0.2922; поріг 0.58 пройдено. Вибір моделі до і після оцінки TEST дає ідентичний файл моделі.

Матриця: рядки — істина, стовпці — прогноз [норма, аномалія, перехідний]:

| Істина | Норма | Аномалія | Перехідний |
|---|---:|---:|---:|
| Норма | 1491 | 5 | 65 |
| Аномалія | 2 | 190 | 10 |
| Перехідний | 33 | 17 | 187 |

`EXPERIMENTS-S7/classifiers.*` порівнює majority/MLP на трьох сценаріях, по 3 повтори після прогріву. MLP не покращив Q у цьому запуску. Ці метрики оцінюють різні речі; час і Q залежать від комп'ютера.

## Перевірка

Release build: 0 попереджень, 0 помилок. 308 реалізованих тестів успішні, включно з незміненим приймальним Case09. 8 шаблонів OptimizerTests залишені до S8. Перевірено числовий градієнт усіх параметрів маленької мережі з двома прихованими шарами, SGD-оновлення, XOR, лінійні дані, стабільність softmax, причинність, scaler, серіалізацію, внутрішній відбір і CLI-звіти. `verify`: 11/11. Дані викладача, реалізація каналу та приймальні/еталонні тести не змінені.

## Використання

Команди запускати в каталозі, де TelemetryLink.slnx:

```bash
dotnet build TelemetryLink.slnx -m:1
dotnet run --project src/TelemetryLink.Cli -- train --out results/mlp.json
dotnet run --project src/TelemetryLink.Cli -- run --classifier mlp --model EXPERIMENTS-S7/mlp.json
dotnet run --project src/TelemetryLink.Cli -- compare --vary classifier=majority,mlp --model EXPERIMENTS-S7/mlp.json --out results/classifiers.csv
dotnet test tests/TelemetryLink.Tests -m:1 --filter "FullyQualifiedName!~OptimizerTests"
```

Для внутрішнього експерименту без оцінки TEST: `train --no-test --out results/internal.json`. Не перебирайте параметри за результатом valid.csv. Для train `--window` означає історію ознак; для run — вікно пакування. Параметри моделі та scaler для run завантажуються з її JSON.

## Додано

- `EXPERIMENTS-S7/classifiers.csv`
- `EXPERIMENTS-S7/classifiers.manifest.json`
- `EXPERIMENTS-S7/classifiers.summary.csv`
- `EXPERIMENTS-S7/mlp.history.csv`
- `EXPERIMENTS-S7/mlp.json`
- `EXPERIMENTS-S7/mlp.training.json`
- `src/TelemetryLink.Cli/TrainingArtifacts.cs`
- `tests/TelemetryLink.Tests/MetricsTests.cs`
- `tests/TelemetryLink.Tests/PerceptronTests.cs`
- `tests/TelemetryLink.Tests/TrainingCliTests.cs`
- `STEP-09.md`

## Змінено

- `README.md`
- `src/TelemetryLink.Cli/Commands/CompareCommand.cs`
- `src/TelemetryLink.Cli/Commands/TrainCommand.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Core/Metrics/ClassificationReport.cs`
- `src/TelemetryLink.Core/Prediction/FeatureExtractor.cs`
- `src/TelemetryLink.Core/Prediction/MultilayerPerceptron.cs`
- `src/TelemetryLink.Core/Prediction/PerceptronClassifier.cs`
- `src/TelemetryLink.Core/Prediction/PerceptronTrainer.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Комміт

```bash
git add src tests README.md STEP-09.md EXPERIMENTS-S7
git commit -m "feat: train and evaluate telemetry MLP classifier"
```

Далі — S8: random search та генетичний алгоритм з однаковим бюджетом оцінок.
