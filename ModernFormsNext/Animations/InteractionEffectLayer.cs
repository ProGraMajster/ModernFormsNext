namespace ModernFormsNext.Animations;

/// <summary>Identifies a shared rendering layer for interaction effects.</summary>
public enum InteractionEffectLayer
{
    /// <summary>After the background and border, before control content.</summary>
    AboveBackgroundBelowContent,

    /// <summary>After control content and before the framework focus overlay.</summary>
    AboveContent
}
