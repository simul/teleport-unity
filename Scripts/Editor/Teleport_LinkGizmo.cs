using UnityEngine;
using UnityEditor;
using System.IO;

namespace teleport
{
	public class Teleport_LinkGizmo : MonoBehaviour
	{
		static GUIStyle centredStyle=new GUIStyle();
		static Color linkColour=new Color(0.15f, 0.18f, 1.0f);
		static Teleport_LinkGizmo()
		{
			centredStyle.normal.textColor = linkColour;
			centredStyle.fontSize = 36;
			centredStyle.alignment= TextAnchor.MiddleCenter;
			centredStyle.normal.background=new Texture2D(24,24);
			var GizmosPath = Application.dataPath + "/Gizmos";
			if (!System.IO.Directory.Exists(GizmosPath))
			{
				System.IO.Directory.CreateDirectory(GizmosPath);
			}
			string sourceFile = Path.Combine(Startup.GizmosPath, "PortalLinkIcon.png");
			string destFile = Path.Combine(GizmosPath, "PortalLinkIcon.png");
			if (!System.IO.File.Exists(destFile))
				System.IO.File.Copy(sourceFile, destFile, true);
		}
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawLink(teleport.Link Link, GizmoType gizmoType)
		{
			Camera camera = Link.gameObject.GetComponent<Camera>(); 
			Gizmos.color = linkColour;
			Gizmos.DrawSphere( Link.transform.position,0.5f);
			Gizmos.DrawIcon(Link.transform.position, "PortalLinkIcon.png", true);
			Handles.Label(Link.transform.position+new Vector3(0,0.6f,0),Link.url,centredStyle);

		}
	}
}
