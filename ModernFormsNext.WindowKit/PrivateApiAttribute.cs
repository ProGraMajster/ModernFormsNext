using System;

namespace ModernFormsNext.WindowKit.Metadata;

/// <summary>
/// Marks public surface that is intended for framework or backend use rather than application code.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Constructor 
                | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Struct)]
public sealed class PrivateApiAttribute : Attribute
{

}
