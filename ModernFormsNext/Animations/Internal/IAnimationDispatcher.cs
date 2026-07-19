namespace ModernFormsNext.Animations;

internal interface IAnimationDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
