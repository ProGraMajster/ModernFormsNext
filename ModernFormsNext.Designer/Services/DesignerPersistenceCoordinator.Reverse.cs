using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

internal sealed partial class DesignerPersistenceCoordinator
{
    // Both entry points keep the parsed projection detached until an explicit action.
    internal void ReviewDesignerCode(string path)
    {
        ThrowIfDisposed();
        if (session.ActiveOpenDocument is not { } document || !documentStates.TryGetValue(document, out var state))
            return;
        if (state.Notice?.Kind == DesignerPersistenceNoticeKind.ExternalDesignConflict ||
            state.PendingDesignMissing || state.Recovery is not null)
        {
            session.Log("Resolve the design document conflict or recovery copy before importing code.");
            return;
        }

        try
        {
            var observed = ObserveFile(IOPath.GetFullPath(path), parseDesign: false, document.Document.RootKind);
            bool canonical = document.Path is not null &&
                PathsEqual(observed.Path, files.GetGeneratedCodePath(document.Document, document.Path));
            StageReviewedCode(state, observed, canonical);
            RaiseStateChanged();
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            session.Log("Code import failed: " + exception.Message);
        }
    }

    private void StageReviewedCode(DocumentState state, ObservedFile source, bool canonical)
    {
        state.PendingGeneratedSource = source;
        state.PendingGeneratedCanonical = canonical;
        state.PendingDiskGeneratedHash = canonical ? source.Hash : null;
        state.PendingDiskLastWriteUtc = source.LastWriteUtc;
        state.ReviewedGeneration = state.Document.RevisionGeneration;
        state.ReviewedRevision = state.Document.History.CurrentRevision;

        var complete = source is { Exists: true, Hash: not null, Error: null, ParsedGeneratedDocument: not null };
        var actions = DesignerPersistenceActions.Compare | DesignerPersistenceActions.SaveAs;
        if (complete)
            actions |= DesignerPersistenceActions.ApplyCode | DesignerPersistenceActions.Keep;
        else if (!canonical)
            actions |= DesignerPersistenceActions.Dismiss;

        state.Notice = new DesignerPersistenceNotification(
            Guid.NewGuid(),
            complete ? DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict : DesignerPersistenceNoticeKind.ExternalGeneratedCodeInvalid,
            complete ? "Review generated Designer code" : "Generated code could not be imported",
            complete
                ? "Compare the source and choose Apply Code to apply supported values, Keep Designer, or Save As."
                : "The Designer kept the current document. Correct the source and review it again, or save to a different file. "
                    + (source.Error ?? "The source is missing or unreadable."),
            state.Document.DisplayName, actions, DiskTimestampUtc: source.LastWriteUtc);
    }

    private bool VerifyReviewedSource(DocumentState state, ObservedFile source, out string? error)
    {
        var read = stableFileReader.Read(source.Path);
        if (source.Hash is not null && read is { Exists: true, Error: null } &&
            string.Equals(source.Hash, read.Hash, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        // An acknowledgement belongs to one fingerprint. A newer source gets a new notice.
        StageReviewedCode(state, ObserveFile(source.Path, parseDesign: false, state.Document.Document.RootKind),
            state.PendingGeneratedCanonical);
        RaiseStateChanged();
        error = "The source changed since review. Compare the new version before choosing an action.";
        return false;
    }

    private bool ApplyReviewedCode(DocumentState state, out string? error)
    {
        error = null;
        var document = state.Document;
        if (!ReferenceEquals(session.ActiveOpenDocument, document) ||
            state.PendingGeneratedSource is not { ParsedGeneratedDocument: { } parsed } source ||
            session.Transactions.HasActiveTransaction)
        {
            error = "Activate the document and finish its current edit before applying reviewed code.";
            return false;
        }

        try
        {
            if (state.ReviewedGeneration != document.RevisionGeneration ||
                state.ReviewedRevision != document.History.CurrentRevision)
            {
                StageReviewedCode(state, source, state.PendingGeneratedCanonical);
                RaiseStateChanged();
                error = "The Designer model changed since review. Compare the versions again.";
                return false;
            }
            if (!VerifyReviewedDesign(state))
            {
                CheckForExternalChanges();
                error = "The authoritative design file changed. Review that change before applying code.";
                return false;
            }
            if (!VerifyReviewedSource(state, source, out error)) return false;

            var current = document.Document;
            DesignDocument updated;
            if (document.Path is null && !document.IsDirty && current.Controls.Count == 0 &&
                current.Properties.Count == 0 && current.Events.Count == 0)
            {
                updated = parsed;
            }
            else
            {
                var baseline = files.ProjectDesignerCode(current);
                if (!baseline.Success || baseline.Document is null ||
                    !DesignerReverseReconciliation.TryApply(current, baseline.Document, parsed, out updated))
                {
                    error = "The source changes the design structure or omits existing assignments. Edit those changes in the Designer or use a new design.";
                    return false;
                }
            }

            var roundTrip = new CSharpDesignerRoundTripService();
            bool changed = !string.Equals(roundTrip.ComputeDesignHash(current),
                roundTrip.ComputeDesignHash(updated), StringComparison.Ordinal);
            if (changed && !EnsureRecoveryNow(document, out error)) return false;
            CancelExternalSchedule(state);
            if (!VerifyReviewedDesign(state))
            {
                CheckForExternalChanges();
                error = "The authoritative design file changed during review.";
                return false;
            }
            if (!VerifyReviewedSource(state, source, out error)) return false;
            if (changed)
                session.ReplaceDocument(updated, "Apply reviewed Designer code");
            if (state.PendingGeneratedCanonical)
            {
                state.KnownGeneratedHash = source.Hash;
                state.ExpectedGeneratedHash = null;
                state.ReviewedGeneratedHash = source.Hash;
            }
            ClearPendingExternalState(state);
            RaiseStateChanged();
            return true;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            error = "The reviewed code could not be applied: " + exception.Message;
            return false;
        }
    }

    private bool VerifyReviewedDesign(DocumentState state)
    {
        if (state.Document.Path is not { } path) return true;
        var read = stableFileReader.Read(path);
        return read.Error is null && (read.Exists
            ? read.Hash is not null && string.Equals(read.Hash, state.KnownDesignHash, StringComparison.OrdinalIgnoreCase)
            : state.KnownDesignHash is null);
    }

    private static string FormatReverseDiagnostics(CSharpDesignerParseResult parse)
        => string.Join(Environment.NewLine, parse.Diagnostics.Select(diagnostic =>
            diagnostic.Line is { } line
                ? $"Line {line}, column {diagnostic.Column} to {diagnostic.EndLine ?? line}:{diagnostic.EndColumn ?? diagnostic.Column}. {diagnostic.Message}"
                : diagnostic.Message));

    private string DescribeReviewedCode(DocumentState state)
    {
        if (state.PendingGeneratedSource is not { } source) return state.Notice?.Message ?? string.Empty;
        if (source.Error is { } error) return error;
        if (source.ParsedGeneratedDocument is not { } parsed) return state.Notice?.Message ?? string.Empty;
        if (state.ReviewedGeneration != state.Document.RevisionGeneration ||
            state.ReviewedRevision != state.Document.History.CurrentRevision)
            return "The Designer model changed since this review. Check the source again before applying it.";
        try
        {
            var changes = new List<string>();
            var current = state.Document.Document;
            var baseline = files.ProjectDesignerCode(current);
            if (!baseline.Success || baseline.Document is null ||
                !DesignerReverseReconciliation.TryApply(current, baseline.Document, parsed, out _, changes))
                return "This source cannot be merged as a value edit. Review structure and missing assignments in the source editor.";
            return changes.Count == 0
                ? "No supported model values changed."
                : "Affected values (up to 40)" + Environment.NewLine + string.Join(Environment.NewLine, changes);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            return "The comparison could not be prepared. " + exception.Message;
        }
    }

    private bool TryPrepareGeneratedWrite(DocumentState state, string designPath, DesignerGenerationFileResult proposed,
        bool canonical, out DesignerGenerationFileResult write, out bool ownsWholeFile, out string? error)
    {
        write = proposed;
        ownsWholeFile = false;
        error = null;
        var read = stableFileReader.Read(proposed.Path);
        if (read.Error is not null ||
            canonical && (read.Exists
                ? read.Hash is null || !string.Equals(read.Hash, state.KnownGeneratedHash, StringComparison.OrdinalIgnoreCase)
                : state.KnownGeneratedHash is not null))
        {
            error = "The generated file changed during write preparation. Check external changes and try again.";
            return false;
        }
        if (!read.Exists)
        {
            ownsWholeFile = true;
            return true;
        }
        if (!canonical || read.Text is null)
        {
            error = "Choose a destination without an existing generated file.";
            return false;
        }
        if (read.Text == proposed.Code ||
            read.Hash is not null && string.Equals(read.Hash, state.OwnedGeneratedHash, StringComparison.OrdinalIgnoreCase))
        {
            ownsWholeFile = true;
            return true;
        }

        var disk = ObserveFile(designPath, parseDesign: true, state.Document.Document.RootKind);
        if (disk.Error is not null || disk.DesignDocument is null ||
            IsUnexpectedDiskState(disk, state.KnownDesignHash, state.ExpectedDesignHash))
        {
            error = "The authoritative design file changed or is unavailable. Check external changes and try again.";
            return false;
        }
        var baseline = files.PrepareDesignerCode(disk.DesignDocument, designPath);
        if (baseline.Succeeded && read.Text == baseline.Code)
        {
            ownsWholeFile = true;
            return true;
        }

        var parsed = files.ImportDesignerCodeText(read.Text,
            new CSharpDesignerParseOptions { RootKind = state.Document.Document.RootKind });
        if (!parsed.Success)
        {
            error = "Resolve the generated source diagnostics before replacing it.";
            StageReviewedCode(state, new ObservedFile(read.Path, true, read.Hash, read.LastWriteUtc,
                null, null, Retryable: false, FormatReverseDiagnostics(parsed)), canonical: true);
            RaiseStateChanged();
            return false;
        }
        bool reviewed = read.Hash is not null &&
            string.Equals(read.Hash, state.ReviewedGeneratedHash, StringComparison.OrdinalIgnoreCase);
        if (!baseline.Succeeded ||
            !DesignerGeneratedSourceRewrite.TryPreserve(read.Text, baseline.Code, proposed.Code, reviewed, out var preserved))
        {
            error = "The generated file differs from the replaceable region. Review it before saving, or use Save As.";
            if (!reviewed)
            {
                StageReviewedCode(state, new ObservedFile(read.Path, true, read.Hash, read.LastWriteUtc,
                    null, parsed.Document, Retryable: false, Error: null), canonical: true);
                RaiseStateChanged();
            }
            return false;
        }

        write = new DesignerGenerationFileResult(true, proposed.Path, preserved, []);
        return true;
    }
}
