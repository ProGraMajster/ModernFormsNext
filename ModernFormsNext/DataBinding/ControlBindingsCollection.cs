using ModernFormsNext.Layout;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Represents the collection of data bindings for a control.
    /// </summary>
    [DefaultEvent(nameof(CollectionChanged))]
    public class ControlBindingsCollection : BindingsCollection
    {
        private readonly IBindableComponent _control;

        /// <summary>
        ///  Initializes a new instance of the <see cref="ControlBindingsCollection"/> class.
        /// </summary>
        /// <param name="control">The component that owns the collection.</param>
        public ControlBindingsCollection(IBindableComponent control)
        {
            _control = control;
        }

        /// <summary>
        ///  Gets the bindable component that owns this collection.
        /// </summary>
        public IBindableComponent BindableComponent => _control;

        /// <summary>
        ///  Gets the owning component as a <see cref="Control"/>, when it is a control.
        /// </summary>
        public Control? Control => _control as Control;

        /// <summary>
        ///  Gets the binding associated with the specified component property name.
        /// </summary>
        /// <param name="propertyName">The property name to find.</param>
        /// <returns>The matching binding, or <see langword="null"/> when no binding exists.</returns>
        public Binding? this[string propertyName]
        {
            get
            {
                foreach (Binding binding in this)
                {
                    if (string.Equals(binding.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return binding;
                    }
                }

                return null;
            }
        }

        /// <summary>
        ///  Adds the binding to the collection. An ArgumentNullException is thrown if this
        ///  binding is null. An exception is thrown if a binding to the same target and
        ///  Property as an existing binding or if the binding's column isn't a valid column
        ///  given this DataSource.Table's schema.
        ///  Fires the CollectionChangedEvent.
        /// </summary>
        public new void Add(Binding binding) => base.Add(binding);

        /// <summary>
        ///  Creates the binding and adds it to the collection. An InvalidBindingException is
        ///  thrown if this binding can't be constructed. An exception is thrown if a binding
        ///  to the same target and Property as an existing binding or if the binding's column
        ///  isn't a valid column given this DataSource.Table's schema.
        ///  Fires the CollectionChangedEvent.
        /// </summary>
        public Binding Add(string propertyName, object dataSource, string? dataMember) =>
            Add(
                propertyName,
                dataSource,
                dataMember,
                formattingEnabled: false,
                DefaultDataSourceUpdateMode,
                nullValue: null,
                formatString: string.Empty,
                formatInfo: null);

        /// <summary>
        ///  Creates a binding with optional formatting and adds it to the collection.
        /// </summary>
        /// <param name="propertyName">The component property to bind.</param>
        /// <param name="dataSource">The data source object, list, or binding source.</param>
        /// <param name="dataMember">The source member to bind to.</param>
        /// <param name="formattingEnabled">Whether format and parse conversion behavior is enabled.</param>
        /// <returns>The binding added to the collection.</returns>
        public Binding Add(
            string propertyName,
            object dataSource,
            string? dataMember,
            bool formattingEnabled) =>
                Add(
                    propertyName,
                    dataSource,
                    dataMember,
                    formattingEnabled,
                    DefaultDataSourceUpdateMode,
                    nullValue: null,
                    formatString: string.Empty,
                    formatInfo: null);

        /// <summary>
        ///  Creates a binding with optional formatting and a data source update mode, then
        ///  adds it to the collection.
        /// </summary>
        /// <param name="propertyName">The component property to bind.</param>
        /// <param name="dataSource">The data source object, list, or binding source.</param>
        /// <param name="dataMember">The source member to bind to.</param>
        /// <param name="formattingEnabled">Whether format and parse conversion behavior is enabled.</param>
        /// <param name="updateMode">When component changes are written back to the data source.</param>
        /// <returns>The binding added to the collection.</returns>
        public Binding Add(
            string propertyName,
            object dataSource,
            string? dataMember,
            bool formattingEnabled,
            DataSourceUpdateMode updateMode) =>
                Add(
                    propertyName,
                    dataSource,
                    dataMember,
                    formattingEnabled,
                    updateMode,
                    nullValue: null,
                    formatString: string.Empty,
                    formatInfo: null);

        /// <summary>
        ///  Creates a binding with optional formatting, a data source update mode, and a null
        ///  display value, then adds it to the collection.
        /// </summary>
        /// <param name="propertyName">The component property to bind.</param>
        /// <param name="dataSource">The data source object, list, or binding source.</param>
        /// <param name="dataMember">The source member to bind to.</param>
        /// <param name="formattingEnabled">Whether format and parse conversion behavior is enabled.</param>
        /// <param name="updateMode">When component changes are written back to the data source.</param>
        /// <param name="nullValue">The value displayed when the source value is null.</param>
        /// <returns>The binding added to the collection.</returns>
        public Binding Add(
            string propertyName,
            object dataSource,
            string? dataMember,
            bool formattingEnabled,
            DataSourceUpdateMode updateMode,
            object? nullValue) =>
                Add(
                    propertyName,
                    dataSource,
                    dataMember,
                    formattingEnabled,
                    updateMode,
                    nullValue,
                    formatString: string.Empty,
                    formatInfo: null);

        /// <summary>
        ///  Creates a binding with optional formatting, a data source update mode, a null
        ///  display value, and a format string, then adds it to the collection.
        /// </summary>
        /// <param name="propertyName">The component property to bind.</param>
        /// <param name="dataSource">The data source object, list, or binding source.</param>
        /// <param name="dataMember">The source member to bind to.</param>
        /// <param name="formattingEnabled">Whether format and parse conversion behavior is enabled.</param>
        /// <param name="updateMode">When component changes are written back to the data source.</param>
        /// <param name="nullValue">The value displayed when the source value is null.</param>
        /// <param name="formatString">The format string used when formatting data source values.</param>
        /// <returns>The binding added to the collection.</returns>
        public Binding Add(
            string propertyName,
            object dataSource,
            string? dataMember,
            bool formattingEnabled,
            DataSourceUpdateMode updateMode,
            object? nullValue,
            string formatString) =>
                Add(
                    propertyName,
                    dataSource,
                    dataMember,
                    formattingEnabled,
                    updateMode,
                    nullValue,
                    formatString,
                    formatInfo: null);

        /// <summary>
        ///  Creates a binding with the full formatting and update configuration, then adds it
        ///  to the collection.
        /// </summary>
        /// <param name="propertyName">The component property to bind.</param>
        /// <param name="dataSource">The data source object, list, or binding source.</param>
        /// <param name="dataMember">The source member to bind to.</param>
        /// <param name="formattingEnabled">Whether format and parse conversion behavior is enabled.</param>
        /// <param name="updateMode">When component changes are written back to the data source.</param>
        /// <param name="nullValue">The value displayed when the source value is null.</param>
        /// <param name="formatString">The format string used when formatting data source values.</param>
        /// <param name="formatInfo">The format provider used for culture-aware formatting.</param>
        /// <returns>The binding added to the collection.</returns>
        public Binding Add(
            string propertyName,
            object dataSource,
            string? dataMember,
            bool formattingEnabled,
            DataSourceUpdateMode updateMode,
            object? nullValue,
            string formatString,
            IFormatProvider? formatInfo)
        {
            ArgumentNullException.ThrowIfNull(dataSource);

            Binding binding = new(
                propertyName,
                dataSource,
                dataMember,
                formattingEnabled,
                updateMode,
                nullValue,
                formatString,
                formatInfo);
            Add(binding);
            return binding;
        }

        /// <summary>
        ///  Creates the binding and adds it to the collection. An InvalidBindingException is
        ///  thrown if this binding can't be constructed. An exception is thrown if a binding to
        ///  the same target and Property as an existing binding or if the binding's column isn't
        ///  a valid column given this DataSource.Table's schema.
        ///  Fires the CollectionChangedEvent.
        /// </summary>
        protected override void AddCore(Binding dataBinding)
        {
            ArgumentNullException.ThrowIfNull(dataBinding);

            if (dataBinding.BindableComponent == _control)
            {
                throw new ArgumentException(SR.BindingsCollectionAdd1, nameof(dataBinding));
            }

            if (dataBinding.BindableComponent is not null)
            {
                throw new ArgumentException(SR.BindingsCollectionAdd2, nameof(dataBinding));
            }

            // important to set prop first for error checking.
            dataBinding.SetBindableComponent(_control);

            base.AddCore(dataBinding);
        }

        internal void CheckDuplicates(Binding binding)
        {
            Debug.Assert(!string.IsNullOrEmpty(binding.PropertyName), "The caller should check for this.");

            for (int i = 0; i < Count; i++)
            {
                Binding current = this[i];
                if (binding != current
                    && !string.IsNullOrEmpty(current.PropertyName)
                    && string.Equals(binding.PropertyName, current.PropertyName, StringComparison.InvariantCulture))
                {
                    throw new ArgumentException(SR.BindingsCollectionDup, nameof(binding));
                }
            }
        }

        /// <summary>
        ///  Clears the collection of any bindings.
        ///  Fires the CollectionChangedEvent.
        /// </summary>
        public new void Clear() => base.Clear();

        /// <summary>
        ///  Removes all bindings and detaches them from the owning component.
        /// </summary>
        protected override void ClearCore()
        {
            int numLinks = Count;
            for (int i = 0; i < numLinks; i++)
            {
                Binding dataBinding = this[i];
                dataBinding.SetBindableComponent(null);
            }

            base.ClearCore();
        }

        /// <summary>
        ///  Gets or sets the update mode used by overloads that do not specify one explicitly.
        /// </summary>
        public DataSourceUpdateMode DefaultDataSourceUpdateMode { get; set; } = DataSourceUpdateMode.OnValidation;

        /// <summary>
        ///  Removes the given binding from the collection.
        ///  An ArgumentNullException is thrown if this binding is null. An ArgumentException is
        ///  thrown if this binding doesn't belong to this collection.
        ///  The CollectionChanged event is fired if it succeeds.
        /// </summary>
        public new void Remove(Binding binding) => base.Remove(binding);

        /// <summary>
        ///  Removes the given binding from the collection.
        ///  It throws an IndexOutOfRangeException if this doesn't have a valid binding.
        ///  The CollectionChanged event is fired if it succeeds.
        /// </summary>
        public new void RemoveAt(int index) => base.RemoveAt(index);

        /// <summary>
        ///  Removes a binding and detaches it from the owning component.
        /// </summary>
        /// <param name="dataBinding">The binding to remove.</param>
        protected override void RemoveCore(Binding dataBinding)
        {
            ArgumentNullException.ThrowIfNull(dataBinding);

            if (dataBinding.BindableComponent != _control)
            {
                throw new ArgumentException(SR.BindingsCollectionForeign, nameof(dataBinding));
            }

            dataBinding.SetBindableComponent(value: null);
            base.RemoveCore(dataBinding);
        }
    }
}
