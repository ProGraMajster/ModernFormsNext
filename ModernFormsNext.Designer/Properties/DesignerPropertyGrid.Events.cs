using System.Drawing;
using ModernFormsNext.Designer.Services;

namespace ModernFormsNext.Designer.Properties;

internal sealed partial class DesignerPropertyGrid
{
    private readonly DesignerEventBindingService? eventBindings;
    private CancellationTokenSource? eventOperation;
    private CancellationTokenSource? eventInspection;
    private DesignerEventDescriptor? inspectedEvent;
    private ComboBox? eventPicker;
    private bool retiredEvents;

    internal void ActivateDefaultEvent()
    {
        CancelEdit();
        if (state.SelectDefaultEvent() is { } descriptor) ActivateEvent(descriptor);
    }

    private void ActivateEvent(DesignerEventDescriptor descriptor)
    {
        var existing = descriptor.GetValueText();
        BindEvent(descriptor, string.IsNullOrWhiteSpace(existing)
            ? DesignerPropertyGridState.CreateDefaultEventHandlerName(state.HeaderName, descriptor.Name) : existing,
            create: string.IsNullOrWhiteSpace(existing), navigate: true);
    }

    private async void BindEvent(DesignerEventDescriptor descriptor, string name, bool create, bool navigate)
    {
        if (retiredEvents || eventBindings is null) return;
        eventOperation?.Cancel();
        using var operation = new CancellationTokenSource();
        eventOperation = operation;
        try { await eventBindings.BindAsync(descriptor, name, create, navigate, operation.Token); }
        catch (Exception ex) { if (!retiredEvents) state.Session.Log($"Event editor failed: {ex.Message}"); }
        finally
        {
            if (ReferenceEquals(eventOperation, operation)) eventOperation = null;
            if (!retiredEvents) state.Refresh();
        }
    }

    private void AttachEventPicker(DesignerEventDescriptor descriptor, Rectangle bounds)
    {
        textEditor!.Width = Math.Max(1, bounds.Width - 32);
        eventPicker = new ComboBox { Left = bounds.Right - 30, Top = bounds.Top, Width = 30,
            Height = bounds.Height, Enabled = false, AccessibleName = "Choose compatible event handler" };
        eventPicker.Items.Add(new EventHandlerChoice("…", null));
        eventPicker.Items.Add(new EventHandlerChoice("Clear binding (keep user methods)", string.Empty));
        eventPicker.SelectedIndex = 0;
        var editor = eventPicker;
        editor.SelectedIndexChanged += (_, _) =>
        {
            if (!ReferenceEquals(editor, eventPicker) || editor.SelectedItem is not EventHandlerChoice { HandlerName: { } name }) return;
            CancelEdit();
            BindEvent(descriptor, name, create: false, navigate: name.Length != 0);
        };
        Controls.Add(eventPicker);
        LoadEventMethods(descriptor, eventPicker);
    }

    private async void LoadEventMethods(DesignerEventDescriptor descriptor, ComboBox editor)
    {
        if (fileService is null || eventPicker is null || retiredEvents) return;
        var document = state.Session.Document;
        var generation = state.Session.Transactions.RecordedChangeGeneration;
        using var operation = new CancellationTokenSource();
        try
        {
            eventInspection?.Cancel();
            eventInspection = operation;
            var methods = await fileService.ReadCompatibleEventHandlersAsync(document, descriptor.Parameters, operation.Token);
            if (retiredEvents || !ReferenceEquals(editor, eventPicker) || !ReferenceEquals(document, state.Session.Document)
                || generation != state.Session.Transactions.RecordedChangeGeneration || operation.IsCancellationRequested) return;
            // Reuse ComboBox's established popup ownership/disposal, keyboard and accessibility
            // paths. The selector becomes usable only after the bounded source snapshot is ready.
            foreach (var name in methods)
                editor.Items.Add(new EventHandlerChoice(name, name));
            editor.Enabled = true;
            if (methods.Count == 0) state.Session.Log("No compatible method exists in the paired user C# class. Type a new handler name and press Enter.");
            if (ReferenceEquals(eventInspection, operation)) eventInspection = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!retiredEvents) state.Session.Log($"Cannot choose an event handler: {ex.Message}"); }
        finally
        {
            if (ReferenceEquals(eventInspection, operation)) eventInspection = null;
            // Clearing a binding needs no source file and remains available after inspection fails.
            if (!retiredEvents && ReferenceEquals(editor, eventPicker)) editor.Enabled = true;
        }
    }

    private async void RefreshSelectedEventDiagnostic(bool force = false)
    {
        var descriptor = state.Mode == DesignerPropertyGridMode.Events ? state.SelectedEvent : null;
        if (retiredEvents || fileService is null || !force && ReferenceEquals(inspectedEvent, descriptor)) return;
        inspectedEvent = descriptor;
        eventInspection?.Cancel();
        eventInspection = null;
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.GetValueText())) return;
        using var operation = new CancellationTokenSource();
        eventInspection = operation;
        try
        {
            var result = await fileService.PrepareEventHandlerAsync(state.Session.Document, descriptor.GetValueText(),
                descriptor.Parameters, create: false, operation.Token);
            if (!retiredEvents && ReferenceEquals(inspectedEvent, descriptor) && !operation.IsCancellationRequested)
            {
                descriptor.Diagnostic = result.Plan.Succeeded ? null : result.Plan.Message;
                if (descriptor.Diagnostic is not null) state.Session.Log(descriptor.Diagnostic);
                Invalidate();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!retiredEvents && ReferenceEquals(inspectedEvent, descriptor))
            { descriptor.Diagnostic = $"Cannot inspect handler: {ex.Message}"; Invalidate(); }
        }
        finally { if (ReferenceEquals(eventInspection, operation)) eventInspection = null; }
    }

    private void RemoveEventPicker()
    {
        if (eventPicker is not null)
        {
            eventInspection?.Cancel();
            var owned = eventPicker;
            eventPicker = null;
            Controls.Remove(owned);
            owned.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !retiredEvents)
        {
            retiredEvents = true;
            eventOperation?.Cancel();
            eventInspection?.Cancel();
            RemoveEventPicker();
        }
        base.Dispose(disposing);
    }

    private sealed record EventHandlerChoice(string Text, string? HandlerName)
    {
        public override string ToString() => Text;
    }
}
