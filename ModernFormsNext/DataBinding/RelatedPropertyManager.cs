using ModernFormsNext.Layout;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ModernFormsNext.DataBinding
{
    internal class RelatedPropertyManager : PropertyManager
    {
        private BindingManagerBase _parentManager;
        private PropertyDescriptor _fieldInfo;

        internal RelatedPropertyManager(BindingManagerBase parentManager, string dataField)
            : base(GetCurrentOrNull(parentManager), dataField)
        {
            Bind(parentManager, dataField);
        }

        [MemberNotNull(nameof(_parentManager))]
        [MemberNotNull(nameof(_fieldInfo))]
        private void Bind(BindingManagerBase parentManager, string dataField)
        {
            Debug.Assert(parentManager is not null, "How could this be a null parentManager.");
            _parentManager = parentManager;
            _fieldInfo = parentManager.GetItemProperties().Find(dataField, true) ??
                throw new ArgumentException(string.Format(SR.RelatedListManagerChild, dataField));

            parentManager.CurrentItemChanged += ParentManager_CurrentItemChanged;
            Refresh();
        }

        internal override string GetListName()
        {
            string name = GetListName([]);
            if (name.Length > 0)
            {
                return name;
            }

            return base.GetListName();
        }

        protected internal override string GetListName(ArrayList? listAccessors)
        {
            if (listAccessors is null)
            {
                return string.Empty;
            }

            listAccessors.Insert(0, _fieldInfo);

            return _parentManager.GetListName(listAccessors);
        }

        internal override PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors)
        {
            PropertyDescriptor[] accessors;

            if (listAccessors is not null && listAccessors.Length > 0)
            {
                accessors = new PropertyDescriptor[listAccessors.Length + 1];
                listAccessors.CopyTo(accessors, 1);
            }
            else
            {
                accessors = new PropertyDescriptor[1];
            }

            // Set this accessor (add to the beginning)
            accessors[0] = _fieldInfo;

            // Get props
            return _parentManager.GetItemProperties(accessors);
        }

        private void ParentManager_CurrentItemChanged(object? sender, EventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            EndCurrentEdit();
            SetDataSource(GetCurrentOrNull(_parentManager));
            OnCurrentChanged(EventArgs.Empty);
        }

        internal override Type BindType => _fieldInfo.PropertyType;

        public override object? Current => (DataSource is not null) ? _fieldInfo.GetValue(DataSource) : null;

        private static object? GetCurrentOrNull(BindingManagerBase parentManager)
        {
            bool anyCurrent = (parentManager.Position >= 0 && parentManager.Position < parentManager.Count);
            return anyCurrent ? parentManager.Current : null;
        }
    }
}
