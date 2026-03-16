using System;
using System.ComponentModel;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a timer that raises the <see cref="Tick"/> event
    /// at user-defined intervals on the UI thread.
    /// </summary>
    [DefaultProperty (nameof (Interval))]
    [DefaultEvent (nameof (Tick))]
    [ToolboxItemFilter ("ModernFormsNext")]
    public class Timer : Component
    {
        private DispatcherTimer dispatcherTimer;
        private int interval = 100;
        private bool enabled;
        private EventHandler onTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class.
        /// </summary>
        public Timer ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with the specified container.
        /// </summary>
        public Timer (IContainer container) : this ()
        {
            container?.Add (this);
        }

        /// <summary>
        /// Occurs when the timer interval has elapsed.
        /// </summary>
        public event EventHandler Tick {
            add => onTimer += value;
            remove => onTimer -= value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the timer is running.
        /// </summary>
        [DefaultValue (false)]
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value)
                    return;

                enabled = value;

                if (enabled)
                    StartTimer ();
                else
                    StopTimer ();
            }
        }

        /// <summary>
        /// Gets or sets the interval between timer ticks in milliseconds.
        /// </summary>
        [DefaultValue (100)]
        public int Interval {
            get => interval;
            set {
                if (value < 1)
                    throw new ArgumentOutOfRangeException (nameof (value));

                interval = value;

                if (dispatcherTimer != null) {
                    dispatcherTimer.Interval = TimeSpan.FromMilliseconds (interval);
                }
            }
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        public void Start () => Enabled = true;

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Stop () => Enabled = false;

        /// <summary>
        /// Raises the <see cref="Tick"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnTick (EventArgs e)
        {
            onTimer?.Invoke (this, e);
        }

        private void StartTimer ()
        {
            dispatcherTimer ??= new DispatcherTimer ();

            dispatcherTimer.Interval = TimeSpan.FromMilliseconds (interval);
            dispatcherTimer.Tick -= DispatcherTimer_Tick;
            dispatcherTimer.Tick += DispatcherTimer_Tick;

            dispatcherTimer.Start ();
        }

        private void StopTimer ()
        {
            if (dispatcherTimer != null) {
                dispatcherTimer.Stop ();
            }
        }

        private void DispatcherTimer_Tick (object sender, EventArgs e)
        {
            OnTick (EventArgs.Empty);
        }

        /// <summary>
        /// Releases the resources used by the <see cref="Timer"/>.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.
        /// </param>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                StopTimer ();

                if (dispatcherTimer != null) {
                    dispatcherTimer.Tick -= DispatcherTimer_Tick;
                    dispatcherTimer = null;
                }
            }

            base.Dispose (disposing);
        }

        /// <summary>
        /// Returns a string that represents the current timer.
        /// </summary>
        /// <returns>A string containing the type name and interval.</returns>
        public override string ToString ()
        {
            return $"{base.ToString ()}, Interval: {Interval}";
        }
    }
}
