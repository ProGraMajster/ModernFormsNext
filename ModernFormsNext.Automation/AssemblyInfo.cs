using System.Runtime.CompilerServices;

// The Windows adapter reconstructs detached result/snapshot DTOs using their existing internal
// constructors. It must not access live traversal/session internals or introduce serializer APIs here.
[assembly: InternalsVisibleTo("ModernFormsNext.Automation.Windows")]
