using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a PictureBox control.
    /// </summary>
    public class PictureBox : Control
    {
        private const int DefaultAnimationFrameDelay = 100;
        private const int MinimumAnimationFrameDelay = 10;

        private static HttpClient? client;

        private SKBitmap? image;
        private string? image_location;
        private PictureBoxSizeMode size_mode;
        private List<AnimatedImageFrame>? animated_frames;
        private int animated_frame_index;
        private int animation_repetition_count = -1;
        private int completed_animation_repetitions;
        private Timer? animation_timer;
        private int load_request_id;

        private sealed class AnimatedImageFrame
        {
            public AnimatedImageFrame (SKBitmap bitmap, int delay)
            {
                Bitmap = bitmap;
                Delay = delay;
            }

            public SKBitmap Bitmap { get; }

            public int Delay { get; }
        }

        private sealed class DecodedImage
        {
            public DecodedImage (SKBitmap? bitmap, List<AnimatedImageFrame>? frames, int repetitionCount)
            {
                Bitmap = bitmap;
                Frames = frames;
                RepetitionCount = repetitionCount;
            }

            public SKBitmap? Bitmap { get; }

            public List<AnimatedImageFrame>? Frames { get; }

            public int RepetitionCount { get; }
        }

        /// <summary>
        /// Initializes a new instance of the PictureBox class.
        /// </summary>
        public PictureBox ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        // Lazily initialize and cache an HttpClient if needed.
        private static HttpClient Client => client ??= new HttpClient ();

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 50);

        /// <summary>
        /// Gets or sets the image the PictureBox should display.
        /// </summary>
        /// <remarks>
        /// Setting this property directly displays a static bitmap and stops any animated
        /// image that was previously loaded through <see cref="ImageLocation"/> or
        /// <see cref="Load(string)"/>.
        ///
        /// When an animated GIF is loaded from a path or URL, this property returns the
        /// currently displayed frame.
        /// </remarks>
        public SKBitmap? Image {
            get => image;
            set {
                ClearAnimation ();
                SetDisplayedImage (value, updateSize: true);
            }
        }

        /// <summary>
        /// Gets or sets the path or URL of the image the PictureBox should display.
        /// </summary>
        /// <remarks>
        /// Static image formats are decoded into <see cref="Image"/>. Animated GIF files
        /// are decoded into cached frames and advanced on the UI thread by an internal
        /// timer. Loading happens asynchronously for URLs and reports failures through
        /// <see cref="IsErrored"/>.
        /// </remarks>
        public string? ImageLocation {
            get => image_location;
            set => LoadInternal (value);
        }

        /// <summary>
        /// Gets a value indicating the requested image could not be loaded.
        /// </summary>
        public bool IsErrored { get; private set; }

        /// <summary>
        /// Loads the image at the specified path or URL and sets ImageLocation to it.
        /// </summary>
        /// <remarks>
        /// The loaded image may be a static bitmap or an animated GIF. Animated GIFs are
        /// advanced automatically until another image is assigned or the control is
        /// disposed.
        /// </remarks>
        public void Load (string url)
        {
            if (string.IsNullOrWhiteSpace (url))
                throw new InvalidOperationException ("ImageLocation not specified.");

            ImageLocation = url;
        }

        // Load image from path or URL and display it.
        private async void LoadInternal (string? url)
        {
            if (image_location == url)
                return;

            var request_id = ++load_request_id;

            if (url is null) {
                image_location = null;
                ClearAnimation ();
                SetDisplayedImage (null, updateSize: true);
                return;
            }

            IsErrored = false;
            image_location = url;

            try {
                var bytes = await LoadImageBytesAsync (url);

                if (request_id != load_request_id)
                    return;

                ApplyDecodedImage (DecodeImage (bytes));
            } catch (Exception) {
                if (request_id != load_request_id)
                    return;

                IsErrored = true;
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicated the sizing mode used.
        /// </summary>
        public PictureBoxSizeMode SizeMode {
            get => size_mode;
            set {
                if (size_mode != value) {
                    size_mode = value;

                    //AutoSize = size_mode == PictureBoxSizeMode.AutoSize;
                    //SetAutoSizeMode (size_mode == PictureBoxSizeMode.AutoSize ? AutoSizeMode.GrowAndShrink : AutoSizeMode.GrowOnly);

                    UpdateSize ();

                    OnSizeModeChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Raised when the value of the SizeMode property changes.
        /// </summary>
        public event EventHandler? SizeModeChanged;

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the SizeModeChanged event.
        /// </summary>
        protected void OnSizeModeChanged (EventArgs e) => SizeModeChanged?.Invoke (this, e);

        private void AnimationTimer_Tick (object? sender, EventArgs e)
        {
            if (animated_frames is null || animated_frames.Count <= 1) {
                StopAnimationTimer ();
                return;
            }

            var next_frame = animated_frame_index + 1;

            if (next_frame >= animated_frames.Count) {
                completed_animation_repetitions++;

                if (animation_repetition_count >= 0 && completed_animation_repetitions > animation_repetition_count) {
                    StopAnimationTimer ();
                    return;
                }

                next_frame = 0;
            }

            animated_frame_index = next_frame;
            var frame = animated_frames[animated_frame_index];

            SetDisplayedImage (frame.Bitmap, updateSize: false);

            if (animation_timer is not null)
                animation_timer.Interval = frame.Delay;
        }

        private void ApplyDecodedImage (DecodedImage decoded)
        {
            ClearAnimation ();

            if (decoded.Frames is { Count: > 0 } frames) {
                animated_frames = frames;
                animated_frame_index = 0;
                animation_repetition_count = decoded.RepetitionCount;
                completed_animation_repetitions = 0;

                SetDisplayedImage (frames[0].Bitmap, updateSize: true);
                StartAnimationTimer ();
                return;
            }

            SetDisplayedImage (decoded.Bitmap, updateSize: true);
        }

        private void ClearAnimation ()
        {
            StopAnimationTimer ();

            if (animated_frames is not null) {
                foreach (var frame in animated_frames)
                    frame.Bitmap.Dispose ();

                animated_frames = null;
                image = null;
            }

            animated_frame_index = 0;
            animation_repetition_count = -1;
            completed_animation_repetitions = 0;
        }

        private static DecodedImage DecodeImage (byte[] bytes)
        {
            using var data = SKData.CreateCopy (bytes);
            using var codec = SKCodec.Create (data);

            if (codec is null)
                return new DecodedImage (SKBitmap.Decode (bytes), null, 0);

            if (codec.FrameCount > 1)
                return new DecodedImage (null, DecodeAnimationFrames (codec), codec.RepetitionCount);

            return new DecodedImage (SKBitmap.Decode (codec), null, 0);
        }

        private static List<AnimatedImageFrame> DecodeAnimationFrames (SKCodec codec)
        {
            var image_info = new SKImageInfo (codec.Info.Width, codec.Info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            var frame_infos = codec.FrameInfo;
            var frames = new List<AnimatedImageFrame> (codec.FrameCount);

            try {
                for (var i = 0; i < codec.FrameCount; i++) {
                    var bitmap = new SKBitmap (image_info);
                    bitmap.Erase (SKColors.Transparent);

                    // A prior frame of -1 lets Skia decode any required dependency frames.
                    // That keeps GIF disposal/blending behavior inside the codec instead
                    // of reimplementing it in the control.
                    var result = codec.GetPixels (image_info, bitmap.GetPixels (), new SKCodecOptions (i, -1));

                    if (result != SKCodecResult.Success) {
                        bitmap.Dispose ();
                        throw new InvalidOperationException ($"Could not decode animated image frame {i}. Skia result: {result}.");
                    }

                    frames.Add (new AnimatedImageFrame (bitmap, GetAnimationFrameDelay (frame_infos, i)));
                }
            } catch {
                foreach (var frame in frames)
                    frame.Bitmap.Dispose ();

                throw;
            }

            return frames;
        }

        private static int GetAnimationFrameDelay (SKCodecFrameInfo[] frameInfos, int index)
        {
            if (index < 0 || index >= frameInfos.Length)
                return DefaultAnimationFrameDelay;

            var duration = frameInfos[index].Duration;

            if (duration <= 0)
                return DefaultAnimationFrameDelay;

            return Math.Max (duration, MinimumAnimationFrameDelay);
        }

        private static async Task<byte[]> LoadImageBytesAsync (string url)
        {
            if (url.Contains ("://"))
                return await Client.GetByteArrayAsync (url);

            return await File.ReadAllBytesAsync (url);
        }

        private void SetDisplayedImage (SKBitmap? value, bool updateSize)
        {
            var changed = image != value;
            var was_errored = IsErrored;

            image = value;
            IsErrored = false;

            if (updateSize && changed)
                UpdateSize ();

            if (changed || was_errored)
                Invalidate ();
        }

        private void StartAnimationTimer ()
        {
            if (animated_frames is null || animated_frames.Count <= 1)
                return;

            if (animation_timer is null) {
                animation_timer = new Timer ();
                animation_timer.Tick += AnimationTimer_Tick;
            }

            animation_timer.Interval = animated_frames[0].Delay;
            animation_timer.Start ();
        }

        private void StopAnimationTimer ()
        {
            animation_timer?.Stop ();
        }

        // Trigger a resizing.
        private void UpdateSize ()
        {
            if (image == null)
                return;

            Parent?.PerformLayout (this, nameof (AutoSize));
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                ClearAnimation ();

                if (animation_timer is not null) {
                    animation_timer.Tick -= AnimationTimer_Tick;
                    animation_timer.Dispose ();
                    animation_timer = null;
                }
            }

            base.Dispose (disposing);
        }
    }
}
