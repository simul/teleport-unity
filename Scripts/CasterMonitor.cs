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
    private static extern void Initialise(OnShowActor showActor, OnHideActor hideActor,
        OnSetHeadPose headPoseSetter, OnNewInput newInputProcessing, OnDisconnect disconnect);
    [DllImport("SimulCasterServer")]
    private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);
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

        Initialise(ShowActor, HideActor, SetHeadPose, ProcessInput, Disconnect);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void OnValidate()
    {
        UpdateCasterSettings(casterSettings);
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void ShowActor(IntPtr actorPtr)
    {
        Debug.LogWarning("ShowActor(IntPtr actorPtr) not implemented.");
    }

    private void HideActor(IntPtr actorPtr)
    {
        Debug.LogWarning("HideActor(IntPtr actorPtr) not implemented.");
    }

    private void SetHeadPose(avs.HeadPose newHeadPose)
    {
        Debug.LogWarning("SetHeadPose(avs.HeadPose newHeadPose) not implemented.");
    }

    private void ProcessInput(avs.InputState newInput)
    {
        Debug.LogWarning("ProcessInput(avs.InputState newInput) not implemented.");
    }

    private void Disconnect()
    {
        Debug.LogWarning("Disconnect() not implemented.");
    }
}
