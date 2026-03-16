using System;
using System.Collections.Generic;
using ModernFormsNext.WindowKit.Compatibility;
using ModernFormsNext.WindowKit.Controls.Platform;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit
{
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

        public static T AddService<T>(T implementation) where T : class
        {
            services.Add(typeof(T), implementation);

            return implementation;
        }

        public static T GetRequiredService<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var implementation))
                return (T)implementation;

            throw new ApplicationException($"Could not resolve service type {typeof(T)}");
        }

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

    public static class AvaloniaLocator
    {
        public static AvaloniaInstance Current = new AvaloniaInstance();

        public class AvaloniaInstance
        {
            public T GetRequiredService<T>() where T : class 
                => AvaloniaGlobals.GetRequiredService<T>();

            public T? GetService<T>() where T : class 
                => AvaloniaGlobals.GetService<T>();
        }
    }
}
