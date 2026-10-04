namespace ModernFormsNext;

internal sealed partial class ControlFocusScope
{
    private ValidationPass? validationPass;
    private ValidationPass? acceptedDeparture;

    private bool ValidateDeparture(Control? previous, Control? destination)
    {
        if (previous is null || destination is null || !destination.CausesValidation) return true;
        if (acceptedDeparture is { } accepted && ReferenceEquals(previous, accepted.Owner) &&
            ReferenceEquals(destination, accepted.Destination) && generation == accepted.Version + 1)
        {
            // One-use approval for the latest destination collected during the completed
            // departure validation. All owner writes still belong to Request's normal commit.
            acceptedDeparture = null;
            return true;
        }

        var pass = new ValidationPass(this, previous, destination, generation);
        validationPass = pass;
        bool valid;
        try { valid = previous.ValidateCore(pass.IsCurrent); }
        finally { validationPass = null; }
        if (!valid || !pass.IsCurrent()) return false;
        if (!pass.Redirected) return true;

        acceptedDeparture = pass;
        try { Request(pass.Destination); }
        finally { acceptedDeparture = null; }
        return false; // The original destination/request has been superseded.
    }

    // One pending destination, not an unbounded request queue. A cancel rejects the entire
    // departure; a forced retirement or immediate CausesValidation=false request invalidates it.
    private sealed class ValidationPass(ControlFocusScope scope, Control owner, Control destination, long version)
    {
        private Func<bool> destinationCurrent = destination.CaptureValidationTree();
        internal Control Owner { get; } = owner;
        internal Control Destination { get; private set; } = destination;
        internal long Version { get; private set; } = version;
        internal bool Redirected { get; private set; }

        internal void Redirect(Control target, long requestVersion)
        {
            Destination = target;
            destinationCurrent = target.CaptureValidationTree();
            Version = requestVersion;
            Redirected = true;
        }

        internal bool IsCurrent() => scope.generation == Version && ReferenceEquals(scope.Owner, Owner) &&
            scope.IsEligible(Owner) && scope.IsEligible(Destination) && destinationCurrent();
    }
}
