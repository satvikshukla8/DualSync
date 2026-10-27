using NAudio.CoreAudioApi;

namespace DualSync.Audio;

public sealed record AudioDevice(string Id, string Name, bool IsDefault, bool Available = true)
{
    public override string ToString() => Name + (IsDefault ? " · Windows default" : "") + (Available ? "" : " · disconnected");
}

public static class DeviceCatalog
{
    public static List<AudioDevice> Enumerate()
    {
        using var enumerator = new MMDeviceEnumerator();
        string? defaultId = null;
        if (enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var defaultDevice))
        { using (defaultDevice) defaultId = defaultDevice!.ID; }
        var result = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device) result.Add(new(device.ID, device.FriendlyName, device.ID == defaultId));
        }
        return result.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name).ToList();
    }
}
