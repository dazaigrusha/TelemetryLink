# Частина 10 — S8: оптимізація параметрів

Повний проєкт поверх частини 9. .NET 10 SDK, TelemetryLink.slnx. Всі шляхи нижче від кореня TelemetryLink. Видалених файлів немає.

## Реалізація

Три гени: WindowFrames (ціле 8–64), CompressionThreshold (0.3–1.6), CriticalWeight (0–3). Випадковий пошук рівномірно обирає ціле вікно та неперервні інші гени. Генетичний алгоритм: випадкова початкова популяція, турнір з поверненням, незалежний вибір кожного гена одного з батьків, мутація рівномірним зсувом на частку діапазону з обмеженням меж та елітизм. Мінімум одна еліта; для популяцій >1 еліт має бути менше за розмір популяції.

Обидва алгоритми викликають fitness РІВНО budget разів. Еліти не оцінюються знову. Початкова популяція обмежується бюджетом. Останнє покоління може бути неповним — збережені еліти лишаються в ньому. Для популяції 1 застосована схема (1+1): один нащадок за оцінку, заміна лише при поліпшенні.

Fitness — Q повного тракту, з однаковими сесією, сценарієм, алгоритмами та channel-seed. Модель MLP завантажується один раз; параметри нейромережі й її навчання не оптимізуються. Дані TRAIN/valid не використовуються для підбору цих трьох параметрів; використовується session.csv. Результат підбору на цій сесії не є оцінкою узагальнення на інших даних.

## Запуск

Команди виконувати там, де лежить TelemetryLink.slnx:

```bash
dotnet build TelemetryLink.slnx -m:1
dotnet test tests/TelemetryLink.Tests -m:1
dotnet run --project src/TelemetryLink.Cli -- verify
dotnet run --project src/TelemetryLink.Cli -- optimize --optimizer random,ga --budget 200 --seed 1 --scenario emergency --classifier mlp --model EXPERIMENTS-S7/mlp.json --out results/optimization.csv
dotnet run --project src/TelemetryLink.Cli -- run --scenario emergency --classifier mlp --model EXPERIMENTS-S7/mlp.json --tuning EXPERIMENTS-S8/optimization.ga.best.json
```

Без параметрів MLP працюватиме базовий majority. Типово budget=60, seed=1, population=12, tournament=3, mutation=0.2, mutation-scale=0.15, elite=1. Параметри GA: `--population`, `--tournament`, `--mutation`, `--mutation-scale`, `--elite`. Вхідні файли захищені від перезапису вихідними файлами.

## Результати та їх трактування

Збережений приклад: аварійний сценарій, MLP з частини 9, dp/greedy/huffman, seed=1; channel-seed з конфігурації. По 200 оцінок кожному:

| Алгоритм | Найкращий Q | Вікно | Поріг | Вага critical | Оцінок |
|---|---:|---:|---:|---:|---:|
| random | 0.626225 | 41 | 0.703122 | 2.935309 | 200 |
| ga | 0.625362 | 40 | 0.300000 | 2.244039 | 200 |

У цьому запуску випадковий пошук виграв. Немає вимоги або гарантії, що GA кращий. Q включає виміряний час, тому порядок запусків, прогрів і навантаження впливають на оцінки. Один seed і одна сесія не доводять загальної переваги. Детермінованість алгоритмів перевіряється на синтетичній детермінованій fitness; реальна Q шумна. Елітизм гарантує непогіршення збереженої оцінки, а не нового вимірювання часу.

CSV має стовпці optimizer,generation,evaluations,best_fitness,mean_fitness. Для графіка збіжності використовуйте evaluations по осі X, best_fitness по Y: покоління GA і крок random мають різну вартість. Random mean — середнє всіх оцінених точок до кроку, GA mean — середнє поточної популяції включно з елітами; напряму ці середні порівнювати не слід.

Summary містить параметри, межі генів, історію, витрачений бюджет і SHA-256 входів/моделі. Конфігурація з паролем не копіюється. best.json містить лише параметри тракту. Через `--tuning` його можна підключити у run/compare; явні прапорці значень мають перевагу над файлом.

## Перевірки

336 тестів успішні, пропусків немає. Release build: 0 помилок/попереджень. verify: 11/11. Перевірено рівний бюджет (включно з 1, бюджетом меншим за популяцію та неповним поколінням), елітизм, відсутність переоцінки еліт, відтворюваність усіх кандидатів для чистої fitness, статистику турніру, кросовер, межі мутації, нульову мутацію, популяцію 1, негативні/нескінченні fitness, некоректні опції, експорт та повторне завантаження параметрів. Прогін із збереженим GA best.json виконаний без аварії, доставлені пакети відновлені точно. В аварійному сценарії втрата critical становила 48.3% при порозі 20% — поріг не виконано. Оптимізація максимізує Q без окремого жорсткого обмеження втрат; best означає найкращу знайдену Q, а не проходження всіх порогів. Дані, канал, приймальні й еталонні тести викладача не змінені.

## Додані файли

- `EXPERIMENTS-S8/optimization.csv`
- `EXPERIMENTS-S8/optimization.ga.best.json`
- `EXPERIMENTS-S8/optimization.random.best.json`
- `EXPERIMENTS-S8/optimization.summary.json`
- `tests/TelemetryLink.Tests/OptimizationCliTests.cs`
- `tests/TelemetryLink.Tests/OptimizerTests.cs`
- `STEP-10.md`

## Змінені файли

- `README.md`
- `src/TelemetryLink.Cli/CommandContext.cs`
- `src/TelemetryLink.Cli/Commands/OptimizeCommand.cs`
- `src/TelemetryLink.Cli/Program.cs`
- `src/TelemetryLink.Core/Configuration/Tuning.cs`
- `src/TelemetryLink.Core/Optimization/GeneticOptimizer.cs`
- `src/TelemetryLink.Core/Optimization/IParameterOptimizer.cs`
- `src/TelemetryLink.Core/Optimization/RandomSearchOptimizer.cs`
- `tests/TelemetryLink.Tests/TestsToWrite.cs`

## Комміт

```bash
git add src tests README.md STEP-10.md EXPERIMENTS-S8
git commit -m "feat: optimize telemetry parameters with random search and genetic algorithm"
```

Далі — S9: експеримент порядку операцій, фінальні таблиці та інструкція до захисту. Це ще не завершення всього проєкту.
