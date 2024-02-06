using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_LinkGizmo : MonoBehaviour
	{
		static GUIStyle centredStyle=new GUIStyle();
		static Color linkColour=new Color(0.15f, 0.18f, 1.0f);
		static Teleport_LinkGizmo()
		{
			centredStyle.normal.textColor = linkColour;
			centredStyle.fontSize = 24;
			centredStyle.alignment= TextAnchor.MiddleCenter;
			centredStyle.normal.background=new Texture2D(24,24);
		}
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawLink(teleport.Link Link, GizmoType gizmoType)
		{
			Camera camera = Link.gameObject.GetComponent<Camera>(); 
			Gizmos.color = linkColour;
			Gizmos.DrawSphere( Link.transform.position,0.5f);
			Handles.Label(Link.transform.position+new Vector3(0,0.6f,0), Link.url,centredStyle);

		}
	}
}
