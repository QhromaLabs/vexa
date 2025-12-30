using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Vexa.Models;

namespace Vexa.Services;

public sealed class FootPedalService
{
    private readonly Dictionary<int, InputAction> _buttonMap = new()
    {
        [1] = InputAction.PlayPause,
        [2] = InputAction.Rewind,
        [3] = InputAction.LoopLastFiveSeconds
    };

    public event EventHandler<InputAction>? ActionTriggered;

    public void Initialize(WindowInteropHelper helper)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x08,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = helper.Handle
            },
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x0C,
                usUsage = 0x01,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = helper.Handle
            }
        };

        RegisterRawInputDevices(devices, (uint)devices.Length, Marshal.SizeOf<RAWINPUTDEVICE>());
        HwndSource.FromHwnd(helper.Handle)?.AddHook(WndProc);
    }

    public void SetMapping(int buttonIndex, InputAction action)
    {
        _buttonMap[buttonIndex] = action;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_INPUT)
        {
            return IntPtr.Zero;
        }

        var size = 0u;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0)
        {
            return IntPtr.Zero;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != size)
            {
                return IntPtr.Zero;
            }

            var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (raw.header.dwType != RIM_TYPEHID)
            {
                return IntPtr.Zero;
            }

            var data = new byte[raw.hid.dwSizeHid * raw.hid.dwCount];
            Marshal.Copy(raw.hid.bRawData, data, 0, data.Length);
            var buttonIndex = GetFirstNonZeroByteIndex(data);
            if (buttonIndex > 0 && _buttonMap.TryGetValue(buttonIndex, out var action))
            {
                ActionTriggered?.Invoke(this, action);
                handled = true;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return IntPtr.Zero;
    }

    private static int GetFirstNonZeroByteIndex(byte[] data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] != 0)
            {
                return i + 1;
            }
        }

        return 0;
    }

    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEHID = 2;
    private const uint RIDEV_INPUTSINK = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWHID hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWHID
    {
        public uint dwSizeHid;
        public uint dwCount;
        public IntPtr bRawData;
    }

    [DllImport("User32.dll")]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, int cbSize);

    [DllImport("User32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
}
