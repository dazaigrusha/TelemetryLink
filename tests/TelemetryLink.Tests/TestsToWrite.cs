namespace TelemetryLink.Tests;

/// <summary>
/// ПЕРЕЛІК ТЕСТІВ, ЯКІ ТРЕБА НАПИСАТИ.
///
/// Кожна заготовка — це один сценарій. Знімайте Skip, реалізуйте тіло,
/// за потреби розносьте по окремих файлах (HuffmanTests.cs, PackerTests.cs
/// і так далі) — структура тестів має дозволяти швидко знайти потрібний тип
/// перевірок.
///
/// Готові приклади стилю — у HuffmanTests.cs.
/// Готові приймальні перевірки — у ReferencePacketTests.cs і AcceptanceCaseTests.cs;
/// їх переписувати не треба, вони і є цільовим станом системи.
///
/// Категорії позначені в назві файла-групи:
///   [С] стандартний   [К] крайній   [Х] хибний   [І] інваріант
/// </summary>
public class OptimizerTests
{
    [Fact(Skip = "TODO(S8) [І] кейс D1 №16: елітизм не дає погіршення")] public void Genetic_BestOfLastGeneration_NeverWorseThanGenerationZero() { }
    [Fact(Skip = "TODO(S8) [І]: обидва оптимізатори витрачають однаковий бюджет")] public void BothOptimizers_UseExactlySameEvaluationBudget() { }
    [Fact(Skip = "TODO(S8) [І]: той самий seed → той самий результат")] public void SameSeed_ProducesSameResult() { }
    [Fact(Skip = "TODO(S8) [С]: турнір частіше обирає кращу особину")] public void Tournament_PrefersHigherFitness() { }
    [Fact(Skip = "TODO(S8) [С]: гени нащадка взяті в батьків")] public void Crossover_ChildGenesComeFromParents() { }
    [Fact(Skip = "TODO(S8) [К]: нульова ймовірність мутації нічого не змінює")] public void Mutation_ZeroProbability_LeavesChromosomeUnchanged() { }
    [Fact(Skip = "TODO(S8) [І]: мутація не виводить гени за межі")] public void Mutation_KeepsGenesWithinBounds() { }
    [Fact(Skip = "TODO(S8) [К]: бюджет менший за популяцію")] public void Genetic_BudgetSmallerThanPopulation_DoesNotThrow() { }
}
