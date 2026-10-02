namespace ErpMcp.Infrastructure.Persistence;

internal static class SqlText
{
    /// <summary>
    /// Builds an ILIKE "contains" pattern, escaping LIKE wildcards so user text is matched literally.
    /// </summary>
    public static string? ContainsPattern(string? text) =>
        text is null
            ? null
            : "%" + text.Replace(@"\", @"\\", StringComparison.Ordinal)
                        .Replace("%", @"\%", StringComparison.Ordinal)
                        .Replace("_", @"\_", StringComparison.Ordinal) + "%";

    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? AsUtc(DateTime? value) =>
        value is null ? null : AsUtc(value.Value);
}
