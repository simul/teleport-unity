using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Teleport_ControllerGizmo : MonoBehaviour
{
	[DrawGizmo(GizmoType.Selected|GizmoType.NotInSelectionHierarchy)]
	static void DrawClientspaceRoot(Teleport_Controller controller,GizmoType gizmoType)
	{
		Gizmos.color = new Color(1.0f,0.3f,0.3f);
        // Draws a 5 unit long red line in front of the object
        float sz=0.3f;
        Gizmos.DrawRay(controller.transform.position, controller.transform.forward * sz);
		Gizmos.color = new Color(0.0f,0.3f,1.0f);
        Gizmos.DrawRay(controller.transform.position, controller.transform.right * sz);
		Gizmos.color = new Color(0.0f,1.0f,0.0f);
        Gizmos.DrawRay(controller.transform.position, controller.transform.up * sz);
	}
}
