using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_HeadGizmo : MonoBehaviour
	{
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawHead(Teleport_Head head, GizmoType gizmoType)
		{
			Gizmos.color = new Color(0.2f, 0.3f, 1.0f);
			Camera camera = head.gameObject.GetComponent<Camera>();
			if(camera)
			{
				Gizmos.matrix = head.transform.localToWorldMatrix;
				Gizmos.DrawFrustum(Vector3.zero, 20.0f, 2.0f, 0.01f, 1.2f);
			}
		}
	}
}
