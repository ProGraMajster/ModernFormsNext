using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    public partial class BindingContext
    {
        private class HashKey
        {
            private readonly WeakReference _wRef;
            private readonly int _dataSourceHashCode;
            private readonly string _dataMember;

            internal HashKey(object dataSource, string? dataMember)
            {
                ArgumentNullException.ThrowIfNull(dataSource);
                dataMember ??= string.Empty;

                // The dataMember should be case insensitive, so convert the
                // dataMember to lower case
                _wRef = new WeakReference(dataSource, false);
                _dataSourceHashCode = dataSource.GetHashCode();
                _dataMember = dataMember.ToLower(CultureInfo.InvariantCulture);
            }

            public override int GetHashCode() => HashCode.Combine(_dataSourceHashCode, _dataMember);

            public override bool Equals(object? target)
            {
                if (target is not HashKey keyTarget)
                {
                    return false;
                }

                return _wRef.Target == keyTarget._wRef.Target && _dataMember == keyTarget._dataMember;
            }
        }
    }
}
