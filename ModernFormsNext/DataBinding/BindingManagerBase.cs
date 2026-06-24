using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides the base behavior for managers that coordinate bindings against a data source.
    /// </summary>
    /// <remarks>
    ///  <see cref="CurrencyManager"/> manages list-like sources, while <see cref="PropertyManager"/>
    ///  manages single-object property sources. Derived managers are responsible for maintaining
    ///  current item state and for propagating data between their <see cref="Bindings"/> and the
    ///  underlying data source.
    /// </remarks>
    public abstract class BindingManagerBase
    {
        private BindingsCollection? _bindings;
        private bool _pullingData;

        /// <summary>
        ///  Stores handlers for the <see cref="CurrentChanged"/> event.
        /// </summary>
        /// <remarks>
        ///  This field is protected for WinForms-like compatibility and should only be invoked by
        ///  derived binding managers from their current-change event flow.
        /// </remarks>
        protected EventHandler? onCurrentChangedHandler; // Don't rename (breaking change)

        /// <summary>
        ///  Stores handlers for the <see cref="PositionChanged"/> event.
        /// </summary>
        /// <remarks>
        ///  This field is protected for WinForms-like compatibility and should only be invoked by
        ///  derived binding managers from their position-change event flow.
        /// </remarks>
        protected EventHandler? onPositionChangedHandler; // Don't rename (breaking change)

        // Hook BindingComplete events on all owned Binding objects, and propagate those events through our own BindingComplete event
        private BindingCompleteEventHandler? _onBindingCompleteHandler;

        // same deal about the new currentItemChanged event
        private protected EventHandler? _onCurrentItemChangedHandler;

        // Event handler for the DataError event
        private BindingManagerDataErrorEventHandler? _onDataErrorHandler;

        /// <summary>
        ///  Gets the collection of bindings managed by this binding manager.
        /// </summary>
        public BindingsCollection Bindings
        {
            get
            {
                if (_bindings is null)
                {
                    _bindings = new ListManagerBindingsCollection(this);

                    // Hook collection change events on collection, so we can hook or unhook the BindingComplete events on individual bindings
                    _bindings.CollectionChanging += OnBindingsCollectionChanging;
                    _bindings.CollectionChanged += OnBindingsCollectionChanged;
                }

                return _bindings;
            }
        }

        /// <summary>
        ///  Raises the <see cref="BindingComplete"/> event.
        /// </summary>
        /// <param name="args">The event data for the completed binding operation.</param>
        protected internal void OnBindingComplete(BindingCompleteEventArgs args)
        {
            _onBindingCompleteHandler?.Invoke(this, args);
        }

        /// <summary>
        ///  Raises the <see cref="CurrentChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected internal abstract void OnCurrentChanged(EventArgs e);

        /// <summary>
        ///  Raises the <see cref="CurrentItemChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected internal abstract void OnCurrentItemChanged(EventArgs e);

        /// <summary>
        ///  Raises the <see cref="DataError"/> event for a binding data transfer exception.
        /// </summary>
        /// <param name="e">The exception raised while moving data.</param>
        protected internal void OnDataError(Exception e)
        {
            _onDataErrorHandler?.Invoke(this, new BindingManagerDataErrorEventArgs(e));
        }

        /// <summary>
        ///  Gets the current item represented by this binding manager.
        /// </summary>
        public abstract object? Current { get; }

        private protected abstract void SetDataSource(object? dataSource);

        /// <summary>
        ///  Initializes a new instance of the <see cref="BindingManagerBase"/> class.
        /// </summary>
        public BindingManagerBase() { }

        internal BindingManagerBase(object? dataSource)
        {
            SetDataSource(dataSource);
        }

        internal abstract Type BindType { get; }

        internal abstract PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors);

        /// <summary>
        ///  Gets the property descriptors for items managed by this binding manager.
        /// </summary>
        /// <returns>The property descriptors for the current item type.</returns>
        public virtual PropertyDescriptorCollection GetItemProperties() => GetItemProperties(listAccessors: null);

        /// <summary>
        ///  Gets property descriptors after walking a chain of related list accessors.
        /// </summary>
        /// <param name="dataSources">The data sources already visited while resolving the chain.</param>
        /// <param name="listAccessors">The property descriptors that describe the related list path.</param>
        /// <returns>The property descriptors for the resolved item type, or <see langword="null"/>.</returns>
        protected internal virtual PropertyDescriptorCollection? GetItemProperties(ArrayList dataSources, ArrayList listAccessors)
        {
            IList? list = null;
            if (this is CurrencyManager currencyManager)
            {
                list = currencyManager.List;
            }

            if (list is ITypedList typedList)
            {
                PropertyDescriptor[] properties = new PropertyDescriptor[listAccessors.Count];
                listAccessors.CopyTo(properties, 0);
                return typedList.GetItemProperties(properties);
            }

            return GetItemProperties(BindType, 0, dataSources, listAccessors);
        }

        /// <summary>
        ///  Gets property descriptors for a list type after walking related list accessors.
        /// </summary>
        /// <param name="listType">The list or item type to inspect.</param>
        /// <param name="offset">The current accessor offset.</param>
        /// <param name="dataSources">The data sources already visited while resolving the chain.</param>
        /// <param name="listAccessors">The property descriptors that describe the related list path.</param>
        /// <returns>The property descriptors for the resolved item type, or <see langword="null"/>.</returns>
        protected virtual PropertyDescriptorCollection? GetItemProperties(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type listType,
            int offset,
            ArrayList dataSources,
            ArrayList listAccessors)
        {
            if (listAccessors.Count < offset)
            {
                return null;
            }

            if (listAccessors.Count == offset)
            {
                if (!typeof(IList).IsAssignableFrom(listType))
                {
                    return TypeDescriptor.GetProperties(listType);
                }

                foreach (PropertyInfo property in listType.GetProperties())
                {
                    if (property.Name == "Item" && property.PropertyType != typeof(object))
                    {
                        return TypeDescriptor.GetProperties(property.PropertyType, [new BrowsableAttribute(true)]);
                    }
                }

                // return the properties on the type of the first element in the list
                if (dataSources[offset - 1] is IList list && list.Count > 0)
                {
                    return TypeDescriptor.GetProperties(list[0]!);
                }

                return null;
            }

            if (typeof(IList).IsAssignableFrom(listType))
            {
                PropertyDescriptorCollection? itemProps = null;
                foreach (PropertyInfo property in listType.GetProperties())
                {
                    if (property.Name == "Item" && property.PropertyType != typeof(object))
                    {
                        // get all the properties that are not marked as Browsable(false)
                        itemProps = TypeDescriptor.GetProperties(property.PropertyType, [new BrowsableAttribute(true)]);
                    }
                }

                if (itemProps is null)
                {
                    // Use the properties on the type of the first element in the list
                    // if offset == 0, then this means that the first dataSource did not have a strongly typed Item property.
                    // the dataSources are added only for relatedCurrencyManagers, so in this particular case
                    // we need to use the dataSource in the currencyManager.
                    IList? list;
                    if (offset == 0)
                    {
                        list = DataSource as IList;
                    }
                    else
                    {
                        list = dataSources[offset - 1] as IList;
                    }

                    if (list is not null && list.Count > 0)
                    {
                        itemProps = TypeDescriptor.GetProperties(list[0]!);
                    }
                }

                if (itemProps is not null)
                {
                    for (int j = 0; j < itemProps.Count; j++)
                    {
                        if (itemProps[j].Equals(listAccessors[offset]))
                        {
                            return GetItemProperties(itemProps[j].PropertyType, offset + 1, dataSources, listAccessors);
                        }
                    }
                }
            }
            else
            {
                foreach (PropertyInfo property in listType.GetProperties())
                {
                    if (property.Name.Equals(((PropertyDescriptor)listAccessors[offset]!).Name))
                    {
                        return GetItemProperties(property.PropertyType, offset + 1, dataSources, listAccessors);
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///  Occurs when one of the bindings managed by this manager completes a data transfer.
        /// </summary>
        public event BindingCompleteEventHandler BindingComplete
        {
            add => _onBindingCompleteHandler += value;
            remove => _onBindingCompleteHandler -= value;
        }

        /// <summary>
        ///  Occurs when the current item changes.
        /// </summary>
        public event EventHandler CurrentChanged
        {
            add => onCurrentChangedHandler += value;
            remove => onCurrentChangedHandler -= value;
        }

        /// <summary>
        ///  Occurs when the current item reports that one of its properties changed.
        /// </summary>
        public event EventHandler CurrentItemChanged
        {
            add => _onCurrentItemChangedHandler += value;
            remove => _onCurrentItemChangedHandler -= value;
        }

        /// <summary>
        ///  Occurs when a binding managed by this manager raises a data transfer exception.
        /// </summary>
        public event BindingManagerDataErrorEventHandler DataError
        {
            add => _onDataErrorHandler += value;
            remove => _onDataErrorHandler -= value;
        }

        internal abstract string GetListName();

        /// <summary>
        ///  Cancels the pending edit on the current item, when the data source supports editing.
        /// </summary>
        public abstract void CancelCurrentEdit();

        /// <summary>
        ///  Commits the pending edit on the current item, when the data source supports editing.
        /// </summary>
        public abstract void EndCurrentEdit();

        /// <summary>
        ///  Adds a new item to the managed data source.
        /// </summary>
        public abstract void AddNew();

        /// <summary>
        ///  Removes the item at the specified index from the managed data source.
        /// </summary>
        /// <param name="index">The zero-based item index to remove.</param>
        public abstract void RemoveAt(int index);

        /// <summary>
        ///  Gets or sets the zero-based position of the current item.
        /// </summary>
        public abstract int Position { get; set; }

        /// <summary>
        ///  Occurs when <see cref="Position"/> changes.
        /// </summary>
        public event EventHandler PositionChanged
        {
            add => onPositionChangedHandler += value;
            remove => onPositionChangedHandler -= value;
        }

        /// <summary>
        ///  Recomputes whether this manager is actively binding data.
        /// </summary>
        protected abstract void UpdateIsBinding();

        /// <summary>
        ///  Gets the display name of the managed list after applying related list accessors.
        /// </summary>
        /// <param name="listAccessors">The property descriptors that describe the related list path.</param>
        /// <returns>The resolved list name.</returns>
        protected internal abstract string GetListName(ArrayList? listAccessors);

        /// <summary>
        ///  Suspends data binding for this manager.
        /// </summary>
        public abstract void SuspendBinding();

        /// <summary>
        ///  Resumes data binding for this manager.
        /// </summary>
        public abstract void ResumeBinding();

        /// <summary>
        ///  Pulls values from bound components into the current data item.
        /// </summary>
        protected void PullData() => PullData(out _);

        internal void PullData(out bool success)
        {
            success = true;
            _pullingData = true;

            try
            {
                UpdateIsBinding();

                int numLinks = Bindings.Count;
                for (int i = 0; i < numLinks; i++)
                {
                    if (Bindings[i].PullData())
                    {
                        success = false;
                    }
                }
            }
            finally
            {
                _pullingData = false;
            }
        }

        /// <summary>
        ///  Pushes values from the current data item into bound components.
        /// </summary>
        protected void PushData()
        {
            if (_pullingData)
            {
                return;
            }

            UpdateIsBinding();

            int numLinks = Bindings.Count;
            for (int i = 0; i < numLinks; i++)
            {
                Bindings[i].PushData();
            }
        }

        internal abstract object? DataSource { get; }

        internal abstract bool IsBinding { get; }

        /// <summary>
        ///  Gets a value indicating whether data binding is currently suspended.
        /// </summary>
        public bool IsBindingSuspended => !IsBinding;

        /// <summary>
        ///  Gets the number of items managed by this binding manager.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        ///  BindingComplete events on individual Bindings are propagated up through the BindingComplete event on
        ///  the owning BindingManagerBase. To do this, we have to track changes to the bindings collection, adding
        ///  or removing handlers on items in the collection as appropriate.
        ///
        ///  For the Add and Remove cases, we hook the collection 'changed' event, and add or remove handler for
        ///  specific binding.
        ///
        ///  For the Refresh case, we hook both the 'changing' and 'changed' events, removing handlers for all
        ///  items that were in the collection before the change, then adding handlers for whatever items are
        ///  in the collection after the change.
        /// </summary>
        private void OnBindingsCollectionChanged(object? sender, CollectionChangeEventArgs e)
        {
            if (e.Element is not Binding binding)
            {
                return;
            }

            switch (e.Action)
            {
                case CollectionChangeAction.Add:
                    binding.BindingComplete += Binding_BindingComplete;
                    break;
                case CollectionChangeAction.Remove:
                    binding.BindingComplete -= Binding_BindingComplete;
                    break;
                case CollectionChangeAction.Refresh:
                    foreach (Binding bi in Bindings)
                    {
                        bi.BindingComplete += Binding_BindingComplete;
                    }

                    break;
            }
        }

        private void OnBindingsCollectionChanging(object? sender, CollectionChangeEventArgs e)
        {
            if (e.Action != CollectionChangeAction.Refresh)
            {
                return;
            }

            foreach (Binding bi in Bindings)
            {
                bi.BindingComplete -= Binding_BindingComplete;
            }
        }

        private void Binding_BindingComplete(object? sender, BindingCompleteEventArgs args)
        {
            OnBindingComplete(args);
        }
    }
}
