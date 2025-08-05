using CsvHelper;

using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

using ShellProgressBar;

namespace DeviceAssigner;

internal class StudentDeviceAssigner(Config config)
{
    #region Constants

    private const string AppName = "DeviceAssigner";
    private const string DefaultFailedCsvFilePath = "failed-devices.csv";

    private static readonly string[] AdminScopes =
    [
        "https://www.googleapis.com/auth/admin.directory.device.chromeos",
        "https://www.googleapis.com/auth/admin.directory.orgunit",
        "https://www.googleapis.com/auth/spreadsheets",
    ];

    #endregion

    #region Variables

    private SheetsService? _sheetsService;
    private OrgUnitCreator _ouCreator = null!;

    #endregion

    #region Properties

    public int CartNumber => config.CartNumber;
    public string DeviceIdTemplate => config.DeviceIdTemplate;
    public string AssetIdTemplate => config.AssetIdTemplate;
    public string TabNameTemplate => config.TabNameTemplate;
    public string OrgUnitPathTemplate => config.OrgUnitPathTemplate;
    public string CsvFilePath => config.CsvFilePath;
    public string AdminUserToImpersonate => config.AdminUserToImpersonate;
    public string CustomerId => config.CustomerId;
    public string ServiceAccountPath => config.ServiceAccountFilePath;
    public string? GoogleSheetId => config.GoogleSheetId;
    public bool PromptOnError => config.PromptOnError;
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
        using var adminService = CreateDirectoryService();
        _ouCreator = new OrgUnitCreator(adminService, CustomerId);
        _sheetsService = CreateSheetsService();

        foreach (var record in records)
        {
            await ProcessDeviceRecord(adminService, record, failed, progress);

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

    #region Processing Methods

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
            if (!string.IsNullOrWhiteSpace(GoogleSheetId))
            {
                try
                {
                    await UpdateGoogleSheetAsync(result.Device ?? null!, record);
                }
                catch (Exception ex)
                {
                    OnError($"Error updating Google Sheet with device '{record.SerialNumber}': {ex.Message}");
                }
            }
            return;
        }

        failed.Add(record);
        progress.WriteErrorLine($"[ERROR] {result.Message}");

        if (!PromptOnError)
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
                return new(false, device, $"Device not found for serial '{record.SerialNumber}'");
            }

            var deviceId = TemplateResolver.FormatTemplate(DeviceIdTemplate, new Dictionary<string, string?>
            {
                ["CartNumber"] = CartNumber.ToString(),
                ["DeviceNumber"] = record.DeviceNumber!,
                ["SerialNumber"] = record.SerialNumber,
                ["PurchaseId"] = record.PurchaseId,
            });
            var assetId = TemplateResolver.FormatTemplate(AssetIdTemplate, new Dictionary<string, string?>
            {
                ["CartNumber"] = CartNumber.ToString(),
                ["DeviceId"] = deviceId!,
                ["StudentName"] = record.StudentName!,
                ["DeviceNumber"] = record.DeviceNumber!,
                ["SerialNumber"] = record.SerialNumber,
                ["PurchaseId"] = record.PurchaseId,
            });
            var orgUnitPath = TemplateResolver.Resolve(OrgUnitPathTemplate, new TemplateData
            {
                CartNumber = CartNumber,
                DeviceNumber = deviceId!,
                SerialNumber = record.SerialNumber,
                PurchaseId = record.PurchaseId,
            });
            var notes = string.IsNullOrEmpty(device.Notes)
                ? assetId
                : $"{assetId} ({device.Notes})";

            var updatedDevice = new ChromeOsDevice
            {
                AnnotatedAssetId = assetId,
                OrgUnitPath = orgUnitPath ?? device.OrgUnitPath,
                Notes = notes == device.Notes
                    ? device.Notes
                    : notes,
            };

            if (IsDryRun)
            {
                return new(true, device, $"[Dry Run] '{device.SerialNumber}', Asset: {assetId}, OU: {updatedDevice.OrgUnitPath}");
            }

            // Ensure OU exists, create it if it doesn't
            await _ouCreator.EnsureOrgUnitExistsAsync(updatedDevice.OrgUnitPath);

            await service.Chromeosdevices.Update(updatedDevice, CustomerId, device.DeviceId).ExecuteAsync();
            return new(true, device, $"Updated device: '{device.SerialNumber}', Asset: {assetId}, OU: {updatedDevice.OrgUnitPath}");
        }
        catch (Exception ex)
        {
            return new(false, null, $"Error updating device '{record.SerialNumber}': {ex.Message}");
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

    #endregion

    #region Auth Methods

    private GoogleCredential CreateCredentials() =>
        GoogleCredential
            .FromFile(ServiceAccountPath)
            .CreateScoped(AdminScopes)
            .CreateWithUser(AdminUserToImpersonate);

    private DirectoryService CreateDirectoryService() =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredentials(),
            ApplicationName = AppName,
        });

    private SheetsService CreateSheetsService() =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredentials(),
            ApplicationName = AppName,
        });

    #endregion

    #region CSV Methods

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

    #region Google Sheet Methods

    private async Task UpdateGoogleSheetAsync(ChromeOsDevice device, StudentDeviceRecord record)
    {
        if (_sheetsService is null || string.IsNullOrWhiteSpace(GoogleSheetId))
            return;

        //var tabName = $"Cart {CartNumber}";
        var tabName = TemplateResolver.Resolve(TabNameTemplate, new TemplateData
        {
            CartNumber = CartNumber,
            DeviceNumber = record.DeviceNumber!,
            SerialNumber = record.SerialNumber,
            PurchaseId = record.PurchaseId,
        });
        //var sheet = await EnsureSheetTabExists(tabName);

        //var range = $"{tabName}!A2:K";
        //var response = await _sheetsService.Spreadsheets.Values.Get(GoogleSheetId, range).ExecuteAsync();
        //var existing = response.Values?.ToList() ?? [];

        //var rowIndex = existing.FindIndex(row =>
        //    row.Count > 2 && string.Equals(row[2]?.ToString(), record.SerialNumber, StringComparison.OrdinalIgnoreCase));

        var newRow = new List<object?>
        {
            $"{CartNumber}-{record.DeviceNumber}",
            record.SerialNumber,
            CartNumber,
            device.MacAddress,
            "", // Out of Service
            device.Model,
            record.StudentName,
            device.AnnotatedAssetId, //Existing Device Id
            record.PurchaseId,
            record.Damage,
        };

        //if (rowIndex >= 0)
        //{
        //    var updateRange = $"{tabName}!A{rowIndex + 2}";
        //    var update = _sheetsService.Spreadsheets.Values.Update(new ValueRange { Values = [newRow] }, GoogleSheetId, updateRange);
        //    update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        //    var result = await update.ExecuteAsync();
        //}
        //else
        //{
            var appendRange = $"{tabName}!A:K";
            var append = _sheetsService.Spreadsheets.Values.Append(new ValueRange { Values = [newRow] }, GoogleSheetId, appendRange);
            append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            /*var result =*/ await append.ExecuteAsync();
        //}
    }

    private async Task<SheetProperties> EnsureSheetTabExists(string title)
    {
        var spreadsheet = await _sheetsService!.Spreadsheets.Get(GoogleSheetId).ExecuteAsync();
        var sheet = spreadsheet.Sheets?.FirstOrDefault(s => s.Properties.Title == title);

        if (sheet != null)
            return sheet.Properties;

        var addSheetRequest = new AddSheetRequest
        {
            Properties = new SheetProperties { Title = title },
        };

        var batchUpdate = new BatchUpdateSpreadsheetRequest
        {
            Requests = [new Request { AddSheet = addSheetRequest }],
        };

        await _sheetsService.Spreadsheets.BatchUpdate(batchUpdate, GoogleSheetId).ExecuteAsync();
        return addSheetRequest.Properties;
    }

    #endregion
}

internal sealed class StudentDeviceAssignerError(string error) : EventArgs
{
    public string Error => error;
}
