using ModernFormsNext.Layout;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Represents a collection of data bindings on a control.
    /// </summary>
    [DefaultEvent(nameof(CollectionChanged))]
    public class BindingsCollection : BaseCollection
    {
        private readonly List<Binding> _list = [];
        private CollectionChangeEventHandler? _onCollectionChanging;
        private CollectionChangeEventHandler? _onCollectionChanged;

        internal BindingsCollection()
        {
        }

        /// <summary>
        ///  Gets the number of bindings in the collection.
        /// </summary>
        public override int Count => _list.Count;

        /// <summary>
        ///  Gets the bindings in the collection as an object.
        /// </summary>
        protected override ArrayList List => ArrayList.Adapter(_list);

        /// <summary>
        ///  Gets the <see cref="Binding"/> at the specified index.
        /// </summary>
        public Binding this[int index] => _list[index]!;

        /// <summary>
        ///  Adds a binding to the collection and raises collection change events.
        /// </summary>
        /// <param name="binding">The binding to add.</param>
        protected internal void Add(Binding binding)
        {
            CollectionChangeEventArgs eventArgs = new(CollectionChangeAction.Add, binding);
            OnCollectionChanging(eventArgs);
            AddCore(binding);
            OnCollectionChanged(eventArgs);
        }

        /// <summary>
        ///  Adds a <see cref="Binding"/> to the collection.
        /// </summary>
        protected virtual void AddCore(Binding dataBinding)
        {
            ArgumentNullException.ThrowIfNull(dataBinding);
            _list.Add(dataBinding);
        }

        /// <summary>
        ///  Occurs when the collection is about to change.
        /// </summary>
        [SRDescription(nameof(SR.collectionChangingEventDescr))]
        public event CollectionChangeEventHandler? CollectionChanging
        {
            add => _onCollectionChanging += value;
            remove => _onCollectionChanging -= value;
        }

        /// <summary>
        ///  Occurs when the collection is changed.
        /// </summary>
        [SRDescription(nameof(SR.collectionChangedEventDescr))]
        public event CollectionChangeEventHandler? CollectionChanged
        {
            add => _onCollectionChanged += value;
            remove => _onCollectionChanged -= value;
        }

        /// <summary>
        ///  Removes all bindings from the collection and raises collection change events.
        /// </summary>
        protected internal void Clear()
        {
            CollectionChangeEventArgs eventArgs = new(CollectionChangeAction.Refresh, null);
            OnCollectionChanging(eventArgs);
            ClearCore();
            OnCollectionChanged(eventArgs);
        }

        /// <summary>
        ///  Clears the collection of any members.
        /// </summary>
        protected virtual void ClearCore() => _list.Clear();

        /// <summary>
        ///  Raises the <see cref="CollectionChanging"/> event.
        /// </summary>
        protected virtual void OnCollectionChanging(CollectionChangeEventArgs e)
        {
            _onCollectionChanging?.Invoke(this, e);
        }

        /// <summary>
        ///  Raises the <see cref="CollectionChanged"/> event.
        /// </summary>
        protected virtual void OnCollectionChanged(CollectionChangeEventArgs ccevent)
        {
            _onCollectionChanged?.Invoke(this, ccevent);
        }

        /// <summary>
        ///  Removes a binding from the collection and raises collection change events.
        /// </summary>
        /// <param name="binding">The binding to remove.</param>
        protected internal void Remove(Binding binding)
        {
            CollectionChangeEventArgs eventArgs = new(CollectionChangeAction.Remove, binding);
            OnCollectionChanging(eventArgs);
            RemoveCore(binding);
            OnCollectionChanged(eventArgs);
        }

        /// <summary>
        ///  Removes the binding at the specified index.
        /// </summary>
        /// <param name="index">The zero-based binding index.</param>
        protected internal void RemoveAt(int index) => Remove(this[index]);

        /// <summary>
        ///  Removes the specified <see cref="Binding"/> from the collection.
        /// </summary>
        protected virtual void RemoveCore(Binding dataBinding) => _list.Remove(dataBinding);

        /// <summary>
        ///  Gets whether the collection contains bindings that should be serialized.
        /// </summary>
        /// <returns><see langword="true"/> when the collection contains at least one binding.</returns>
        protected internal bool ShouldSerializeMyAll() => Count > 0;
    }
}
