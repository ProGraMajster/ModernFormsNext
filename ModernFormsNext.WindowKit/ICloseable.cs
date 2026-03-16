using System;

namespace ModernFormsNext.WindowKit
{
    public interface ICloseable
    {
        event EventHandler? Closed;
    }
}
