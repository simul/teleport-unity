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
			if(session.clientspaceRoot&&session.head)
			{
				Vector3 clientspace_pos=session.clientspaceRoot.transform.position;
				Vector3 head_pos=session.head.transform.position;
				Vector3 p1=new Vector3(head_pos.x,clientspace_pos.y,head_pos.z);
				Gizmos.color = new Color(0.8f,0.3f,1.0f);
				Gizmos.DrawLine(clientspace_pos,p1);
				Gizmos.color = new Color(0.2f,1.0f,0.3f);
				Gizmos.DrawLine(p1,head_pos);
			}
		}
	}
}