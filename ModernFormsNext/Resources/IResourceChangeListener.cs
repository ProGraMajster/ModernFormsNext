namespace ModernFormsNext;

internal interface IResourceChangeListener
{
    bool OnResourceChanged(ResourceDictionary source, object key);
}
