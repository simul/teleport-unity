using UnityEngine;
using UnityEditor;
using System.Runtime.CompilerServices;
using System.IO;
using avs;

namespace teleport
{
	public class Teleport_ControllerGizmo : MonoBehaviour
	{
		static UnityEngine.Mesh cylinder;
		static UnityEngine.Mesh controllerLeft;
		static UnityEngine.Mesh controllerRight;
		static Teleport_ControllerGizmo()
		{
			var GizmosPath= Application.dataPath + "/Gizmos";
			if(!System.IO.Directory.Exists(GizmosPath)) 
			{ 
				System.IO.Directory.CreateDirectory(GizmosPath);
			} 
			string sourceFile = Path.Combine(Startup.GizmosPath, "Joystick.png");
			string destFile = Path.Combine(GizmosPath, "Joystick.png");
			if (!System.IO.File.Exists(destFile))
				System.IO.File.Copy(sourceFile, destFile,true);
			var go= GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			cylinder = go.GetComponent<MeshFilter>().sharedMesh;
			GameObject.DestroyImmediate(go);
		}
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)] 
		static void DrawClientspaceRoot(Teleport_Controller controller, GizmoType gizmoType)
		{
			float s=1.0f;
			var tr= controller.transform;
			if (controllerLeft)
				Gizmos.DrawMesh(controllerLeft, 0, tr.position, tr.rotation, new UnityEngine.Vector3(s, s, s));
			// Draws a 5 unit long red line in front of the object
			//float pointing_length = 0.3f;
			float l = 0.05f;
			float axis_size = l;
			float c=0.01f;
			float X=0.02f;
			float Y=0.04f;
			float Z=0.05f;
			float T=0.005f;
			Gizmos.color = new Color(0.8f, 0.5f, 0.3f);
			Gizmos.DrawMesh(cylinder, 0, tr.position + l * tr.right - Z * tr.forward, Quaternion.LookRotation(tr.forward, tr.right), new UnityEngine.Vector3(c, l, c));
			Gizmos.color = new Color(0.5f, 0.7f, 0.0f);
			//Gizmos.DrawRay(tr.position, tr.up * axis_size);
			Gizmos.DrawMesh(cylinder, 0, tr.position + l * tr.up - Z * tr.forward, Quaternion.LookRotation(tr.right, tr.up), new UnityEngine.Vector3(c, l, c));
			//Gizmos.DrawIcon(tr.position, "Joystick.png");
			// Main handle:
			Gizmos.color = new Color(0.0f, 0.7f, 0.8f);
			Gizmos.DrawMesh(cylinder, 0, tr.position-Z* tr.forward, Quaternion.LookRotation(tr.up, tr.forward), new UnityEngine.Vector3(X, Z, Y));
			Gizmos.DrawMesh(cylinder, 0, tr.position-T*tr.forward, Quaternion.LookRotation(tr.up, tr.forward), new UnityEngine.Vector3(1.25F*X, T, 1.25F*Y));

		}
	}
}
