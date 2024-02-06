using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_MonitorGizmo : MonoBehaviour
	{
		static GUIStyle centredStyle=new GUIStyle();
		static Color MonitorColour=new Color(0.15f, 0.18f, 0.0f);
		static Color BackgroundColour = new Color(0.15f, 0.18f, 0.7f);
		static Teleport_MonitorGizmo()
		{
			centredStyle.normal.textColor = MonitorColour;
			centredStyle.fontSize = 24;
			centredStyle.alignment= TextAnchor.MiddleCenter;
			centredStyle.normal.background=new Texture2D(24,24);
			
		}
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawMonitor(teleport.Monitor Monitor, GizmoType gizmoType)
		{
			Camera camera = Monitor.gameObject.GetComponent<Camera>(); 
			Gizmos.color = BackgroundColour;
			Gizmos.DrawSphere( Monitor.transform.position,0.1f);
			Handles.Label(Monitor.transform.position + new Vector3(0, 1.2f, 0), ((double)Monitor.GetUnixStartTimestampUs()/1000000.0).ToString(), centredStyle);
			Handles.Label(Monitor.transform.position + new Vector3(0, 1.4f, 0), Monitor.GetSessionTimestampNowS().ToString(), centredStyle);

		}
	}
}
