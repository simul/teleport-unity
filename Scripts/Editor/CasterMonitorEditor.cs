using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(teleport.CasterMonitor))]
public class CasterMonitorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(10);
        if(GUILayout.Button("Open Resource Window")) teleport.CasterResourceWindow.OpenResourceWindow();
    }
}
