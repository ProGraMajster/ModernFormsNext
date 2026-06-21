using System;
using System.Collections.Generic;
using ModernFormsNext.WindowKit.Compatibility;
using ModernFormsNext.WindowKit.Controls.Platform;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit
{
    /// <summary>
    /// Provides a process-wide registry for platform services used by WindowKit infrastructure.
    /// </summary>
    /// <remarks>
    /// This compatibility service locator is used by backend-facing code to resolve services such
    /// as runtime platform, dispatcher, cursor factory, and clipboard implementations.
    /// </remarks>
    public static class AvaloniaGlobals
    {
        private static Dictionary<Type, object> services = new Dictionary<Type, object>();

        static AvaloniaGlobals()
        {
            var runtime = AddService<IRuntimePlatform> (new StandardRuntimePlatform());

            /*if (OperatingSystemEx.IsWindows())
                InitializeWindows();
            else if (OperatingSystemEx.IsLinux())
                InitializeLinux();
            else if (OperatingSystemEx.IsMacOS())
                InitializeOSX();
            else
                throw new InvalidOperationException("Unrecognized Operating System");*/
        }

        /// <summary>
        /// Registers a platform service implementation.
        /// </summary>
        /// <typeparam name="T">The service contract type.</typeparam>
        /// <param name="implementation">The service implementation to register.</param>
        /// <returns>The registered implementation.</returns>
        public static T AddService<T>(T implementation) where T : class
        {
            services.Add(typeof(T), implementation);

            return implementation;
        }

        /// <summary>
        /// Gets a required platform service implementation.
        /// </summary>
        /// <typeparam name="T">The service contract type.</typeparam>
        /// <returns>The registered service implementation.</returns>
        /// <exception cref="ApplicationException">Thrown when no service is registered for <typeparamref name="T"/>.</exception>
        public static T GetRequiredService<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var implementation))
                return (T)implementation;

            throw new ApplicationException($"Could not resolve service type {typeof(T)}");
        }

        /// <summary>
        /// Gets an optional platform service implementation.
        /// </summary>
        /// <typeparam name="T">The service contract type.</typeparam>
        /// <returns>The registered service implementation, or <see langword="null"/> when it is not registered.</returns>
        public static T? GetService<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var implementation))
                return (T)implementation;

            return null;
        }

        /*private static void InitializeLinux()
        {
            var x11 = new AvaloniaX11Platform();
            x11.Initialize(new X11PlatformOptions());

            AddService<IWindowingPlatform>(x11);
            AddService<IDispatcherImpl>(new X11PlatformThreading(x11));
            AddService<ICursorFactory>(new X11CursorFactory(x11.Display));
            AddService<IClipboard>(new X11Clipboard(x11));
        }

        private static void InitializeOSX()
        {
            var platform = Native.AvaloniaNativePlatform.Initialize();
            
            AddService<IWindowingPlatform>(platform);
            AddService<IDispatcherImpl>(new Native.DispatcherImpl(platform.Factory.CreatePlatformThreadingInterface()));
            AddService<ICursorFactory>(new Native.CursorFactory(platform.Factory.CreateCursorFactory()));
            AddService<IClipboard>(new Native.ClipboardImpl(platform.Factory.CreateClipboard()));
        }

        private static void InitializeWindows()
        {
            Win32Platform.Initialize();

            AddService<IWindowingPlatform>(Win32Platform.Instance);
            AddService<IDispatcherImpl>(Win32Platform.Instance._dispatcher);
            AddService<ICursorFactory>(CursorFactory.Instance);
            AddService<IClipboard>(new ClipboardImpl());
        }*/
    }

    /// <summary>
    /// Provides compatibility access to the global WindowKit service locator.
    /// </summary>
    public static class AvaloniaLocator
    {
        /// <summary>
        /// Gets the current compatibility locator instance.
        /// </summary>
        public static AvaloniaInstance Current = new AvaloniaInstance();

        /// <summary>
        /// Provides typed accessors for services registered in <see cref="AvaloniaGlobals"/>.
        /// </summary>
        public class AvaloniaInstance
        {
            /// <summary>
            /// Gets a required platform service implementation.
            /// </summary>
            /// <typeparam name="T">The service contract type.</typeparam>
            /// <returns>The registered service implementation.</returns>
            public T GetRequiredService<T>() where T : class 
                => AvaloniaGlobals.GetRequiredService<T>();

            /// <summary>
            /// Gets an optional platform service implementation.
            /// </summary>
            /// <typeparam name="T">The service contract type.</typeparam>
            /// <returns>The registered service implementation, or <see langword="null"/> when it is not registered.</returns>
            public T? GetService<T>() where T : class 
                => AvaloniaGlobals.GetService<T>();
        }
    }
}
