using Google.Apis.Admin.Directory.directory_v1.Data;

namespace DeviceAssigner;

internal record StudentDeviceUpdateResult(bool Success, ChromeOsDevice? Device, string Message);
