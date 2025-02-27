//
// XInputJoystick.cs
//
// Author:
//       Stefanos A. <stapostol@gmail.com>
//
// Copyright (c) 2006-2013 Stefanos Apostolopoulos
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using OpenTK.Input;
using System.Runtime.InteropServices;
using System.Security;
using System.Diagnostics;

namespace OpenTK.Platform.Windows
{
    internal class XInputJoystick : IJoystickDriver2, IDisposable
    {
        // All XInput devices use the same Guid
        // (only one GamePadConfiguration entry required)
        private static readonly Guid guid =
            new Guid("78696e70757400000000000000000000"); // equiv. to "xinput"

        private XInput xinput = new XInput();

        public JoystickState GetState(int index)
        {
            XInputState xstate;
            XInputErrorCode error = xinput.GetState((XInputUserIndex)index, out xstate);

            JoystickState state = new JoystickState();
            if (error == XInputErrorCode.Success)
            {
                state.SetIsConnected(true);

                state.SetAxis(0, (short)xstate.GamePad.ThumbLX);
                state.SetAxis(1, (short)Math.Min(short.MaxValue, -xstate.GamePad.ThumbLY));
                state.SetAxis(2, (short)Common.HidHelper.ScaleValue(xstate.GamePad.LeftTrigger, 0, byte.MaxValue, short.MinValue, short.MaxValue));
                state.SetAxis(3, (short)xstate.GamePad.ThumbRX);
                state.SetAxis(4, (short)Math.Min(short.MaxValue, -xstate.GamePad.ThumbRY));
                state.SetAxis(5, (short)Common.HidHelper.ScaleValue(xstate.GamePad.RightTrigger, 0, byte.MaxValue, short.MinValue, short.MaxValue));

                state.SetButton(0, (xstate.GamePad.Buttons & XInputButtons.A) != 0);
                state.SetButton(1, (xstate.GamePad.Buttons & XInputButtons.B) != 0);
                state.SetButton(2, (xstate.GamePad.Buttons & XInputButtons.X) != 0);
                state.SetButton(3, (xstate.GamePad.Buttons & XInputButtons.Y) != 0);
                state.SetButton(4, (xstate.GamePad.Buttons & XInputButtons.LeftShoulder) != 0);
                state.SetButton(5, (xstate.GamePad.Buttons & XInputButtons.RightShoulder) != 0);
                state.SetButton(6, (xstate.GamePad.Buttons & XInputButtons.Back) != 0);
                state.SetButton(7, (xstate.GamePad.Buttons & XInputButtons.Start) != 0);
                state.SetButton(8, (xstate.GamePad.Buttons & XInputButtons.LeftThumb) != 0);
                state.SetButton(9, (xstate.GamePad.Buttons & XInputButtons.RightThumb) != 0);
                state.SetButton(10, (xstate.GamePad.Buttons & XInputButtons.Guide) != 0);

                state.SetHat(JoystickHat.Hat0, new JoystickHatState(TranslateHat(xstate.GamePad.Buttons)));
            }

            return state;
        }

        private HatPosition TranslateHat(XInputButtons buttons)
        {
            XInputButtons dir = 0;

            dir = XInputButtons.DPadUp | XInputButtons.DPadLeft;
            if ((buttons & dir) == dir)
            {
                return HatPosition.UpLeft;
            }
            dir = XInputButtons.DPadUp | XInputButtons.DPadRight;
            if ((buttons & dir) == dir)
            {
                return HatPosition.UpRight;
            }
            dir = XInputButtons.DPadDown | XInputButtons.DPadLeft;
            if ((buttons & dir) == dir)
            {
                return HatPosition.DownLeft;
            }
            dir = XInputButtons.DPadDown | XInputButtons.DPadRight;
            if ((buttons & dir) == dir)
            {
                return HatPosition.DownRight;
            }

            dir = XInputButtons.DPadUp;
            if ((buttons & dir) == dir)
            {
                return HatPosition.Up;
            }
            dir = XInputButtons.DPadRight;
            if ((buttons & dir) == dir)
            {
                return HatPosition.Right;
            }
            dir = XInputButtons.DPadDown;
            if ((buttons & dir) == dir)
            {
                return HatPosition.Down;
            }
            dir = XInputButtons.DPadLeft;
            if ((buttons & dir) == dir)
            {
                return HatPosition.Left;
            }

            return HatPosition.Centered;
        }

        public JoystickCapabilities GetCapabilities(int index)
        {
            XInputDeviceCapabilities xcaps;
            XInputErrorCode error = xinput.GetCapabilities(
                (XInputUserIndex)index,
                XInputCapabilitiesFlags.Default,
                out xcaps);

            if (error == XInputErrorCode.Success)
            {
                //GamePadType type = TranslateSubType(xcaps.SubType);
                int buttons = TranslateButtons(xcaps.GamePad.Buttons);
                int axes = TranslateAxes(ref xcaps.GamePad);

                return new JoystickCapabilities(axes, buttons, 1, true);
            }
            return new JoystickCapabilities();
        }

        public string GetName(int index)
        {
            return "XInput Controller " + index;
        }

        public Guid GetGuid(int index)
        {
            return guid;
        }

        public bool SetVibration(int index, float left, float right)
        {
            left = MathHelper.Clamp(left, 0.0f, 1.0f);
            right = MathHelper.Clamp(right, 0.0f, 1.0f);

            XInputVibration vibration = new XInputVibration(
                (ushort)(left * UInt16.MaxValue),
                (ushort)(right * UInt16.MaxValue));

            return xinput.SetState((XInputUserIndex)index, ref vibration) == XInputErrorCode.Success;
        }

        private int TranslateAxes(ref XInputGamePad pad)
        {
            int count = 0;
            count += pad.ThumbLX != 0 ? 1 : 0;
            count += pad.ThumbLY != 0 ? 1 : 0;
            count += pad.ThumbRX != 0 ? 1 : 0;
            count += pad.ThumbRY != 0 ? 1 : 0;
            count += pad.LeftTrigger != 0 ? 1 : 0;
            count += pad.RightTrigger != 0 ? 1 : 0;
            return count;
        }

        private int NumberOfSetBits(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        private int TranslateButtons(XInputButtons xbuttons)
        {
            return NumberOfSetBits((int)xbuttons);
        }

#if false
        // Todo: Implement JoystickType enumeration
        GamePadType TranslateSubType(XInputDeviceSubType xtype)
        {
            switch (xtype)
            {
                case XInputDeviceSubType.ArcadePad: return GamePadType.ArcadePad;
                case XInputDeviceSubType.ArcadeStick: return GamePadType.ArcadeStick;
                case XInputDeviceSubType.DancePad: return GamePadType.DancePad;
                case XInputDeviceSubType.DrumKit: return GamePadType.DrumKit;
                case XInputDeviceSubType.FlightStick: return GamePadType.FlightStick;
                case XInputDeviceSubType.GamePad: return GamePadType.GamePad;
                case XInputDeviceSubType.Guitar: return GamePadType.Guitar;
                case XInputDeviceSubType.GuitarAlternate: return GamePadType.AlternateGuitar;
                case XInputDeviceSubType.GuitarBass: return GamePadType.BassGuitar;
                case XInputDeviceSubType.Wheel: return GamePadType.Wheel;
                case XInputDeviceSubType.Unknown:
                default:
                    return GamePadType.Unknown;
            }
        }
#endif

        private enum XInputErrorCode
        {
            Success = 0,
            DeviceNotConnected
        }

        private enum XInputDeviceType : byte
        {
            GamePad
        }

        private enum XInputDeviceSubType : byte
        {
            Unknown = 0,
            GamePad = 1,
            Wheel = 2,
            ArcadeStick = 3,
            FlightStick = 4,
            DancePad = 5,
            Guitar = 6,
            GuitarAlternate = 7,
            DrumKit = 8,
            GuitarBass = 0xb,
            ArcadePad = 0x13
        }

        private enum XInputCapabilities
        {
            ForceFeedback = 0x0001,
            Wireless = 0x0002,
            Voice = 0x0004,
            PluginModules = 0x0008,
            NoNavigation = 0x0010,
        }

        private enum XInputButtons : ushort
        {
            DPadUp = 0x0001,
            DPadDown = 0x0002,
            DPadLeft = 0x0004,
            DPadRight = 0x0008,
            Start = 0x0010,
            Back = 0x0020,
            LeftThumb = 0x0040,
            RightThumb = 0x0080,
            LeftShoulder = 0x0100,
            RightShoulder = 0x0200,
            Guide = 0x0400, // Undocumented, requires XInputGetStateEx + XINPUT_1_3.dll or higher
            A = 0x1000,
            B = 0x2000,
            X = 0x4000,
            Y = 0x8000
        }

        [Flags]
        private enum XInputCapabilitiesFlags
        {
            Default = 0,
            GamePadOnly = 1
        }

        private enum XInputBatteryType : byte
        {
            Disconnected = 0x00,
            Wired = 0x01,
            Alkaline = 0x02,
            NiMH = 0x03,
            Unknown = 0xff
        }

        private enum XInputBatteryLevel : byte
        {
            Empty = 0x00,
            Low = 0x01,
            Medium = 0x02,
            Full = 0x03
        }

        private enum XInputUserIndex
        {
            First = 0,
            Second,
            Third,
            Fourth,
            Any = 0xff
        }

#pragma warning disable 0649 // field is never assigned

        private struct XInputThresholds
        {
            public const int LeftThumbDeadzone = 7849;
            public const int RightThumbDeadzone = 8689;
            public const int TriggerThreshold = 30;
        }

        private struct XInputGamePad
        {
            public XInputButtons Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLX;
            public short ThumbLY;
            public short ThumbRX;
            public short ThumbRY;
        }

        private struct XInputState
        {
            public int PacketNumber;
            public XInputGamePad GamePad;
        }

        private struct XInputVibration
        {
            public ushort LeftMotorSpeed;
            public ushort RightMotorSpeed;

            public XInputVibration(ushort left, ushort right)
            {
                LeftMotorSpeed = left;
                RightMotorSpeed = right;
            }
        }

        private struct XInputDeviceCapabilities
        {
            public XInputDeviceType Type;
            public XInputDeviceSubType SubType;
            public short Flags;
            public XInputGamePad GamePad;
            public XInputVibration Vibration;
        }

        private struct XInputBatteryInformation
        {
            public XInputBatteryType Type;
            public XInputBatteryLevel Level;
        }

        internal int FindNextValidID(int index)
        {
            if (index > 3 || index < 0)
            {
                //Valid XinputIDs are 0 - 3
                //Return -1 to indicate we haven't found a valid ID
                return -1;
            }
            XInputDeviceCapabilities xcaps;
            XInputErrorCode error = xinput.GetCapabilities(
                (XInputUserIndex)index,
                XInputCapabilitiesFlags.Default,
                out xcaps);
            if (error != XInputErrorCode.Success)
            {
                //This ID isn't actually connected, so loop around & try the next
                return FindNextValidID(index + 1);
            }
            //This is connected!
            return index;
        }

        private class XInput : IDisposable
        {
            private IntPtr dll;

            internal XInput()
            {
                // Try to load the newest XInput***.dll installed on the system
                // The delegates below will be loaded dynamically from that dll
                dll = Functions.LoadLibrary("XINPUT1_4");
                if (dll == IntPtr.Zero)
                {
                    dll = Functions.LoadLibrary("XINPUT1_3");
                }
                if (dll == IntPtr.Zero)
                {
                    dll = Functions.LoadLibrary("XINPUT1_2");
                }
                if (dll == IntPtr.Zero)
                {
                    dll = Functions.LoadLibrary("XINPUT1_1");
                }
                if (dll == IntPtr.Zero)
                {
                    dll = Functions.LoadLibrary("XINPUT9_1_0");
                }
                if (dll == IntPtr.Zero)
                {
                   Debug.Print("XInput was not found on this platform");
                   return;
                }

                // Load the entry points we are interested in from that dll
                GetCapabilities = (XInputGetCapabilities)Load("XInputGetCapabilities", typeof(XInputGetCapabilities));
                GetState =
                    // undocumented XInputGetStateEx (Ordinal 100) with support for the "Guide" button (requires XINPUT_1_3+)
                    (XInputGetState)Load(100, typeof(XInputGetState)) ??
                    // documented XInputGetState (no support for the "Guide" button)
                    (XInputGetState)Load("XInputGetState", typeof(XInputGetState));
                SetState = (XInputSetState)Load("XInputSetState", typeof(XInputSetState));
            }

            private Delegate Load(ushort ordinal, Type type)
            {
                IntPtr pfunc = Functions.GetProcAddress(dll, (IntPtr)ordinal);
                if (pfunc != IntPtr.Zero)
                {
                    return Marshal.GetDelegateForFunctionPointer(pfunc, type);
                }
                return null;
            }

            private Delegate Load(string name, Type type)
            {
                IntPtr pfunc = Functions.GetProcAddress(dll, name);
                if (pfunc != IntPtr.Zero)
                {
                    return Marshal.GetDelegateForFunctionPointer(pfunc, type);
                }
                return null;
            }

            internal XInputGetCapabilities GetCapabilities;
            internal XInputGetState GetState;
            internal XInputSetState SetState;

            [SuppressUnmanagedCodeSecurity]
            internal delegate XInputErrorCode XInputGetCapabilities(
                XInputUserIndex dwUserIndex,
                XInputCapabilitiesFlags dwFlags,
                out XInputDeviceCapabilities pCapabilities);

            [SuppressUnmanagedCodeSecurity]
            internal delegate XInputErrorCode XInputGetState
            (
                XInputUserIndex dwUserIndex,
                out XInputState pState
            );

            [SuppressUnmanagedCodeSecurity]
            internal delegate XInputErrorCode XInputSetState
            (
                XInputUserIndex dwUserIndex,
                ref XInputVibration pVibration
            );

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool manual)
            {
                if (manual)
                {
                    if (dll != IntPtr.Zero)
                    {
                        Functions.FreeLibrary(dll);
                        dll = IntPtr.Zero;
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool manual)
        {
            if (manual)
            {
                xinput.Dispose();
            }
            else
            {
                Debug.Print("{0} leaked, did you forget to call Dispose()?", typeof(XInputJoystick).Name);
            }
        }

#if DEBUG
        ~XInputJoystick()
        {
            Dispose(false);
        }
#endif
    }
}
