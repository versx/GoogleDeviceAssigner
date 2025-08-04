namespace DeviceAssigner;

public record StudentDeviceRecord(
    string SerialNumber,
    string? AssetInfo,
    string? OrgUnitPath
);
