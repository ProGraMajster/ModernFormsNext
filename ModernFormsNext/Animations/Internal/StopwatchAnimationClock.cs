using System.Diagnostics;

namespace ModernFormsNext.Animations;

internal sealed class StopwatchAnimationClock : IAnimationClock
{
    private readonly long startTimestamp = Stopwatch.GetTimestamp();

    public TimeSpan CurrentTime => Stopwatch.GetElapsedTime(startTimestamp);
}
