using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntentPluginManager : MonoBehaviour
{
    public static IntentPluginManager Instance { get; private set; }
    [SerializeField] private string PluginName = "com.phantasma.poltergeistmodule.MainActivity";
    
    private AndroidJavaObject _PluginInstance;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        #if UNITY_ANDROID
        InitializePlugin(PluginName);
        #endif
    }

    private void InitializePlugin(string pluginName)
    {
#if UNITY_ANDROID
        _PluginInstance = new AndroidJavaObject(pluginName);
        if (_PluginInstance == null)
        {
            Debug.LogError("Error Loading Plugin..");
        }
#endif
        
        //_PluginInstance.CallStatic("ReceiveActivity", UnityActivity);
    }
    
    public void CallMethodByName(string msg)
    {
#if UNITY_ANDROID
        Debug.Log("Unity-PG" + $" test->{msg}");
#endif
    }

    public void ReturnMessage(string msg)
    {
#if UNITY_ANDROID
        _PluginInstance.Call("ReturnMessage", msg);
#endif
    }
}
