using System;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    public interface IMicroComExceptionCallback
    {
        void RaiseException(Exception e);
    }
}
