using ModernFormsNext.Layout;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    [SRDescription(nameof(SR.ICurrencyManagerProviderDescr))]
    public interface ICurrencyManagerProvider
    {
        /// <summary>
        ///  Return the main currency manager for this data source.
        /// </summary>
        CurrencyManager? CurrencyManager { get; }

        /// <summary>
        ///  Return a related currency manager for specified data member on this data source.
        ///  If data member is null or empty, this method returns the data source's main currency
        ///  manager (ie. this method returns the same value as the CurrencyManager property).
        /// </summary>
        CurrencyManager? GetRelatedCurrencyManager(string? dataMember);
    }
}
