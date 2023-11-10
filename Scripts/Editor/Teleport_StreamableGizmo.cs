using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class teleport.StreamableRootGizmo : MonoBehaviour
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
		static teleport.StreamableRootGizmo()
		{
			cylinder = GetPrimitive(PrimitiveType.Cylinder);
			sphere = GetPrimitive(PrimitiveType.Sphere);
			cube = GetPrimitive(PrimitiveType.Cube);
		}
		[DrawGizmo(GizmoType.Selected )]
		static void DrawStreamable(teleport.StreamableRoot Streamable, GizmoType gizmoType)
		{
			Gizmos.color = new Color(0.2f, 0.3f, 1.0f);
			Camera camera = Streamable.gameObject.GetComponent<Camera>();

			var bounds = Streamable.GetBounds();
			float rad= bounds.size.magnitude;
			Gizmos.color = new Color(0.0f, 0.7f, 0.8f,0.2f);
			Gizmos.DrawMesh(sphere, 0, bounds.center, Quaternion.LookRotation(Streamable.transform.up, Streamable.transform.forward), new UnityEngine.Vector3(rad, rad, rad));
			Gizmos.color = new Color(0.8f, 0.5f, 0.3f,0.2f);
			Gizmos.DrawMesh(cube, 0, bounds.center, Quaternion.identity, bounds.size);

		}
	}
}
