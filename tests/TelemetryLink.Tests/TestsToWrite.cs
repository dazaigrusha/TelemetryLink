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
public class PacketTests
{
    [Fact(Skip = "TODO(S2) [С]: пакет проходить туди й назад побайтово")] public void BuildThenRead_RestoresFramesByteForByte() { }
    [Fact(Skip = "TODO(S2) [С]: режим NO_COMPRESSION")] public void BuildThenRead_WorksInNoCompressionMode() { }
    [Fact(Skip = "TODO(S2) [К]: пакет 74 байти → E_SHORT")] public void PacketShorterThan75Bytes_ReturnsEShort() { }
    [Fact(Skip = "TODO(S2) [К]: порожній вхід не кидає виняток")] public void EmptyInput_ReturnsEShort_WithoutThrowing() { }
    [Fact(Skip = "TODO(S2) [Х]: змінений байт шифротексту → E_HMAC (кейс 10)")] public void TamperedCiphertextByte_ReturnsEHmac() { }
    [Fact(Skip = "TODO(S2) [Х]: змінений тег → E_HMAC (кейс 10)")] public void TamperedTag_ReturnsEHmac() { }
    [Fact(Skip = "TODO(S2) [Х]: обрізаний пакет → E_LENGTH (кейс 11)")] public void TruncatedPacket_ReturnsELength() { }
    [Fact(Skip = "TODO(S2) [Х]: зіпсований magic → E_MAGIC")] public void BadMagic_ReturnsEMagic() { }
    [Fact(Skip = "TODO(S2) [Х]: невірний пароль → E_HMAC і ЖОДНОГО кадру (кейс 11)")] public void WrongPassword_ReturnsEHmac_AndNoFrames() { }
}

public class SecurityTests
{
    [Fact(Skip = "TODO(S2) [С]: той самий пароль і сіль → ті самі ключі")] public void Derive_SamePasswordAndSalt_ProducesSameKeys() { }
    [Fact(Skip = "TODO(S2) [І]: K_enc != K_mac — пряме питання на захисті")] public void EncryptionKey_DiffersFromMacKey() { }
    [Fact(Skip = "TODO(S2) [К]: перший блок лічильника має номер 1, не 0")] public void CounterBlock_FirstBlockIndexIsOne() { }
    [Fact(Skip = "TODO(S2) [С]: Transform двічі повертає оригінал")] public void Transform_AppliedTwice_ReturnsOriginal() { }
    [Fact(Skip = "TODO(S2) [К]: CTR не змінює довжину, зокрема для 0, 15, 16, 17 байтів")] public void Transform_PreservesLength() { }
    [Fact(Skip = "TODO(S2) [І]: різні пакети — різний nonce")] public void BuildNonce_DiffersBetweenPackets() { }
    [Fact(Skip = "TODO(S2) [Х]: тег, порахований іншим ключем, не проходить")] public void Verify_TagFromDifferentKey_ReturnsFalse() { }
}

public class PackerTests
{
    [Fact(Skip = "TODO(S4) [І]: ДП збігається з повним перебором на 200 наборах")] public void DynamicProgramming_MatchesBruteForce() { }
    [Fact(Skip = "TODO(S4) [І]: ДП не гірше за жадібний (кейс C1 №8)")] public void DynamicProgramming_NeverWorseThanGreedy() { }
    [Fact(Skip = "TODO(S4) [І]: жоден пакувальник не перевищує місткість")] public void NoPacker_ExceedsCapacity() { }
    [Fact(Skip = "TODO(S4) [І]: індекси зростають і не повторюються")] public void Select_ReturnsIndexesInAscendingOrder() { }
    [Fact(Skip = "TODO(S4) [С]: конкретний набір, де жадібний програє — потрібен на захисті")] public void Greedy_IsSuboptimal_OnKnownCounterexample() { }
    [Fact(Skip = "TODO(S3) [С]: наївний зупиняється на важкому кадрі, жадібний його пропускає")] public void Sequential_StopsAtFirstFrameThatDoesNotFit() { }
    [Fact(Skip = "TODO(S4) [К]: порожній список кандидатів")] public void Select_EmptyCandidates_ReturnsEmpty() { }
    [Fact(Skip = "TODO(S4) [К]: місткість 0 і від'ємна")] public void Select_NonPositiveCapacity_ReturnsEmpty() { }
    [Fact(Skip = "TODO(S4) [К]: єдиний кадр важчий за місткість")] public void Select_SingleItemHeavierThanCapacity_ReturnsEmpty() { }
    [Fact(Skip = "TODO(S4) [К]: усе влазить → беремо все")] public void Select_AllItemsFit_ReturnsAll() { }
}

public class TransmitterTests
{
    [Fact(Skip = "TODO(S4) [І]: жоден кадр не загублено і не подвоєно")] public void BuildPackets_NoFrameIsLostOrDuplicated() { }
    [Fact(Skip = "TODO(S4) [І]: кожен пакет вкладається в max_packet_bytes")] public void BuildPackets_EveryPacketFitsMaxPacketBytes() { }
    [Fact(Skip = "TODO(S4) [С]: кожен пакет читається назад")] public void BuildPackets_EveryPacketRoundTrips() { }
    [Fact(Skip = "TODO(S4) [К]: порожній сеанс")] public void BuildPackets_EmptySession_DoesNotThrow() { }
    [Fact(Skip = "TODO(S4) [К]: один кадр")] public void BuildPackets_SingleFrame_ProducesOnePacket() { }
    [Fact(Skip = "TODO(S4) [К]: дуже малий ліміт пакета → сирий режим, а не падіння")] public void BuildPackets_TinyPacketLimit_FallsBackToRawMode() { }
}

public class SchedulerTests
{
    [Fact(Skip = "TODO(S5) [С]: послідовний зберігає порядок")] public void Sequential_PreservesInputOrder() { }
    [Fact(Skip = "TODO(S5) [С]: жадібний ставить найщільніший першим")] public void ValueDensity_PutsHighestDensityFirst() { }
    [Fact(Skip = "TODO(S5) [К]: порожній список")] public void Order_EmptyList_ReturnsEmpty() { }
    [Fact(Skip = "TODO(S5) [І]: результат — перестановка входу")] public void Order_IsPermutationOfInput() { }
}

public class ChannelTests
{
    [Fact(Skip = "TODO(S5) [К]: спокійний сценарій нічого не втрачає")] public void CalmScenario_LosesNothing() { }
    [Fact(Skip = "TODO(S5) [І]: той самий seed → той самий розклад втрат")] public void SameSeed_ProducesSameLossSequence() { }
    [Fact(Skip = "TODO(S5) [С]: p_gb = loss / (loss + B(1-loss)) = 0,0556 для аварійного")] public void BadStateProbability_UsesCorrectFormula() { }
    [Fact(Skip = "TODO(S5) [К]: пакет поза бюджетом не займає смугу")] public void PacketOverBudget_DoesNotConsumeBandwidth() { }
}

public class MetricsTests
{
    [Fact(Skip = "TODO(S6) [С]: Q — зважена сума компонент")] public void Q_EqualsWeightedSum_OfComponents() { }
    [Fact(Skip = "TODO(S6) [К]: жодного критичного кадру не відновлено → Q = 0")] public void Q_IsZero_WhenNoCriticalFrameRecovered() { }
    [Fact(Skip = "TODO(S6) [К]: у сеансі немає критичних кадрів → R = 1, не NaN")] public void R_NoCriticalFramesInSession_IsOne_NotNaN() { }
    [Fact(Skip = "TODO(S6) [І]: усі компоненти в [0,1]")] public void AllComponents_StayWithinZeroToOne() { }
    [Fact(Skip = "TODO(S7) [К]: ідеальний прогноз → macro-F1 = 1")] public void MacroF1_OfPerfectPrediction_IsOne() { }
    [Fact(Skip = "TODO(S7) [С]: ігнорування найменшого класу карається")] public void MacroF1_PunishesIgnoringSmallestClass() { }
    [Fact(Skip = "TODO(S7) [К]: клас відсутній у прогнозі → 0, а не NaN")] public void MacroF1_ClassAbsentFromPrediction_IsZero_NotNaN() { }
}

public class PerceptronTests
{
    [Fact(Skip = "TODO(S7) [С] НАЙВАЖЛИВІШИЙ: градієнт backprop проти числового")] public void Backpropagation_MatchesNumericalGradient() { }
    [Fact(Skip = "TODO(S7) [І]: softmax дає суму 1")] public void Forward_ReturnsProbabilitiesThatSumToOne() { }
    [Fact(Skip = "TODO(S7) [С]: лінійно роздільні дані вчаться до 100 %")] public void Train_OnLinearlySeparableData_ReachesFullAccuracy() { }
    [Fact(Skip = "TODO(S7) [С]: XOR потребує прихованого шару")] public void Train_OnXor_RequiresHiddenLayer() { }
    [Fact(Skip = "TODO(S7) [І]: той самий seed → ті самі ваги")] public void SameSeed_ProducesSameInitialWeights() { }
    [Fact(Skip = "TODO(S7) [С]: збереження і завантаження не змінюють прогноз")] public void ToJson_ThenFromJson_PreservesPredictions() { }
    [Fact(Skip = "TODO(S7) [К]: перший кадр — нульові різниці, не NaN")] public void FeatureExtractor_FirstFrame_HasZeroDeltas() { }
    [Fact(Skip = "TODO(S7) [К]: вікно довше за сеанс")] public void FeatureExtractor_WindowLongerThanSession_UsesAvailableFrames() { }
    [Fact(Skip = "TODO(S7) [К]: стала ознака не дає ділення на нуль")] public void Scaler_ConstantFeature_DoesNotDivideByZero() { }
}

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
