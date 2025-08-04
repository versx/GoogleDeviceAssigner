using DeviceAssigner;

var config = Config.Load() ?? Config.ParseConfigFromArgs(args);
if (config == null)
{
    Console.WriteLine($"Failed to load {Config.DefaultConfigFileName}, exiting...");
    PrintUsage();
    return;
}

var assigner = new StudentDeviceAssigner(config);
assigner.Error += (_, e) => Console.WriteLine($"[ERROR] {e.Error}");
await assigner.Run();

Console.WriteLine("Done");

static void PrintUsage()
{
    Console.WriteLine("\n📘 Usage:");
    Console.WriteLine(
        "  dotnet run -- " +
        "--serviceAccount=service-account.json " +
        "--adminUser=admin@example.com " +
        "--customerId=my_customer " +
        "--csv=devices.csv [--dryRun] [--promptOnError]"
    );
}
