using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class Teleport_SessionComponentGizmo : MonoBehaviour
	{
		[DrawGizmo(GizmoType.Selected|GizmoType.NotInSelectionHierarchy)]
		static void DrawSessionComponent(Teleport_SessionComponent session,GizmoType gizmoType)
		{
			Gizmos.color = new Color(0.2f,0.3f,1.0f);
			if(session.clientspaceRoot&&session.head)
			{
				Vector3 clientspace_pos=session.clientspaceRoot.transform.position;
				Vector3 head_pos=session.head.transform.position;
				Vector3 p1=new Vector3(head_pos.x,clientspace_pos.y,head_pos.z);
				Gizmos.DrawLine(clientspace_pos,p1);
				Gizmos.DrawLine(p1,head_pos);
			}
		}
	}
}