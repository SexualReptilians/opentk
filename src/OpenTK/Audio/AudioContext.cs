﻿//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library.
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
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;

namespace OpenTK.Audio
{
    /// <summary>
    /// Provides methods to instantiate, use and destroy an audio context for playback.
    /// Static methods are provided to list available devices known by the driver.
    /// </summary>
    public sealed class AudioContext : IDisposable
    {
        private bool disposed;
        private bool is_processing, is_synchronized;
        private ContextHandle context_handle;
        private bool context_exists;

        private string device_name;
        private static object audio_context_lock = new object();
        private static Dictionary<ContextHandle, AudioContext> available_contexts = new Dictionary<ContextHandle, AudioContext>();

        /// \internal
        /// <summary>
        /// Runs before the actual class constructor, to load available devices.
        /// </summary>
        static AudioContext()
        {
            if (AudioDeviceEnumerator.IsOpenALSupported) // forces enumeration
            { }
        }

        /// <summary>Constructs a new AudioContext, using the default audio device.</summary>
        public AudioContext()
            : this(null, 0, 0, false, true, MaxAuxiliarySends.UseDriverDefault) { }

        /// <summary>
        /// Constructs a new AudioContext instance.
        /// </summary>
        /// <param name="device">The device name that will host this instance.</param>
        public AudioContext(string device) : this(device, 0, 0, false, true, MaxAuxiliarySends.UseDriverDefault) { }

        /// <summary>Constructs a new AudioContext, using the specified audio device and device parameters.</summary>
        /// <param name="device">The name of the audio device to use.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <remarks>
        /// Use AudioContext.AvailableDevices to obtain a list of all available audio devices.
        /// devices.
        /// </remarks>
        public AudioContext(string device, int freq) : this(device, freq, 0, false, true, MaxAuxiliarySends.UseDriverDefault) { }

        /// <summary>Constructs a new AudioContext, using the specified audio device and device parameters.</summary>
        /// <param name="device">The name of the audio device to use.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <remarks>
        /// Use AudioContext.AvailableDevices to obtain a list of all available audio devices.
        /// devices.
        /// </remarks>
        public AudioContext(string device, int freq, int refresh)
            : this(device, freq, refresh, false, true, MaxAuxiliarySends.UseDriverDefault) { }

        /// <summary>Constructs a new AudioContext, using the specified audio device and device parameters.</summary>
        /// <param name="device">The name of the audio device to use.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="sync">Flag, indicating a synchronous context. Default is false.</param>
        /// <remarks>
        /// Use AudioContext.AvailableDevices to obtain a list of all available audio devices.
        /// devices.
        /// </remarks>
        public AudioContext(string device, int freq, int refresh, bool sync)
            : this(AudioDeviceEnumerator.AvailablePlaybackDevices[0], freq, refresh, sync, true) { }

        /// <summary>Creates the audio context using the specified device and device parameters.</summary>
        /// <param name="device">The device descriptor obtained through AudioContext.AvailableDevices.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="sync">Flag, indicating a synchronous context. Default is false.</param>
        /// <param name="enableEfx">Indicates whether the EFX extension should be initialized, if present. Default is true.</param>
        /// <exception cref="ArgumentNullException">Occurs when the device string is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Occurs when a specified parameter is invalid.</exception>
        /// <exception cref="AudioDeviceException">
        /// Occurs when the specified device is not available, or is in use by another program.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when an audio context could not be created with the specified parameters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Occurs when an AudioContext already exists.</exception>
        /// <remarks>
        /// <para>For maximum compatibility, you are strongly recommended to use the default constructor.</para>
        /// <para>Multiple AudioContexts are not supported at this point.</para>
        /// <para>
        /// The number of auxilliary EFX sends depends on the audio hardware and drivers. Most Realtek devices, as well
        /// as the Creative SB Live!, support 1 auxilliary send. Creative's Audigy and X-Fi series support 4 sends.
        /// Values higher than supported will be clamped by the driver.
        /// </para>
        /// </remarks>
        public AudioContext(string device, int freq, int refresh, bool sync, bool enableEfx)
        {
            CreateContext(device, freq, refresh, sync, -1, -1, enableEfx, MaxAuxiliarySends.UseDriverDefault);
        }

        /// <summary>Creates the audio context using the specified device and device parameters.</summary>
        /// <param name="device">The device descriptor obtained through AudioContext.AvailableDevices.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="sync">Flag, indicating a synchronous context. Default is false.</param>
        /// <param name="enableEfx">Indicates whether the EFX extension should be initialized, if present. Default is true.</param>
        /// <param name="efxMaxAuxSends">Requires EFX enabled. The number of desired Auxiliary Sends per source.</param>
        /// <exception cref="ArgumentNullException">Occurs when the device string is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Occurs when a specified parameter is invalid.</exception>
        /// <exception cref="AudioDeviceException">
        /// Occurs when the specified device is not available, or is in use by another program.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when an audio context could not be created with the specified parameters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Occurs when an AudioContext already exists.</exception>
        /// <remarks>
        /// <para>For maximum compatibility, you are strongly recommended to use the default constructor.</para>
        /// <para>Multiple AudioContexts are not supported at this point.</para>
        /// <para>
        /// The number of auxilliary EFX sends depends on the audio hardware and drivers. Most Realtek devices, as well
        /// as the Creative SB Live!, support 1 auxilliary send. Creative's Audigy and X-Fi series support 4 sends.
        /// Values higher than supported will be clamped by the driver.
        /// </para>
        /// </remarks>
        public AudioContext(string device, int freq, int refresh, bool sync, bool enableEfx, MaxAuxiliarySends efxMaxAuxSends)
        {
            CreateContext(device, freq, refresh, sync, -1, -1, enableEfx, efxMaxAuxSends);
        }

        /// <summary>Creates the audio context using the specified device and device parameters.</summary>
        /// <param name="device">The device descriptor obtained through AudioContext.AvailableDevices.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="sync">Flag, indicating a synchronous context. Default is false.</param>
        /// <param name="monoSources">Number of mono sources. Pass -1 for driver default.</param>
        /// <param name="stereoSources">Number of stereo sources. Pass -1 for driver default.</param>
        /// <param name="enableEfx">Indicates whether the EFX extension should be initialized, if present. Default is true.</param>
        /// <param name="efxMaxAuxSends">Requires EFX enabled. The number of desired Auxiliary Sends per source.</param>
        /// <exception cref="ArgumentNullException">Occurs when the device string is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Occurs when a specified parameter is invalid.</exception>
        /// <exception cref="AudioDeviceException">
        /// Occurs when the specified device is not available, or is in use by another program.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when an audio context could not be created with the specified parameters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Occurs when an AudioContext already exists.</exception>
        /// <remarks>
        /// <para>For maximum compatibility, you are strongly recommended to use the default constructor.</para>
        /// <para>Multiple AudioContexts are not supported at this point.</para>
        /// <para>
        /// The number of auxilliary EFX sends depends on the audio hardware and drivers. Most Realtek devices, as well
        /// as the Creative SB Live!, support 1 auxilliary send. Creative's Audigy and X-Fi series support 4 sends.
        /// Values higher than supported will be clamped by the driver.
        /// </para>
        /// </remarks>
        public AudioContext(string device, int freq, int refresh, bool sync, int monoSources, int stereoSources, bool enableEfx, MaxAuxiliarySends efxMaxAuxSends)
        {
            CreateContext(device, freq, refresh, sync, monoSources, stereoSources, enableEfx, efxMaxAuxSends);
        }

        /// <summary>May be passed at context construction time to indicate the number of desired auxiliary effect slot sends per source.</summary>
        public enum MaxAuxiliarySends:int
        {
            /// <summary>Will chose a reliably working parameter.</summary>
            UseDriverDefault = 0,
            /// <summary>One send per source.</summary>
            One = 1,
            /// <summary>Two sends per source.</summary>
            Two = 2,
            /// <summary>Three sends per source.</summary>
            Three = 3,
            /// <summary>Four sends per source.</summary>
            Four = 4,
        }

        /// \internal
        /// <summary>Creates the audio context using the specified device.</summary>
        /// <param name="device">The device descriptor obtained through AudioContext.AvailableDevices, or null for the default device.</param>
        /// <param name="freq">Frequency for mixing output buffer, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="refresh">Refresh intervals, in units of Hz. Pass 0 for driver default.</param>
        /// <param name="sync">Flag, indicating a synchronous context. Default is false.</param>
        /// <param name="monoSources">Number of mono sources. Pass -1 for driver default.</param>
        /// <param name="stereoSources">Number of stereo sources. Pass -1 for driver default.</param>
        /// <param name="enableEfx">Indicates whether the EFX extension should be initialized, if present. Default is true.</param>
        /// <param name="efxAuxiliarySends">Requires EFX enabled. The number of desired Auxiliary Sends per source.</param>
        /// <exception cref="ArgumentOutOfRangeException">Occurs when a specified parameter is invalid.</exception>
        /// <exception cref="AudioDeviceException">
        /// Occurs when the specified device is not available, or is in use by another program.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when an audio context could not be created with the specified parameters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Occurs when an AudioContext already exists.</exception>
        /// <remarks>
        /// <para>For maximum compatibility, you are strongly recommended to use the default constructor.</para>
        /// <para>Multiple AudioContexts are not supported at this point.</para>
        /// <para>
        /// The number of auxilliary EFX sends depends on the audio hardware and drivers. Most Realtek devices, as well
        /// as the Creative SB Live!, support 1 auxilliary send. Creative's Audigy and X-Fi series support 4 sends.
        /// Values higher than supported will be clamped by the driver.
        /// </para>
        /// </remarks>
        private void CreateContext(string device, int freq, int refresh, bool sync, int monoSources, int stereoSources, bool enableEfx, MaxAuxiliarySends efxAuxiliarySends)
        {
            if (!AudioDeviceEnumerator.IsOpenALSupported)
            {
                throw new DllNotFoundException("openal32.dll");
            }

            if (AudioDeviceEnumerator.Version == AudioDeviceEnumerator.AlcVersion.Alc1_1 && AudioDeviceEnumerator.AvailablePlaybackDevices.Count == 0)    // Alc 1.0 does not support device enumeration.
            {
                throw new NotSupportedException("No audio hardware is available.");
            }
            if (context_exists)
            {
                throw new NotSupportedException("Multiple AudioContexts are not supported.");
            }
            if (freq < 0)
            {
                throw new ArgumentOutOfRangeException("freq", freq, "Should be greater than zero.");
            }
            if (refresh < 0)
            {
                throw new ArgumentOutOfRangeException("refresh", refresh, "Should be greater than zero.");
            }
            if (monoSources < -1)
            {
                throw new ArgumentOutOfRangeException("monoSources", refresh, "Should be greater than or equal to zero.");
            }
            if (stereoSources < -1)
            {
                throw new ArgumentOutOfRangeException("stereoSources", refresh, "Should be greater than or equal to zero.");
            }

            if (!String.IsNullOrEmpty(device))
            {
                device_name = device;
                Device = Alc.OpenDevice(device); // try to open device by name
            }
            if (Device == IntPtr.Zero)
            {
                device_name = "IntPtr.Zero (null string)";
                Device = Alc.OpenDevice(null); // try to open unnamed default device
            }
            if (Device == IntPtr.Zero)
            {
                device_name = AudioContext.DefaultDevice;
                Device = Alc.OpenDevice(AudioContext.DefaultDevice); // try to open named default device
            }
            if (Device == IntPtr.Zero)
            {
                device_name = "None";
                throw new AudioDeviceException(String.Format("Audio device '{0}' does not exist or is tied up by another application.",
                    String.IsNullOrEmpty(device) ? "default" : device));
            }

            CheckErrors();

            // Build the attribute list
            List<int> attributes = new List<int>();

            if (freq != 0)
            {
                attributes.Add((int)AlcContextAttributes.Frequency);
                attributes.Add(freq);
            }

            if (refresh != 0)
            {
                attributes.Add((int)AlcContextAttributes.Refresh);
                attributes.Add(refresh);
            }

            attributes.Add((int)AlcContextAttributes.Sync);
            attributes.Add(sync ? 1 : 0);

            if (monoSources != -1)
            {
                attributes.Add((int)AlcContextAttributes.MonoSources);
                attributes.Add(monoSources);
            }

            if (stereoSources != -1)
            {
                attributes.Add((int)AlcContextAttributes.StereoSources);
                attributes.Add(stereoSources);
            }

            if (enableEfx && Alc.IsExtensionPresent(Device, "ALC_EXT_EFX"))
            {
                int num_slots;
                switch (efxAuxiliarySends)
                {
                    case MaxAuxiliarySends.One:
                    case MaxAuxiliarySends.Two:
                    case MaxAuxiliarySends.Three:
                    case MaxAuxiliarySends.Four:
                        num_slots = (int)efxAuxiliarySends;
                        break;
                    default:
                    case MaxAuxiliarySends.UseDriverDefault:
                        Alc.GetInteger(Device, AlcGetInteger.EfxMaxAuxiliarySends, 1, out num_slots);
                        break;
                }

                attributes.Add((int)AlcContextAttributes.EfxMaxAuxiliarySends);
                attributes.Add(num_slots);
            }
            attributes.Add(0);

            context_handle = Alc.CreateContext(Device, attributes.ToArray());

            if (context_handle == ContextHandle.Zero)
            {
                Alc.CloseDevice(Device);
                throw new AudioContextException("The audio context could not be created with the specified parameters.");
            }

            CheckErrors();

            // HACK: OpenAL SI on Linux/ALSA crashes on MakeCurrent. This hack avoids calling MakeCurrent when
            // an old OpenAL version is detect - it may affect outdated OpenAL versions different than OpenAL SI,
            // but it looks like a good compromise for now.
            if (AudioDeviceEnumerator.AvailablePlaybackDevices.Count > 0)
            {
                MakeCurrent();
            }

            CheckErrors();

            device_name = Alc.GetString(Device, AlcGetString.DeviceSpecifier);


            lock (audio_context_lock)
            {
                available_contexts.Add(this.context_handle, this);
                context_exists = true;
            }
        }

        /// \internal
        /// <summary>Makes the specified AudioContext current in the calling thread.</summary>
        /// <param name="context">The OpenTK.Audio.AudioContext to make current, or null.</param>
        /// <exception cref="ObjectDisposedException">
        /// Occurs if this function is called after the AudioContext has been disposed.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when the AudioContext could not be made current.
        /// </exception>
        private static void MakeCurrent(AudioContext context)
        {
            lock (audio_context_lock)
            {
                if (!Alc.MakeContextCurrent(context != null ? context.context_handle : ContextHandle.Zero))
                {
                    throw new AudioContextException(String.Format("ALC {0} error detected at {1}.",
                        Alc.GetError(context != null ? (IntPtr)context.context_handle : IntPtr.Zero).ToString(),
                        context != null ? context.ToString() : "null"));
                }
            }
        }

        /// <summary>
        /// Gets or sets a System.Boolean indicating whether the AudioContext
        /// is current.
        /// </summary>
        /// <remarks>
        /// Only one AudioContext can be current in the application at any time,
        /// <b>regardless of the number of threads</b>.
        /// </remarks>
        internal bool IsCurrent
        {
            get
            {
                lock (audio_context_lock)
                {
                    if (available_contexts.Count == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return AudioContext.CurrentContext == this;
                    }
                }
            }
            set
            {
                if (value)
                {
                    AudioContext.MakeCurrent(this);
                }
                else
                {
                    AudioContext.MakeCurrent(null);
                }
            }
        }

        private IntPtr Device { get; set; }

        /// <summary>
        /// Checks for ALC error conditions.
        /// </summary>
        /// <exception cref="OutOfMemoryException">Raised when an out of memory error is detected.</exception>
        /// <exception cref="AudioValueException">Raised when an invalid value is detected.</exception>
        /// <exception cref="AudioDeviceException">Raised when an invalid device is detected.</exception>
        /// <exception cref="AudioContextException">Raised when an invalid context is detected.</exception>
        public void CheckErrors()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            new AudioDeviceErrorChecker(Device).Dispose();
        }

        /// <summary>
        /// Returns the ALC error code for this instance.
        /// </summary>
        public AlcError CurrentError
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return Alc.GetError(Device);
            }
        }

        /// <summary>Makes the AudioContext current in the calling thread.</summary>
        /// <exception cref="ObjectDisposedException">
        /// Occurs if this function is called after the AudioContext has been disposed.
        /// </exception>
        /// <exception cref="AudioContextException">
        /// Occurs when the AudioContext could not be made current.
        /// </exception>
        /// <remarks>
        /// Only one AudioContext can be current in the application at any time,
        /// <b>regardless of the number of threads</b>.
        /// </remarks>
        public void MakeCurrent()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            AudioContext.MakeCurrent(this);
        }

        /// <summary>
        /// Gets a System.Boolean indicating whether the AudioContext is
        /// currently processing audio events.
        /// </summary>
        /// <seealso cref="Process"/>
        /// <seealso cref="Suspend"/>
        public bool IsProcessing
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return is_processing;
            }
            private set { is_processing = value; }
        }

        /// <summary>
        /// Gets a System.Boolean indicating whether the AudioContext is
        /// synchronized.
        /// </summary>
        /// <seealso cref="Process"/>
        public bool IsSynchronized
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return is_synchronized;
            }
            private set { is_synchronized = value; }
        }

        /// <summary>
        /// Returns an integer referring to the number of mono sources.
        /// </summary>
        public int MonoSources
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                int sources;
                Alc.GetInteger(Device, AlcGetInteger.MonoSources, 1, out sources);
                return sources;
            }
        }

        /// <summary>
        /// Returns an integer referring to the number of stereo sources.
        /// </summary>
        public int StereoSources
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                int sources;
                Alc.GetInteger(Device, AlcGetInteger.StereoSources, 1, out sources);
                return sources;
            }
        }

        /// <summary>
        /// Processes queued audio events.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If AudioContext.IsSynchronized is true, this function will resume
        /// the internal audio processing thread. If AudioContext.IsSynchronized is false,
        /// you will need to call this function multiple times per second to process
        /// audio events.
        /// </para>
        /// <para>
        /// In some implementations this function may have no effect.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Occurs when this function is called after the AudioContext had been disposed.</exception>
        /// <seealso cref="Suspend"/>
        /// <seealso cref="IsProcessing"/>
        /// <seealso cref="IsSynchronized"/>
        public void Process()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            Alc.ProcessContext(this.context_handle);
            IsProcessing = true;
        }

        /// <summary>
        /// Suspends processing of audio events.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To avoid audio artifacts when calling this function, set audio gain to zero before
        /// suspending an AudioContext.
        /// </para>
        /// <para>
        /// In some implementations, it can be faster to suspend processing before changing
        /// AudioContext state.
        /// </para>
        /// <para>
        /// In some implementations this function may have no effect.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Occurs when this function is called after the AudioContext had been disposed.</exception>
        /// <seealso cref="Process"/>
        /// <seealso cref="IsProcessing"/>
        /// <seealso cref="IsSynchronized"/>
        public void Suspend()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            Alc.SuspendContext(this.context_handle);
            IsProcessing = false;
        }

        /// <summary>
        /// Checks whether the specified OpenAL extension is supported.
        /// </summary>
        /// <param name="extension">The name of the extension to check (e.g. "ALC_EXT_EFX").</param>
        /// <returns>true if the extension is supported; false otherwise.</returns>
        public bool SupportsExtension(string extension)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            return Alc.IsExtensionPresent(this.Device, extension);
        }

        /// <summary>
        /// Gets a System.String with the name of the device used in this context.
        /// </summary>
        public string CurrentDevice
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return device_name;
            }
        }

        /// <summary>
        /// Gets the OpenTK.Audio.AudioContext which is current in the application.
        /// </summary>
        /// <remarks>
        /// Only one AudioContext can be current in the application at any time,
        /// <b>regardless of the number of threads</b>.
        /// </remarks>
        public static AudioContext CurrentContext
        {
            get
            {
                lock (audio_context_lock)
                {
                    if (available_contexts.Count == 0)
                    {
                        return null;
                    }
                    else
                    {
                        AudioContext context;
                        AudioContext.available_contexts.TryGetValue(
                            (ContextHandle)Alc.GetCurrentContext(),
                            out context);
                        return context;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a list of strings containing all known playback devices.
        /// </summary>
        public static IList<string> AvailableDevices
        {
            get
            {
                return AudioDeviceEnumerator.AvailablePlaybackDevices;
            }
        }
        /// <summary>
        /// Returns the name of the device that will be used as playback default.
        /// </summary>
        public static string DefaultDevice
        {
            get
            {
                return AudioDeviceEnumerator.DefaultPlaybackDevice;
            }
        }

        /// <summary>
        /// Disposes of the AudioContext, cleaning up all resources consumed by it.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool manual)
        {
            if (!disposed)
            {
                if (this.IsCurrent)
                {
                    this.IsCurrent = false;
                }

                if (context_handle != ContextHandle.Zero)
                {
                    available_contexts.Remove(context_handle);
                    Alc.DestroyContext(context_handle);
                }

                if (Device != IntPtr.Zero)
                {
                    Alc.CloseDevice(Device);
                }

                if (manual)
                {
                }
                disposed = true;
            }
        }

        /// <summary>
        /// Finalizes this instance.
        /// </summary>
        ~AudioContext()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Calculates the hash code for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Compares this instance with another.
        /// </summary>
        /// <param name="obj">The instance to compare to.</param>
        /// <returns>True, if obj refers to this instance; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that desrcibes this instance.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that desrcibes this instance.</returns>
        public override string ToString()
        {
            return String.Format("{0} (handle: {1}, device: {2})",
                                 this.device_name, this.context_handle, this.Device);
        }
    }
}
