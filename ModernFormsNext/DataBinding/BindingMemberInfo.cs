using System;
using System.Collections.Generic;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Splits a data binding member path into its list path and final field name.
    /// </summary>
    /// <remarks>
    ///  For a member such as <c>Customer.Name</c>, <see cref="BindingPath"/> is
    ///  <c>Customer</c>, <see cref="BindingField"/> is <c>Name</c>, and
    ///  <see cref="BindingMember"/> returns the original normalized member path.
    /// </remarks>
    public readonly struct BindingMemberInfo : IEquatable<BindingMemberInfo>
    {
        private readonly string? _dataList;
        private readonly string? _dataField;

        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingMemberInfo"/> struct.
        /// </summary>
        /// <param name="dataMember">The binding member path to split.</param>
        public BindingMemberInfo(string? dataMember)
        {
            dataMember ??= string.Empty;

            int lastDot = dataMember.LastIndexOf('.');
            if (lastDot != -1)
            {
                _dataList = dataMember[..lastDot];
                _dataField = dataMember[(lastDot + 1)..];
            }
            else
            {
                _dataList = string.Empty;
                _dataField = dataMember;
            }
        }

        /// <summary>
        ///  Gets the portion of the member path before the final field name.
        /// </summary>
        public string BindingPath => _dataList ?? string.Empty;

        /// <summary>
        ///  Gets the final field name in the member path.
        /// </summary>
        public string BindingField => _dataField ?? string.Empty;

        /// <summary>
        ///  Gets the complete normalized binding member path.
        /// </summary>
        public string BindingMember
            => BindingPath.Length > 0
                ? $"{BindingPath}.{BindingField}"
                : BindingField;

        /// <inheritdoc/>
        public override bool Equals(object? otherObject)
        {
            if (otherObject is not BindingMemberInfo otherMember)
            {
                return false;
            }

            return Equals(otherMember);
        }

        /// <inheritdoc/>
        public bool Equals(BindingMemberInfo other)
            => string.Equals(BindingMember, other.BindingMember, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///  Compares two <see cref="BindingMemberInfo"/> values for equality.
        /// </summary>
        /// <param name="a">The first value to compare.</param>
        /// <param name="b">The second value to compare.</param>
        /// <returns><see langword="true"/> when both values describe the same binding member.</returns>
        public static bool operator ==(BindingMemberInfo a, BindingMemberInfo b) => a.Equals(b);

        /// <summary>
        ///  Compares two <see cref="BindingMemberInfo"/> values for inequality.
        /// </summary>
        /// <param name="a">The first value to compare.</param>
        /// <param name="b">The second value to compare.</param>
        /// <returns><see langword="true"/> when the values describe different binding members.</returns>
        public static bool operator !=(BindingMemberInfo a, BindingMemberInfo b) => !a.Equals(b);

        /// <inheritdoc/>
        public override int GetHashCode() => base.GetHashCode();
    }
}
