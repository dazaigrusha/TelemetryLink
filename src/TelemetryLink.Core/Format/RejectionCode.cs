namespace TelemetryLink.Core.Format;

/// <summary>
/// Причина відмови приймача (FORMAT_SPEC §6).
///
/// Імена навмисно збігаються з кодами специфікації, а не з угодами C#:
/// це константи протоколу, і саме в такому вигляді вони порівнюються з
/// expected_failures.json та друкуються у звіті.
/// </summary>
public enum RejectionCode
{
    /// <summary>Пакет прийнято.</summary>
    None = 0,

    /// <summary>Пакет коротший за 75 байтів (43 + 0 + 32).</summary>
    E_SHORT,

    /// <summary>Невірний magic або version.</summary>
    E_MAGIC,

    /// <summary>Довжина пакета не відповідає ciphertext_len.</summary>
    E_LENGTH,

    /// <summary>HMAC не збігається: зіпсовані дані, зіпсований тег або невірний пароль.</summary>
    E_HMAC,

    /// <summary>Ненульове reserved, невідомі біти flags, неузгоджені довжини секцій.</summary>
    E_PAYLOAD,

    /// <summary>Довжина коду &gt; 15, символ відсутній у таблиці, потік обірвано.</summary>
    E_CODE
}
