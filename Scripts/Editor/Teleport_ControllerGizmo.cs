using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_ControllerGizmo : MonoBehaviour
	{
		[DrawGizmo(GizmoType.Selected | GizmoType.NotInSelectionHierarchy)]
		static void DrawClientspaceRoot(Teleport_Controller controller, GizmoType gizmoType)
		{
			Gizmos.color = new Color(1.0f, 0.3f, 0.3f);
			// Draws a 5 unit long red line in front of the object
			float pointing_length = 0.8f;
			float axis_size = 0.3f;
			Gizmos.DrawRay(controller.transform.position, controller.transform.forward * pointing_length);
			Gizmos.color = new Color(0.0f, 0.3f, 1.0f);
			Gizmos.DrawRay(controller.transform.position, controller.transform.right * axis_size);
			Gizmos.color = new Color(0.0f, 1.0f, 0.0f);
			Gizmos.DrawRay(controller.transform.position, controller.transform.up * axis_size);
		}
	}
}
