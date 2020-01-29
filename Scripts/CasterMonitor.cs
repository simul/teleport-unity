using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class CasterMonitor : MonoBehaviour
{
    private static SCServer.CasterSettings _casterSettings;
    public SCServer.CasterSettings casterSettings;

    public CasterContext casterContext;

    public int listenPort = 10500;
    public int discoveryPort = 10607;
    public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.

    private GCHandle settingsHandle;
    private GCHandle contextHandle;

    #region DLLDelegates
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void OnShowActor(IntPtr actorPtr);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void OnHideActor(IntPtr actorPtr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void OnSetHeadPose(avs.HeadPose newHeadPose);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void OnNewInput(avs.InputState newInput);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void OnDisconnect();
    #endregion

    #region DLLImport
    [DllImport("SimulCasterServer")]
    private static extern void Initialise(IntPtr settings, OnShowActor showActor, OnHideActor hideActor,
        OnSetHeadPose headPoseSetter, OnNewInput newInputProcessing, OnDisconnect disconnect);
    [DllImport("SimulCasterServer")]
    private static extern void Tick(float deltaTime);
    [DllImport("SimulCasterServer")]
    private static extern void Shutdown();
    #endregion

    public static SCServer.CasterSettings GetCasterSettings()
    {
        return _casterSettings;
    }

	private void Awake()
    {
        _casterSettings = casterSettings;

        settingsHandle = GCHandle.Alloc(_casterSettings, GCHandleType.Pinned);

        Initialise(settingsHandle.AddrOfPinnedObject(), ShowActor, HideActor, SetHeadPose, ProcessInput, Disconnect);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void ShowActor(IntPtr actorPtr)
    {
        throw new NotImplementedException();
    }

    private void HideActor(IntPtr actorPtr)
    {
        throw new NotImplementedException();
    }

    private void SetHeadPose(avs.HeadPose newHeadPose)
    {
        throw new NotImplementedException();
    }

    private void ProcessInput(avs.InputState newInput)
    {
        throw new NotImplementedException();
    }

    private void Disconnect()
    {
        throw new NotImplementedException();
    }
}
