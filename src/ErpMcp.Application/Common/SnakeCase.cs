using System.Text;

namespace ErpMcp.Application.Common;

/// <summary>Enum ⇄ snake_case conversion, matching the database and tool argument vocabulary.</summary>
public static class SnakeCase
{
    public static string From<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    public static TEnum To<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value.Replace("_", "", StringComparison.Ordinal), ignoreCase: true);
}
