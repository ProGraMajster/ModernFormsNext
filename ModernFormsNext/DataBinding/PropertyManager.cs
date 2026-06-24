using ModernFormsNext.Layout;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Manages bindings for a single object or single object property.
    /// </summary>
    /// <remarks>
    ///  Unlike <see cref="CurrencyManager"/>, this manager does not navigate a list. Its
    ///  <see cref="Position"/> is always zero and <see cref="Count"/> is always one.
    /// </remarks>
    public class PropertyManager : BindingManagerBase
    {
        private object? _dataSource;
        private readonly string? _propName;
        private PropertyDescriptor? _propInfo;
        private bool _bound;

        /// <summary>
        ///  An object that represents the object to which the property belongs.
        /// </summary>
        public override object? Current => _dataSource;

        private void PropertyChanged(object? sender, EventArgs ea)
        {
            EndCurrentEdit();
            OnCurrentChanged(EventArgs.Empty);
        }

        private protected override void SetDataSource(object? dataSource)
        {
            if (_dataSource is not null && !string.IsNullOrEmpty(_propName))
            {
                _propInfo?.RemoveValueChanged(_dataSource, PropertyChanged);
                _propInfo = null;
            }

            _dataSource = dataSource;

            if (_dataSource is not null && !string.IsNullOrEmpty(_propName))
            {
                _propInfo = TypeDescriptor.GetProperties(_dataSource).Find(_propName, true) ??
                    throw new ArgumentException(string.Format(SR.PropertyManagerPropDoesNotExist, _propName, dataSource));
                _propInfo.AddValueChanged(_dataSource, PropertyChanged);
            }
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="PropertyManager"/> class.
        /// </summary>
        public PropertyManager()
        {
        }

        internal PropertyManager(object dataSource) : base(dataSource)
        {
        }

        internal PropertyManager(object? dataSource, string propName) : base()
        {
            _propName = propName;
            SetDataSource(dataSource);
        }

        internal override PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors)
        {
            return ListBindingHelper.GetListItemProperties(_dataSource, listAccessors);
        }

        internal override Type BindType => _dataSource!.GetType();

        internal override string GetListName()
        {
            return $"{TypeDescriptor.GetClassName(_dataSource!)}.{_propName}";
        }

        /// <summary>
        ///  Suspends binding and commits pending component data to the current object.
        /// </summary>
        public override void SuspendBinding()
        {
            EndCurrentEdit();
            if (_bound)
            {
                try
                {
                    _bound = false;
                    UpdateIsBinding();
                }
                catch
                {
                    _bound = true;
                    UpdateIsBinding();
                    throw;
                }
            }
        }

        /// <summary>
        ///  Resumes data binding.
        /// </summary>
        public override void ResumeBinding()
        {
            OnCurrentChanged(EventArgs.Empty);
            if (!_bound)
            {
                try
                {
                    _bound = true;
                    UpdateIsBinding();
                }
                catch
                {
                    _bound = false;
                    UpdateIsBinding();
                    throw;
                }
            }
        }

        /// <summary>
        ///  Gets the name of the list supplying the data for the binding.
        /// </summary>
        /// <returns>Always returns an empty string.</returns>
        protected internal override string GetListName(ArrayList? listAccessors) => string.Empty;

        /// <summary>
        ///  Cancels the current edit.
        /// </summary>
        public override void CancelCurrentEdit()
        {
            IEditableObject? obj = Current as IEditableObject;
            obj?.CancelEdit();
            PushData();
        }

        /// <summary>
        ///  Ends the current edit.
        /// </summary>
        public override void EndCurrentEdit()
        {
            PullData(out bool success);

            if (success)
            {
                IEditableObject? obj = Current as IEditableObject;
                obj?.EndEdit();
            }
        }

        /// <summary>
        ///  Updates every binding owned by this manager after its binding state changes.
        /// </summary>
        protected override void UpdateIsBinding()
        {
            for (int i = 0; i < Bindings.Count; i++)
            {
                Bindings[i].UpdateIsBinding();
            }
        }

        /// <summary>
        ///  Raises the <see cref="BindingManagerBase.CurrentChanged" /> event.
        /// </summary>
        /// <param name="ea">The event data.</param>
        protected internal override void OnCurrentChanged(EventArgs ea)
        {
            PushData();

            onCurrentChangedHandler?.Invoke(this, ea);
            _onCurrentItemChangedHandler?.Invoke(this, ea);
        }

        /// <summary>
        ///  Raises the <see cref="BindingManagerBase.CurrentItemChanged" /> event.
        /// </summary>
        /// <param name="ea">The event data.</param>
        protected internal override void OnCurrentItemChanged(EventArgs ea)
        {
            PushData();

            _onCurrentItemChangedHandler?.Invoke(this, ea);
        }

        internal override object? DataSource => _dataSource;

        internal override bool IsBinding => _dataSource is not null;

        /// <summary>
        ///  Gets the position in the underlying list that controls bound to this data source point to.
        /// </summary>
        /// <value>Always returns 0.</value>
        public override int Position
        {
            get => 0;
            set
            {
            }
        }

        /// <summary>
        ///  Gets the number of rows managed by the <see cref="BindingManagerBase" />.
        /// </summary>
        /// <value>Always returns 1.</value>
        public override int Count => 1;

        /// <summary>
        ///  Throws a <see cref="NotSupportedException" /> in all cases.
        /// </summary>
        /// <exception cref="NotSupportedException">In all cases.</exception>
        public override void AddNew()
        {
            throw new NotSupportedException(SR.DataBindingAddNewNotSupportedOnPropertyManager);
        }

        /// <summary>
        ///  Throws a <see cref="NotSupportedException" /> in all cases.
        /// </summary>
        /// <param name="index">The index of the row to delete.</param>
        /// <exception cref="NotSupportedException">In all cases.</exception>
        public override void RemoveAt(int index)
        {
            throw new NotSupportedException(SR.DataBindingRemoveAtNotSupportedOnPropertyManager);
        }
    }
}
