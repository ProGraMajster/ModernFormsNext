using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    public partial class CurrencyManager
    {
        [Flags]
        private enum CurrencyManagerStates : byte
        {
            Bound = 0b0000_0001,
            ShouldBind = 0b0000_0010,
            PullingData = 0b0000_0100,
            InChangeRecordState = 0b0000_1000,
            SuspendPushDataInCurrentChanged = 0b0001_0000,
        }
    }
}
