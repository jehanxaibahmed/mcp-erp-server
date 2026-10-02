using System.Text.RegularExpressions;

namespace ErpMcp.Application.Common;

/// <summary>
/// Argument checks for use-case entry points. Each method returns the normalised value so call
/// sites read as <c>var sku = Guard.Sku(input);</c>.
/// </summary>
public static partial class Guard
{
    public const int MaxSearchLength = 100;

    public static string CustomerCode(string? value, string field = "customerCode") =>
        MatchCode(value, field, CustomerCodePattern(), "CUST-0001");

    public static string Sku(string? value, string field = "sku") =>
        MatchCode(value, field, SkuPattern(), "BEV-0001");

    public static string OrderNumber(string? value, string field = "orderNumber") =>
        MatchCode(value, field, OrderNumberPattern(), "SO-100001");

    public static string WarehouseCode(string? value, string field = "warehouseCode") =>
        MatchCode(value, field, WarehouseCodePattern(), "WH-MAN");

    /// <summary>Optional free-text search: trimmed, length-limited, null when blank.</summary>
    public static string? SearchText(string? value, string field = "query")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxSearchLength)
        {
            throw new InputValidationException(field, $"must be at most {MaxSearchLength} characters.");
        }

        return trimmed;
    }

    public static int InRange(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new InputValidationException(field, max == int.MaxValue
                ? $"must be {min} or greater."
                : $"must be between {min} and {max}.");
        }

        return value;
    }

    public static TEnum? OptionalEnum<TEnum>(string? value, string field)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalised = value.Replace("_", "", StringComparison.Ordinal).Trim();
        if (Enum.TryParse<TEnum>(normalised, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var allowed = string.Join(", ", Enum.GetValues<TEnum>().Select(SnakeCase.From));
        throw new InputValidationException(field, $"must be one of: {allowed}.");
    }

    private static string MatchCode(string? value, string field, Regex pattern, string example)
    {
        var normalised = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalised))
        {
            throw new InputValidationException(field, "is required.");
        }

        if (!pattern.IsMatch(normalised))
        {
            throw new InputValidationException(field, $"expected a code like '{example}'.");
        }

        return normalised;
    }

    [GeneratedRegex("^CUST-[0-9]{4}$")]
    private static partial Regex CustomerCodePattern();

    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^SO-[0-9]{6,}$")]
    private static partial Regex OrderNumberPattern();

    [GeneratedRegex("^WH-[A-Z]{3}$")]
    private static partial Regex WarehouseCodePattern();
}
