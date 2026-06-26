using System;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class MaskedTextBoxPanel : Panel
    {
        public MaskedTextBoxPanel ()
        {
            Controls.Add (new Label { Left = 10, Top = 12, Width = 100, Text = "Phone" });
            Controls.Add (new MaskedTextBox ("(999) 000-0000") {
                Left = 120,
                Top = 10,
                Width = 180,
                Text = "5551234567"
            });

            Controls.Add (new Label { Left = 10, Top = 52, Width = 100, Text = "Date" });
            var date = Controls.Add (new MaskedTextBox ("00/00/0000") {
                Left = 120,
                Top = 50,
                Width = 180,
                ValidatingType = typeof (DateTime)
            });
            date.Text = DateTime.Today.ToString ("MMddyyyy");

            Controls.Add (new Label { Left = 10, Top = 92, Width = 100, Text = "Postal code" });
            Controls.Add (new MaskedTextBox ("00000-9999") {
                Left = 120,
                Top = 90,
                Width = 180,
                HidePromptOnLeave = true
            });

            Controls.Add (new Label { Left = 10, Top = 132, Width = 100, Text = "License" });
            Controls.Add (new MaskedTextBox (">AAAA-AAAA-AAAA") {
                Left = 120,
                Top = 130,
                Width = 180,
                TextMaskFormat = MaskFormat.IncludePromptAndLiterals
            });

            Controls.Add (new Label { Left = 10, Top = 172, Width = 100, Text = "Password" });
            Controls.Add (new MaskedTextBox ("0000") {
                Left = 120,
                Top = 170,
                Width = 180,
                PasswordChar = '*'
            });

            var status = Controls.Add (new Label {
                Left = 120,
                Top = 220,
                Width = 520,
                Text = "Try typing invalid characters into the fields above."
            });

            var rejecting = Controls.Add (new MaskedTextBox ("LL-0000") {
                Left = 120,
                Top = 250,
                Width = 180,
                RejectInputOnFirstFailure = true
            });

            rejecting.MaskInputRejected += (_, e) => {
                status.Text = $"Rejected at {e.Position}: {e.RejectionHint}";
            };

            date.TypeValidationCompleted += (_, e) => {
                status.Text = e.IsValidInput ? $"Date parsed: {e.ReturnValue:d}" : e.Message ?? "Date is incomplete.";
            };
        }
    }
}
