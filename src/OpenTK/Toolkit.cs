﻿//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2013 the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using OpenTK.Platform;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTK
{
    /// <summary>
    /// Provides static methods to manage an OpenTK application.
    /// </summary>
    public sealed class Toolkit : IDisposable
    {
        private Factory platform_factory;
        private static Toolkit toolkit;

        private volatile static bool initialized;
        private static readonly object InitLock = new object();

        private Toolkit(Factory factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            platform_factory = factory;
        }

        /// <summary>
        /// Initializes OpenTK with default options.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You *must* call this method if you are combining OpenTK with a
        /// third-party windowing toolkit (e.g. GTK#). In this case, this should be the
        /// first method called by your application:
        /// <code>
        /// static void Main()
        /// {
        ///     using (OpenTK.Toolkit.Init())
        ///     {
        ///      ...
        ///     }
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// The reason is that some toolkits do not configure the underlying platform
        /// correctly or configure it in a way that is incompatible with OpenTK.
        /// Calling this method first ensures that OpenTK is given the chance to
        /// initialize itself and configure the platform correctly.
        /// </para>
        /// </remarks>
        /// <returns>
        /// An IDisposable instance that you can use to dispose of the resources
        /// consumed by OpenTK.
        /// </returns>
        public static Toolkit Init()
        {
            return Init(ToolkitOptions.Default);
        }

        /// <summary>
        /// Initializes OpenTK with the specified options. Use this method
        /// to influence the OpenTK.Platform implementation that will be used.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You *must* call this method if you are combining OpenTK with a
        /// third-party windowing toolkit (e.g. GTK#). In this case, this should be the
        /// first method called by your application:
        /// <code>
        /// static void Main()
        /// {
        ///     using (OpenTK.Toolkit.Init())
        ///     {
        ///      ...
        ///     }
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// The reason is that some toolkits do not configure the underlying platform
        /// correctly or configure it in a way that is incompatible with OpenTK.
        /// Calling this method first ensures that OpenTK is given the chance to
        /// initialize itself and configure the platform correctly.
        /// </para>
        /// </remarks>
        /// <param name="options">A <c>ToolkitOptions</c> instance
        /// containing the desired options.</param>
        /// <returns>
        /// An IDisposable instance that you can use to dispose of the resources
        /// consumed by OpenTK.
        /// </returns>
        public static Toolkit Init(ToolkitOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            lock (InitLock)
            {
                if (!initialized)
                {
                    initialized = true;
                    Configuration.Init(options);
                    Options = options;
                    if (Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        /*
                         * https://docs.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order
                         *
                         * If shipping an AnyCPU build and C++ DLLImports such as OpenALSoft / SDL
                         * we need to use architecture specific P/Invokes.
                         * Windows will attempt to locate an appropriate file using the search order listed
                         * in the document above. However, we want to avoid putting 'our' copy of these files
                         * into the system cache.
                         *
                         * Thus, a common convention is to use an x86 / x64 subfolder to store the architecture
                         * specific DLLImports. (Architecture independant files can be stored in the same
                         * folder as the main DLL)
                         *
                         * For this to work, we need to add the appropriate search path to SetDLLDirectory
                         *
                         * NOTE:
                         * Non-Windows platforms should be handled via the OpenTK.dll.config file as appropriate
                         */
                        Assembly entryAssembly = Assembly.GetEntryAssembly();
                        if (entryAssembly != null)
                        {
                            try
                            {
                                string assemblyLocation = entryAssembly.Location;
                                string path = Path.GetDirectoryName(assemblyLocation);
                                path = Path.Combine(path, IntPtr.Size == 4 ? "x86" : "x64");
                                bool ok = SetDllDirectory(path);
                                if (!ok)
                                {
                                    // A fairly fundamental Win32 syscall failed. Developer probably wants to know about this, but not necessarily users
                                    throw new System.ComponentModel.Win32Exception("Setting x86/x64 specific dll import directory failed.");
                                }
                            }
                            catch (Exception e)
                            {
#if DEBUG
                                throw;
#else
                                Trace.TraceWarning($"Exception when trying to set x86/x64 dll directory. {e}");
#endif
                            }
                        }
                        else
                        {
                            Trace.TraceWarning("Could not get assembly location, we will not set separate x86 and x64 dll import folders. This means you won't get architecture specific dll imports.");
                        }
                    }

                    // The actual initialization takes place in the
                    // platform-specific factory constructors.
                    toolkit = new Toolkit(new Factory());
                }
                return toolkit;
            }
        }

        internal static ToolkitOptions Options { get; private set; }

        /// <summary>
        /// Disposes of the resources consumed by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool manual)
        {
            if (manual)
            {
                lock (InitLock)
                {
                    if (initialized)
                    {
                        platform_factory.Dispose();
                        platform_factory = null;
                        toolkit = null;
                        initialized = false;
                    }
                }
            }
        }

#if DEBUG
        /// <summary>
        /// Finalizes this instance.
        /// </summary>
        ~Toolkit()
        {
            Debug.Print("[Warning] {0} leaked, did you forget to call Dispose()?");
            // We may not Dispose() the toolkit from the finalizer thread,
            // as that will crash on many operating systems.
        }
#endif
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
