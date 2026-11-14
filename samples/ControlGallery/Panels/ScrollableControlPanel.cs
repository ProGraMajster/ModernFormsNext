using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext;
using System.Drawing;

namespace ControlGallery.Panels
{
    /// <summary>Demonstrates wheel routing and committed viewport Scroll notifications.</summary>
    public class ScrollableControlPanel : Panel
    {
        /// <summary>Creates scrolling examples and a bounded event log outside their viewports.</summary>
        public ScrollableControlPanel ()
        {
            AutoScroll = true;
            Controls.Add(new Label { Bounds = new Rectangle(24, 12, 780, 36),
                Text = "Scroll by wheel, arrows, track or thumb. The log also follows programmatic changes and resets." });
            var sc = new ScrollableControl {
                Left = 100,
                Top = 100,
                Width = 200,
                Height = 200,
                AutoScroll = true
            };

            sc.Controls.AddRange (
                new Label { Left = 10, Top = 10, Width = 160, Height = 30, Text = "Wheel over label" },
                new TextBox { Left = 10, Top = 55, Width = 160, Height = 36, Text = "Wheel over input" },
                new CheckBox { Left = 10, Top = 110, Width = 160, Height = 36, Text = "Wheel over option" },
                CreateButton ("Wheel over button", 10, 165),
                CreateButton ("Overflow", 10, 300));

            Controls.Add (sc);

            var sc2 = new ScrollableControl {
                Left = 350,
                Top = 100,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            var nested = new ScrollableControl {
                Left = 10,
                Top = 10,
                Width = 185,
                Height = 110,
                AutoScroll = true
            };
            nested.Controls.AddRange (
                CreateButton ("Inner 1", 5, 5),
                CreateButton ("Inner 2", 5, 140),
                CreateButton ("Inner 3", 5, 275));
            sc2.Controls.AddRange (
                nested,
                new Label {
                    Left = 10,
                    Top = 340,
                    Width = 180,
                    Height = 50,
                    Multiline = true,
                    Text = "At the inner limit, keep wheeling to scroll the outer panel."
                });

            Controls.Add (sc2);

            var sc3 = new ScrollableControl {
                Left = 100,
                Top = 350,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            sc3.Controls.AddRange (
                CreateButton ("1", 0, 0),
                CreateButton ("2", 100, 0),
                CreateButton ("3", 200, 0),
                CreateButton ("4", 300, 0));

            Controls.Add (sc3);

            var sc4 = new ScrollableControl {
                Left = 350,
                Top = 350,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            sc4.Controls.Add (CreateButton ("1", 0, 0));

            Controls.Add (sc4);

            var log = Controls.Add(new Label { Bounds = new Rectangle(24, 570, 800, 150), Multiline = true,
                AccessibleAutomationId = "scroll-event-log", Text = "No viewport movement yet." });
            var entries = new Queue<string>();
            foreach (var (viewport, name) in new[] { (sc, "Vertical"), (sc2, "Outer"), (nested, "Inner"), (sc3, "Horizontal") })
                viewport.Scroll += (_, e) => {
                    entries.Enqueue($"{name}: {e.ScrollOrientation} {e.OldValue} -> {e.NewValue}, {e.Type}");
                    while (entries.Count > 5) entries.Dequeue();
                    log.Text = string.Join(Environment.NewLine, entries);
                };
            AddAction("Scroll V +40", 24, () => Advance(sc.VerticalScrollProperties));
            AddAction("Scroll H +40", 204, () => Advance(sc3.HorizontalScrollProperties));
            AddAction("Reset offsets", 384, () => {
                sc.VerticalScrollProperties.Value = sc.VerticalScrollProperties.Minimum;
                sc3.HorizontalScrollProperties.Value = sc3.HorizontalScrollProperties.Minimum;
            });
            AddAction("Toggle scrolling", 564, () => { sc.AutoScroll = !sc.AutoScroll; sc3.AutoScroll = sc.AutoScroll; });
            var reveal = Controls.Add(new Button { Bounds = new Rectangle(620, 106, 180, 32),
                Text = "Show last inner result" });
            var revealStatus = Controls.Add(new Label { Bounds = new Rectangle(620, 144, 185, 80), Multiline = true,
                AccessibleAutomationId = "scroll-reveal-status", Text = "The result is in the nested panel." });
            reveal.Click += (_, _) => {
                revealStatus.Text = sc2.ScrollControlIntoView(nested.Controls[^1])
                    ? "Last result revealed. Focus stays here."
                    : "The last result is unavailable.";
            };

            void AddAction(string text, int left, Action action)
            {
                var button = Controls.Add(new Button { Bounds = new Rectangle(left, 55, 170, 32), Text = text });
                button.Click += (_, _) => action();
            }
            static void Advance(ScrollProperties axis) => axis.Value = (int)Math.Min((long)axis.Value + 40, axis.Maximum);
        }

        private Button CreateButton (string text, int x, int y)
        {
            return new Button {
                Left = x,
                Top = y,
                Width = 160,
                Height = 110,
                Text = text
            };
        }
    }
}
