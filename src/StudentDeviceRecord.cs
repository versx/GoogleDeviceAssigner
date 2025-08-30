namespace DeviceAssigner;

//internal record StudentDeviceRecord(string SerialNumber, string? AssetInfo, string? OrgUnitPath);
internal record StudentDeviceRecord(
    string SerialNumber,
    string? CartNumber,
    string? DeviceNumber,
    string? StudentName,
    string? Damage,
    string? PurchaseId);
