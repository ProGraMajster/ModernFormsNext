namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>Applies the canonical protected classification before optional payload extraction.</summary>
internal static class PlatformAccessibilityPrivacy
{
    internal static bool HasSensitiveAncestor(IPlatformAccessibleObject node)
    {
        int remaining = 512;
        for (IPlatformAccessibleObject? current = node; current is not null; current = current.Parent)
        {
            if (current.GetIsSensitive() || (current.State & 0x20000000) != 0) return true;
            if (--remaining == 0) return true;
        }
        return false;
    }
}
