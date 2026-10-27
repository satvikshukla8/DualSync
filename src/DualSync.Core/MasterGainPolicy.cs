namespace DualSync.Core;

public static class MasterGainPolicy
{
    public static double Calculate(AudioProfile profile, double sourceVolume, bool sourceMuted)
    {
        var level = Numbers.Clamp(sourceVolume, 0, 1, 0);
        // Respect the user's Windows mute/zero volume even when loopback bypasses it.
        if (sourceMuted || level == 0) return 0;
        if (profile.MasterMode == MasterMode.AppOnly) return profile.MasterVolume;
        return profile.LoopbackIncludesEndpointVolume ? 1 : level;
    }
}
