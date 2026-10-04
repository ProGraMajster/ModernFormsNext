using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.DataBinding;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class ValidationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FocusDepartureCommitsDefaultOnValidationBinding(bool surface, bool formatting)
    {
        using var f = new Fixture(surface);
        f.Bind(formatting);
        f.A.Select();
        f.A.Text = "after";
        Assert.Equal("before", f.Model.Value);
        f.B.Select();
        Assert.Equal("after", f.Model.Value);
        Assert.Equal(1, f.Model.Writes);
        f.AssertOwner(f.B);
    }

    private sealed class Model
    {
        private string value = "before";
        public string Value { get => value; set { this.value = value; Writes++; OnWrite?.Invoke(); } }
        public int Writes { get; set; }
        public Action? OnWrite { get; set; }
        public int Number { get; set; } = 7;
        public string Second { get; set; } = "second";
        public bool Flag { get; set; } = true;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ModernFormsTestHost host = ModernFormsTestHost.Create();
        private readonly SkiaControlSurface? surface;
        private readonly TestWindowHost? window;
        internal Panel Root { get; } = new() { TabStop = false, BindingContext = new BindingContext() };
        internal TextBox A { get; } = new() { Bounds = new Rectangle(10, 10, 90, 30), TabIndex = 0 };
        internal TextBox B { get; } = new() { Bounds = new Rectangle(110, 10, 90, 30), TabIndex = 1 };
        internal TextBox C { get; } = new() { Bounds = new Rectangle(210, 10, 90, 30), TabIndex = 2 };
        internal Form Form { get; } = null!;
        internal Model Model { get; } = new();
        internal BindingSource Source { get; }
        internal Control? Owner => surface is not null ? surface.SelectedControl : window!.FocusedControl;
        internal ITextInputClient? Client => surface is not null ? surface.TextInputClient : Form.TextInputClient;

        internal Fixture(bool useSurface)
        {
            Source = new BindingSource { DataSource = new BindingList<Model> { Model } };
            Root.Controls.AddRange(A, B, C);
            if (useSurface)
            {
                surface = new SkiaControlSurface(Root);
                surface.Resize(400, 200);
            }
            else
            {
                Form = new Form { UseSystemDecorations = true };
                Root.Dock = DockStyle.Fill;
                Form.Controls.Add(Root);
                window = host.Show(Form);
            }
        }

        internal Binding Bind(bool formatting = true, DataSourceUpdateMode mode = DataSourceUpdateMode.OnValidation,
            string member = nameof(Model.Value))
            => A.DataBindings.Add(nameof(TextBox.Text), Source, member, formatting, mode);

        internal void AssertOwner(Control? expected)
        {
            Assert.Same(expected, Owner);
            foreach (Control control in new Control[] { A, B, C })
            {
                Assert.Equal(ReferenceEquals(control, expected), control.Selected);
                Assert.Equal(control.Selected, control.Focused);
            }
        }

        internal void Tab()
        {
            if (surface is null) window!.Input.Tab();
            else { surface.ProcessKeyDown(Keys.Tab); surface.ProcessKeyUp(Keys.Tab); }
        }

        internal void ClickB()
        {
            if (surface is null) window!.Input.Click(B);
            else
            {
                surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 120, 20);
                surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 120, 20);
            }
        }

        internal void ReleaseB()
        {
            if (surface is null) window!.Input.PointerUp(new Point(120, 20));
            else surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 120, 20);
        }

        public void Dispose()
        {
            surface?.Dispose();
            if (surface is not null) Root.Dispose();
            host.Dispose();
            Source.Dispose();
        }
    }
}
