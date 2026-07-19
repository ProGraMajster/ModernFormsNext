namespace ModernFormsNext.Animations;

internal interface IAnimationTickSource : IDisposable
{
    bool IsRunning { get; }

    void Start(Action tickRequested);

    void Stop();
}
