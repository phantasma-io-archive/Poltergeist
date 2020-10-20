using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

// class to set app focus on demand
// NOTE - only Windows OS supported for now...
public class AppFocus : MonoBehaviour
{
    public static AppFocus Instance;

    public void Awake()
    {
        Instance = this;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        _windowHandle = GetActiveWindow();
        Debug.Log("Got app focus handle: " + _windowHandle.ToInt32());
#endif
    }


#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private IntPtr _windowHandle;
    private IntPtr _otherHandle;

    public void StartFocus()
    {
        _otherHandle = GetForegroundWindow();
        if (_otherHandle != _windowHandle)
        {
            SetForegroundWindow(_windowHandle);
        }
    }

    public void EndFocus()
    {
        if (_otherHandle != _windowHandle)
        {
            SetForegroundWindow(_otherHandle);
        }
    }
#else
    public static void Focus()
    {
        Debug.Warning("Focus() not supported on this platform");
    }

#endif

}
