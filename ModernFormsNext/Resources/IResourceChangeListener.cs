namespace ModernFormsNext;

internal interface IResourceChangeListener
{
    void OnResourceChanged(ResourceDictionary source, object key);
}
