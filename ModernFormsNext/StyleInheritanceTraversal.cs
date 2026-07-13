using System;

namespace ModernFormsNext;

// Style lookup is a rendering hot path, so cycle detection uses Floyd's algorithm instead of a
// per-call HashSet allocation. Cyclic chains receive an exact bound covering the non-cyclic prefix
// plus one complete cycle; acyclic chains can simply continue until their parent becomes null.
internal static class StyleInheritanceTraversal
{
    internal static int GetLimit<TStyle>(TStyle start, Func<TStyle, TStyle?> getParent)
        where TStyle : class
    {
        TStyle? slow = start;
        TStyle? fast = start;

        while (fast is not null && getParent(fast) is { } fastParent)
        {
            slow = slow is null ? null : getParent(slow);
            fast = getParent(fastParent);

            if (slow is null || !ReferenceEquals(slow, fast))
                continue;

            var prefixLength = 0;
            TStyle prefixProbe = start;
            TStyle meetingProbe = slow;

            while (!ReferenceEquals(prefixProbe, meetingProbe))
            {
                prefixProbe = getParent(prefixProbe)!;
                meetingProbe = getParent(meetingProbe)!;
                prefixLength++;
            }

            var cycleLength = 1;
            TStyle cycleProbe = getParent(prefixProbe)!;

            while (!ReferenceEquals(prefixProbe, cycleProbe))
            {
                cycleProbe = getParent(cycleProbe)!;
                cycleLength++;
            }

            return prefixLength + cycleLength;
        }

        return int.MaxValue;
    }
}
