using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeviceAssigner;

internal class Config
{
    #region Constants

    public const string DefaultConfigFileName = "config.json";
    private const string DefaultCustomerId = "my_customer";
    private const string DefaultCsvPath = "devices.csv";
    private const string DefaultServiceAccountPath = "service-account.json";

    #endregion

    #region Variables

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    #endregion

    #region Properties

    [JsonPropertyName("csv")]
    public string CsvFilePath { get; set; } = DefaultCsvPath;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = DefaultCustomerId;

    [JsonPropertyName("adminUser")]
    public string AdminUserToImpersonate { get; set; } = null!;

    [JsonPropertyName("serviceAccount")]
    public string ServiceAccountFilePath { get; set; } = DefaultServiceAccountPath;

    [JsonPropertyName("promptOnError")]
    public bool PromptOnError { get; set; } = true;

    [JsonPropertyName("dryRun")]
    public bool IsDryRun { get; set; }

    #endregion

    #region Public Methods

    public void Save(string fileName = DefaultConfigFileName)
    {
        var json = JsonSerializer.Serialize(this, _jsonOptions);
        File.WriteAllText(fileName, json);
    }

    public static Config? Load(string fileName = DefaultConfigFileName)
    {
        if (!File.Exists(fileName))
            return null;

        var json = File.ReadAllText(fileName);
        var obj = JsonSerializer.Deserialize<Config>(json, _jsonOptions);
        return obj;
    }

    public static Config? ParseConfigFromArgs(string[] args)
    {
        string? GetArg(string name)
        {
            var prefix = $"--{name}=";
            return args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
        }

        var config = new Config
        {
            ServiceAccountFilePath = GetArg("serviceAccount") ?? DefaultServiceAccountPath,
            AdminUserToImpersonate = GetArg("adminUser")!,
            CustomerId = GetArg("customerId") ?? DefaultCustomerId,
            CsvFilePath = GetArg("csv") ?? DefaultCsvPath,
            IsDryRun = args.Contains("--dryRun", StringComparer.OrdinalIgnoreCase),
            PromptOnError = args.Contains("--promptOnError", StringComparer.OrdinalIgnoreCase),
        };

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.ServiceAccountFilePath) ||
            string.IsNullOrWhiteSpace(config.AdminUserToImpersonate) ||
            string.IsNullOrWhiteSpace(config.CustomerId) ||
            string.IsNullOrWhiteSpace(config.CsvFilePath))
        {
            return null;
        }

        return config;
    }

    #endregion
}
