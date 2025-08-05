using System.Text.RegularExpressions;

namespace DeviceAssigner;

internal static partial class TemplateResolver
{
    //private static readonly Regex PlaceholderRegex = new(@"{(?<key>[^{}]+)}", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = MyPlaceholderRegex();

    public static string? Resolve(string template, IDictionary<string, string?> values)
    {
        if (string.IsNullOrWhiteSpace(template) || values == null)
            return null;

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return values.TryGetValue(key, out var value)
                ? value ?? string.Empty
                : match.Value;
        });
    }

    public static string? Resolve(string template, TemplateData data)
    {
        if (string.IsNullOrWhiteSpace(template) || data == null)
            return null;

        return template
            .Replace("{CartNumber}", data.CartNumber.ToString())
            .Replace("{DeviceId}", data.DeviceId)
            .Replace("{StudentName}", data.StudentName)
            .Replace("{DeviceNumber}", data.DeviceNumber ?? "")
            .Replace("{SerialNumber}", data.SerialNumber)
            .Replace("{PurchaseId}", data.PurchaseId ?? "")
            .Replace("{Year}", data.Year)
            .Replace("{YearRange}", data.YearRange);
    }

    [GeneratedRegex(@"{(?<key>[^{}]+)}", RegexOptions.Compiled)]
    private static partial Regex MyPlaceholderRegex();
}

internal class TemplateData
{
    public int CartNumber { get; set; }
    public string DeviceId { get; set; } = null!;
    public string DeviceNumber { get; set; } = null!;
    public string SerialNumber { get; set; } = null!;
    public string StudentName { get; set; } = null!;
    public string? PurchaseId { get; set; }

    public string Year { get; set; } = DateTime.Now.Year.ToString();
    public string YearRange => $"{DateTime.Now.Year}-{(DateTime.Now.Year + 1).ToString()[^2..]}";
}
