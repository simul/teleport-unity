using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_HeadGizmo : MonoBehaviour
	{
		static UnityEngine.Mesh cylinder;
		static UnityEngine.Mesh sphere;
		static UnityEngine.Mesh cube;
		static UnityEngine.Mesh GetPrimitive(PrimitiveType primitiveType)
		{
			var go = GameObject.CreatePrimitive(primitiveType);
			UnityEngine.Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
			GameObject.DestroyImmediate(go);
			return mesh;
		}
		static Teleport_HeadGizmo()
		{
			cylinder = GetPrimitive(PrimitiveType.Cylinder);
			sphere = GetPrimitive(PrimitiveType.Sphere);
			cube = GetPrimitive(PrimitiveType.Cube);
		}
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawHead(Teleport_Head head, GizmoType gizmoType)
		{
			Gizmos.color = new Color(0.2f, 0.3f, 1.0f);
			Camera camera = head.gameObject.GetComponent<Camera>(); 
			float c = 0.01f;
			float l = 0.15f;
			Gizmos.color = new Color(0.0f, 0.7f, 0.8f);
			Gizmos.DrawMesh(cylinder, 0, head.transform.position + l * head.transform.forward, Quaternion.LookRotation(head.transform.up, head.transform.forward), new UnityEngine.Vector3(c, l, c));
			Gizmos.color = new Color(0.8f, 0.5f, 0.3f);
			Gizmos.DrawMesh(cylinder, 0, head.transform.position + l * head.transform.right, Quaternion.LookRotation(head.transform.forward, head.transform.right), new UnityEngine.Vector3(c, l, c));
			Gizmos.color = new Color(0.5f, 0.7f, 0.0f);
			Gizmos.DrawMesh(cylinder, 0, head.transform.position + l * head.transform.up, Quaternion.LookRotation(head.transform.right, head.transform.up), new UnityEngine.Vector3(c, l, c));

			// Draw a rough "head",  approximately where the skull dome should be relative to the eyes.

			Gizmos.color = new Color(0.0f, 0.7f, 0.8f);
			float skull_longitudinal_radius=0.1f;
			float skull_lateral_radius = 0.07f;
			float skull_eye_vertical_offset = 0.02f;
			float skull_eye_forward_offset = 0.02f;
			var skull_centre= head.transform.position + (skull_eye_forward_offset - skull_longitudinal_radius) * head.transform.forward + skull_eye_vertical_offset * head.transform.up;
			var jaw_scale = new UnityEngine.Vector3(0.06f, 0.08f, 0.08f);
			var jaw_offset = new UnityEngine.Vector3(0.0f, -0.08f, 0.04f);
			Gizmos.DrawMesh(cube, 0, skull_centre + jaw_offset, head.transform.rotation, jaw_scale);
			Gizmos.DrawMesh(sphere, 0, skull_centre, Quaternion.LookRotation(head.transform.forward, head.transform.up), new UnityEngine.Vector3(2.0f* skull_lateral_radius, 2.0f* skull_longitudinal_radius, 2.0f * skull_longitudinal_radius));

			Gizmos.matrix = head.transform.localToWorldMatrix;
			if (camera)
			{
				Gizmos.DrawFrustum(Vector3.zero, 40.0f, 2.0f, 0.01f, 1.2f);
			}
			else
			{
			//	Gizmos.DrawFrustum(Vector3.zero, 40.0f, 0.2f, 0.0f, 1.0f);
			}
		}
	}
}
