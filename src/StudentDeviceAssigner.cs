using CsvHelper;

using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

using ShellProgressBar;

namespace DeviceAssigner;

internal class StudentDeviceAssigner(Config config)
{
    private const string AppName = "DeviceAssigner";
    private const string DefaultFailedCsvFilePath = "failed-devices.csv";

    #region Properties

    public string CsvFilePath => config.CsvFilePath;
    public string AdminUserToImpersonate => config.AdminUserToImpersonate;
    public string CustomerId => config.CustomerId;
    public string ServiceAccountPath => config.ServiceAccountFilePath;
    public bool IsDryRun => config.IsDryRun;

    #endregion

    #region Events

    public event EventHandler? Completed;
    private void OnCompleted() => Completed?.Invoke(this, new());

    public event EventHandler<StudentDeviceAssignerError>? Error;
    private void OnError(string error) => Error?.Invoke(this, new(error));

    #endregion

    #region Public Methods

    public async Task Run(CancellationToken token = default)
    {
        var records = ReadCsv(CsvFilePath);
        if (records.Count == 0)
        {
            OnError("No device records found in CSV.");
            return;
        }

        using var progress = new ProgressBar(records.Count, "Processing devices...");
        progress.WriteLine($"Parsed {records.Count:N0} records from CSV...");

        var failed = new List<StudentDeviceRecord>();
        using var service = CreateService();

        foreach (var record in records)
        {
            await ProcessDeviceRecord(service, record, failed, progress);

            progress.Tick(record.SerialNumber);

            token.ThrowIfCancellationRequested();
        }

        if (failed.Count > 0)
        {
            ExportCsv(DefaultFailedCsvFilePath, failed);
            progress.WriteErrorLine($"Failed to update {failed.Count:N0} devices. See '{DefaultFailedCsvFilePath}'.");
        }

        OnCompleted();
    }

    #endregion

    #region Private Methods

    private async Task ProcessDeviceRecord(
        DirectoryService service,
        StudentDeviceRecord record,
        List<StudentDeviceRecord> failed,
        ProgressBar progress)
    {
        var result = await UpdateDevice(service, record);
        if (result.Success)
        {
            progress.WriteLine($"[SUCCESS] {result.Message}");
            return;
        }

        failed.Add(record);
        progress.WriteErrorLine($"[ERROR] {result.Message}");

        if (!config.PromptOnError)
            return;

        progress.WriteLine("Press 'y' to continue or any other key to abort:");
        var key = Console.ReadKey(true);
        if (key.Key != ConsoleKey.Y)
            Environment.Exit(1);
    }

    private async Task<StudentDeviceUpdateResult> UpdateDevice(DirectoryService service, StudentDeviceRecord record)
    {
        try
        {
            var device = await GetChromeDeviceBySerial(service, record.SerialNumber);
            if (device?.DeviceId is null)
            {
                return new(false, $"Device not found for serial '{record.SerialNumber}'");
            }

            var updatedDevice = new ChromeOsDevice
            {
                AnnotatedAssetId = record.AssetInfo,
                OrgUnitPath = record.OrgUnitPath ?? device.OrgUnitPath,
                Notes = string.IsNullOrEmpty(device.Notes)
                    ? record.AssetInfo
                    : $"{record.AssetInfo} ({device.Notes})",
            };

            if (IsDryRun)
            {
                return new(true, $"[Dry Run] '{device.SerialNumber}', Asset: {record.AssetInfo}, OU: {updatedDevice.OrgUnitPath}");
            }

            await service.Chromeosdevices.Update(updatedDevice, CustomerId, device.DeviceId).ExecuteAsync();
            return new(true, $"Updated device: '{record.SerialNumber}', Asset: {record.AssetInfo}, OU: {record.OrgUnitPath}");
        }
        catch (Exception ex)
        {
            return new(false, $"Error updating device '{record.SerialNumber}': {ex.Message}");
        }
    }

    private async Task<ChromeOsDevice?> GetChromeDeviceBySerial(DirectoryService service, string serialNumber, int maxResults = 1)
    {
        var query = service.Chromeosdevices.List(CustomerId);
        query.Query = $"id:{serialNumber.Trim()}";
        query.MaxResults = maxResults;

        var response = await query.ExecuteAsync();
        return response.Chromeosdevices?.FirstOrDefault();
    }

    private GoogleCredential CreateCredentials()
    {
        return GoogleCredential
            .FromFile(ServiceAccountPath)
            .CreateScoped(DirectoryService.Scope.AdminDirectoryDeviceChromeos)
            .CreateWithUser(AdminUserToImpersonate); // Impersonate admin
    }

    private DirectoryService CreateService()
    {
        return new DirectoryService(new BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredentials(),
            ApplicationName = AppName,
        });
    }

    private List<StudentDeviceRecord> ReadCsv(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
            return [.. csv.GetRecords<StudentDeviceRecord>()];
        }
        catch (Exception ex)
        {
            OnError($"Failed to parse CSV: {ex.Message}");
            return [];
        }
    }

    private void ExportCsv(string path, IEnumerable<StudentDeviceRecord> records)
    {
        try
        {
            using var writer = new StreamWriter(path);
            using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteHeader<StudentDeviceRecord>();
            csv.NextRecord();
            csv.WriteRecords(records);
        }
        catch (Exception ex)
        {
            OnError($"Failed to write failed devices CSV: {ex.Message}");
        }
    }

    #endregion
}

internal record StudentDeviceRecord(string SerialNumber, string? AssetInfo, string? OrgUnitPath);

internal record StudentDeviceUpdateResult(bool Success, string Message);

internal sealed class StudentDeviceAssignerError(string error) : EventArgs
{
    public string Error => error;
}
