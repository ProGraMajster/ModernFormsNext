using ModernFormsNext.Layout;
using ModernFormsNext.Accessibility;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ModernFormsNext.Help
{
    /// <summary>
    /// Provides contextual help metadata to controls.
    /// </summary>
    /// <remarks>
    /// <see cref="HelpProvider"/> follows the WinForms extender-provider pattern so migrated
    /// applications can associate help strings, keywords, and namespaces with individual controls.
    /// The shared implementation is platform-neutral; richer native help UI belongs in a backend
    /// service rather than in the shared framework assembly.
    /// </remarks>
    [SRDescription(nameof(SR.DescriptionHelpProvider))]
    public class HelpProvider : Component, IExtenderProvider
    {
        private readonly Dictionary<Control, string?> _helpStrings = [];
        private readonly Dictionary<Control, bool> _showHelp = [];
        private readonly List<Control> _boundControls = [];
        private readonly Dictionary<Control, string?> _keywords = [];
        private readonly Dictionary<Control, HelpNavigator> _navigators = [];

        /// <summary>
        ///  Initializes a new instance of the <see cref="HelpProvider"/> class.
        /// </summary>
        public HelpProvider()
        {
        }

        /// <summary>
        ///  Gets or sets a string indicating the name of the Help file associated with this
        ///  <see cref="HelpProvider"/> object.
        /// </summary>
        [Localizable(true)]
        [DefaultValue(null)]
        [SRDescription(nameof(SR.HelpProviderHelpNamespaceDescr))]
        public virtual string? HelpNamespace { get; set; }

        /// <summary>
        /// Gets or sets user-defined data associated with this help provider.
        /// </summary>
        [SRCategory(nameof(SR.CatData))]
        [Localizable(false)]
        [Bindable(true)]
        [SRDescription(nameof(SR.ControlTagDescr))]
        [DefaultValue(null)]
        [TypeConverter(typeof(StringConverter))]
        public object? Tag { get; set; }

        /// <summary>
        ///  Determines if the help provider can offer it's extender properties to the specified target
        ///  object.
        /// </summary>
        public virtual bool CanExtend(object? target) => target is Control;

        /// <summary>
        ///  Retrieves the Help Keyword displayed when the user invokes Help for the specified control.
        /// </summary>
        [DefaultValue(null)]
        [Localizable(true)]
        [SRDescription(nameof(SR.HelpProviderHelpKeywordDescr))]
        public virtual string? GetHelpKeyword(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);
            return _keywords.TryGetValue(ctl, out string? value) ? value : null;
        }

        /// <summary>
        ///  Retrieves the contents of the pop-up help window for the specified control.
        /// </summary>
        [DefaultValue(HelpNavigator.AssociateIndex)]
        [Localizable(true)]
        [SRDescription(nameof(SR.HelpProviderNavigatorDescr))]
        public virtual HelpNavigator GetHelpNavigator(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);
            return _navigators.TryGetValue(ctl, out HelpNavigator value) ? value : HelpNavigator.AssociateIndex;
        }

        /// <summary>
        ///  Retrieves the contents of the pop-up help window for the specified control.
        /// </summary>
        [DefaultValue(null)]
        [Localizable(true)]
        [SRDescription(nameof(SR.HelpProviderHelpStringDescr))]
        public virtual string? GetHelpString(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);
            return _helpStrings.TryGetValue(ctl, out string? value) ? value : null;
        }

        /// <summary>
        ///  Retrieves a value indicating whether Help displays for the specified control.
        /// </summary>
        [Localizable(true)]
        [SRDescription(nameof(SR.HelpProviderShowHelpDescr))]
        public virtual bool GetShowHelp(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);
            return _showHelp.TryGetValue(ctl, out bool value) && value;
        }

        /// <summary>
        ///  Handles the help event for any bound controls.
        /// </summary>
        private void OnControlHelp(object? sender, HelpEventArgs hevent)
        {
            if (sender is not Control ctl)
            {
                return;
            }

            string? helpString = GetHelpString(ctl);
            string? keyword = GetHelpKeyword(ctl);
            HelpNavigator navigator = GetHelpNavigator(ctl);

            if (!GetShowHelp(ctl))
            {
                return;
            }

            // If we have a help file, and help keyword we try F1 help next
            if (HelpNamespace is not null)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    Help.ShowHelp(ctl, HelpNamespace, navigator, keyword);
                }
                else
                {
                    Help.ShowHelp(ctl, HelpNamespace, navigator);
                }

                hevent.Handled = true;
                return;
            }

            // So at this point we don't have a help keyword, so try to display the whats this help
            if (!string.IsNullOrEmpty(helpString))
            {
                Help.ShowPopup(ctl, helpString, hevent.MousePos);
                hevent.Handled = true;
            }
        }

        /// <summary>
        ///  Handles the help event for any bound controls.
        /// </summary>
        private void OnQueryAccessibilityHelp(object? sender, QueryAccessibilityHelpEventArgs e)
        {
            if (sender is not Control ctl)
            {
                return;
            }

            e.HelpString = GetHelpString(ctl);
            e.HelpKeyword = GetHelpKeyword(ctl);
            e.HelpNamespace = HelpNamespace;
        }

        /// <summary>
        ///  Specifies a Help string associated with a control.
        /// </summary>
        public virtual void SetHelpString(Control ctl, string? helpString)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            _helpStrings[ctl] = helpString;
            if (!string.IsNullOrEmpty(helpString))
            {
                SetShowHelp(ctl, true);
            }

            UpdateEventBinding(ctl);
        }

        /// <summary>
        ///  Specifies the Help keyword to display when the user invokes Help for a control.
        /// </summary>
        public virtual void SetHelpKeyword(Control ctl, string? keyword)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            _keywords[ctl] = keyword;
            if (!string.IsNullOrEmpty(keyword))
            {
                SetShowHelp(ctl, true);
            }

            UpdateEventBinding(ctl);
        }

        /// <summary>
        ///  Specifies the Help keyword to display when the user invokes Help for a control.
        /// </summary>
        public virtual void SetHelpNavigator(Control ctl, HelpNavigator navigator)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            SourceGenerated.EnumValidator.Validate(navigator);

            _navigators[ctl] = navigator;
            SetShowHelp(ctl, true);
            UpdateEventBinding(ctl);
        }

        /// <summary>
        ///  Specifies whether Help is displayed for a given control.
        /// </summary>
        public virtual void SetShowHelp(Control ctl, bool value)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            _showHelp[ctl] = value;
            UpdateEventBinding(ctl);
        }

        /// <summary>
        ///  Used by the designer
        /// </summary>
        internal bool ShouldSerializeShowHelp(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            return _showHelp.ContainsKey(ctl);
        }

        /// <summary>
        ///  Used by the designer
        /// </summary>
        public virtual void ResetShowHelp(Control ctl)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            _showHelp.Remove(ctl);
        }

        /// <summary>
        ///  Binds/unbinds event handlers to ctl
        /// </summary>
        private void UpdateEventBinding(Control ctl)
        {
            bool showHelp = GetShowHelp(ctl);
            bool isBound = _boundControls.Contains(ctl);
            if (showHelp && !isBound)
            {
                ctl.HelpRequested += OnControlHelp;
                ctl.QueryAccessibilityHelp += OnQueryAccessibilityHelp;
                _boundControls.Add(ctl);
            }
            else if (!showHelp && isBound)
            {
                ctl.HelpRequested -= OnControlHelp;
                ctl.QueryAccessibilityHelp -= OnQueryAccessibilityHelp;
                _boundControls.Remove(ctl);
            }
        }

        /// <summary>
        /// Releases resources used by the provider and detaches from any controls it extended.
        /// </summary>
        /// <param name="disposing">A value indicating whether managed resources should be released.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Control control in _boundControls.ToArray())
                {
                    control.HelpRequested -= OnControlHelp;
                    control.QueryAccessibilityHelp -= OnQueryAccessibilityHelp;
                }

                _boundControls.Clear();
                _helpStrings.Clear();
                _showHelp.Clear();
                _keywords.Clear();
                _navigators.Clear();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        ///  Returns a string representation for this control.
        /// </summary>
        public override string ToString() => $"{base.ToString()}, HelpNamespace: {HelpNamespace}";
    }
}
