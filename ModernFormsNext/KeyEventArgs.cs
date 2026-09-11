// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext
{
    /// <summary>
    ///  Provides data for the KeyDown or KeyUp event.
    /// </summary>
    public class KeyEventArgs : EventArgs
    {
        private bool _suppressKeyPress = false;

        /// <summary>
        ///  Initializes a new instance of the KeyEventArgs class.
        /// </summary>
        public KeyEventArgs (Keys keyData)
        {
            KeyData = keyData;
        }

        /// <summary>Creates a framework key event from a platform-neutral WindowKit key and modifiers.</summary>
        /// <param name="key">The logical platform key; unsupported mappings produce <see cref="Keys.None"/>.</param>
        /// <param name="modifiers">Explicit Control, Shift, Alt, Meta and AltGraph flags.</param>
        /// <returns>A new caller-owned event with no handled or suppression state.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Unknown modifier bits were supplied.</exception>
        /// <remarks>
        /// Uses the same key mapper as framework windows. This factory performs no layout-dependent
        /// character translation, dispatch or focus lookup. AltGraph remains distinct from Control+Alt.
        /// The detached event may be created on any thread; dispatch requires the receiving UI thread.
        /// </remarks>
        /// <example><code>
        /// var e = KeyEventArgs.FromPlatformKey(Key.S, KeyModifiers.Control);
        /// bool handled = surface.TryProcessKeyDown(e);
        /// </code></example>
        public static KeyEventArgs FromPlatformKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            const KeyModifiers supported = KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt |
                KeyModifiers.Meta | KeyModifiers.AltGraph;
            if ((modifiers & ~supported) != 0)
                throw new ArgumentOutOfRangeException(nameof(modifiers));
            var mapped = WindowKitKeyMapper.ToFormsKey(key);
            if ((modifiers & KeyModifiers.Control) != 0) mapped |= Keys.Control;
            if ((modifiers & KeyModifiers.Shift) != 0) mapped |= Keys.Shift;
            if ((modifiers & KeyModifiers.Alt) != 0) mapped |= Keys.Alt;
            if ((modifiers & KeyModifiers.Meta) != 0) mapped |= Keys.Meta;
            if ((modifiers & KeyModifiers.AltGraph) != 0) mapped |= Keys.AltGraph;
            return new KeyEventArgs(mapped);
        }

        /// <summary>
        ///  Gets a value indicating whether the ALT key was pressed.
        /// </summary>
        public virtual bool Alt => (KeyData & Keys.Alt) == Keys.Alt;

        /// <summary>
        /// Gets a value indicating whether the AltGraph modifier was active.
        /// </summary>
        /// <remarks>
        /// AltGraph may be accompanied by Control and Alt flags on platforms such as Windows.
        /// Text input controls use this property to avoid treating that synthetic Control state
        /// as an editing shortcut.
        /// </remarks>
        public bool AltGraph => (KeyData & Keys.AltGraph) == Keys.AltGraph;

        /// <summary>
        ///  Gets a value indicating whether the CTRL key was pressed.
        /// </summary>
        public bool Control => (KeyData & Keys.Control) == Keys.Control;

        /// <summary>
        ///  Gets or sets a value indicating whether the event was handled.
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        ///  Gets the keyboard code for a KeyDown or KeyUp event.
        /// </summary>
        public Keys KeyCode {
            get {
                var keyGenerated = KeyData & Keys.KeyCode;

                // since Keys can be discontiguous, keeping Enum.IsDefined.
                if (!Enum.IsDefined (typeof (Keys), (int)keyGenerated)) {
                    return Keys.None;
                }
                return keyGenerated;
            }
        }

        /// <summary>
        ///  Gets the keyboard value for a <see cref="Control.KeyDown"/> or
        /// <see cref="Control.KeyUp"/> event.
        /// </summary>
        public int KeyValue => (int)(KeyData & Keys.KeyCode);

        /// <summary>
        ///  Gets the key data for a <see cref="Control.KeyDown"/> or
        /// <see cref="Control.KeyUp"/> event.
        /// </summary>
        public Keys KeyData { get; }

        /// <summary>
        ///  Gets the modifier flags for a <see cref="Control.KeyDown"/> or
        /// <see cref="Control.KeyUp"/> event.
        ///  This indicates which modifier keys (CTRL, SHIFT, and/or ALT) were pressed.
        /// </summary>
        public Keys Modifiers => KeyData & Keys.Modifiers;

        /// <summary>
        ///  Gets a value indicating whether the SHIFT key was pressed.
        /// </summary>
        public virtual bool Shift => (KeyData & Keys.Shift) == Keys.Shift;

        /// <summary>
        /// Gets whether Control represents an editing shortcut rather than an AltGraph sequence.
        /// </summary>
        internal bool IsShortcutControlPressed => Control && !AltGraph;

        /// <summary>
        /// Gets or sets a value indicating the key press should be suppressed.
        /// </summary>
        public bool SuppressKeyPress {
            get => _suppressKeyPress;
            set {
                _suppressKeyPress = value;
                Handled = value;
            }
        }

        internal static Keys FromInputModifiers (RawInputModifiers modifiers)
        {
            var keys = Keys.None;

            if (modifiers.HasFlag (RawInputModifiers.Alt))
                keys |= Keys.Alt;
            if (modifiers.HasFlag (RawInputModifiers.Control))
                keys |= Keys.Control;
            if (modifiers.HasFlag (RawInputModifiers.Shift))
                keys |= Keys.Shift;
            if (modifiers.HasFlag (RawInputModifiers.AltGraph))
                keys |= Keys.AltGraph;
            if (modifiers.HasFlag (RawInputModifiers.Meta))
                keys |= Keys.Meta;

            return keys;
        }
    }
}
