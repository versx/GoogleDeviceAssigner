namespace DeviceAssigner;

internal sealed class StudentDeviceAssignerError(string error) : EventArgs
{
    public string Error => error;
}
