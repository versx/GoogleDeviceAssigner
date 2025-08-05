using DeviceAssigner;

var config = Config.Load() ?? Config.ParseConfigFromArgs(args);
if (config == null)
{
    Console.WriteLine($"Failed to load {Config.DefaultConfigFileName}, exiting...");
    PrintUsage();
    return;
}

var assigner = new StudentDeviceAssigner(config);
assigner.Completed += (_, _) => Console.WriteLine($"Done");
assigner.Error += (_, e) => Console.WriteLine($"[ERROR] {e.Error}");
await assigner.Run();

static void PrintUsage()
{
    Console.WriteLine("\n📘 Usage:");
    Console.WriteLine(
        "  dotnet run (uses 'config.json' in current directory if it exists)"
    );
    Console.WriteLine(
        "  dotnet run -- " +
        "--cartNumber=1" +
        "--deviceIdTemplate={CartNumber}-{DeviceNumber}" +
        "--assetIdTemplate={DeviceId} {PurchaseId} {StudentName}" +
        "--tabNameTemplate=/Chromebooks/Cart {CartNumber} {YearRange}" +
        "--ouTemplate=/Chromebooks/Cart {CartNumber} {YearRange}" +
        "--csv=devices.csv" +
        "--serviceAccount=service-account.json " +
        "--adminUser=admin@example.com " +
        "--customerId=my_customer " +
        "[--googleSheetId=1_3lk4j23KJsl3dd] " +
        "[--dryRun] [--promptOnError]"
    );
}
