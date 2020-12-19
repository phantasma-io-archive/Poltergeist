using Phantasma.SDK;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

// class to set app focus on demand
// NOTE - only Windows OS supported for now...
// https://stackoverflow.com/questions/5206633/find-out-what-application-window-is-in-focus-in-java/18275492
// https://gist.github.com/yuliyv/e886314574cd9a73fe91
// https://stackoverflow.com/questions/49351704/applescript-to-focus-on-a-window-in-preview-app
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
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX

    private string _otherEXE;

    public void StartFocus()
    {
        //_otherEXE = GetForegroundWindow();
        //ExecuteProcessTerminal("open " + _thisEXE);
    }

    public void EndFocus()
    {
        if (!string.IsNullOrEmpty(_otherEXE))
        {
            //ExecuteProcessTerminal("open " + _otherEXE);
        }
    }


    private void ExecuteProcessTerminal(string argument)
    {
        try
        {
            /*Debug.Log("============== Start Executing [" + argument + "] ===============");
            ProcessStartInfo startInfo = new ProcessStartInfo("/bin/bash")
            {
                WorkingDirectory = "/",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            Process myProcess = new Process
            {
                StartInfo = startInfo
            };
            myProcess.StartInfo.Arguments = argument;
            myProcess.Start();
            string output = myProcess.StandardOutput.ReadToEnd();
            Debug.Log("Result for [" + argument + "] is : \n" + output);
            myProcess.WaitForExit();
            Debug.Log("============== End ===============");*/
        }
        catch (Exception e)
        {
            //Debug.Warning(e);
        }
    }
#else
    public void StartFocus()
    {
        Log.WriteWarning("StartFocus() not implemented on this platform");
    }

    public void EndFocus()
    {
        Log.WriteWarning("EndFocus() not implemented on this platform");
    }
#endif
}
