﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class Teleport_ClientspaceRootGizmo : MonoBehaviour
{
	[DrawGizmo(GizmoType.Selected|GizmoType.NotInSelectionHierarchy)]
	static void DrawClientspaceRoot(Teleport_ClientspaceRoot clientspaceRoot,GizmoType gizmoType)
	{
		Color colr=new Color(0.2f,0.3f,1.0f,1.0f);
		Gizmos.color = colr;
		float r = 1.0f;
		for(int h=0;h<3;h++)
		{
			r=r*0.5f;
			for (int i = 0; i < 33; i++)
			{
				float angle1 = 2.0f * Mathf.PI * ((float)i) / 32.0f;
				float angle2 = 2.0f * Mathf.PI * ((float)(i + 1)) / 32.0f;
				float c1 = Mathf.Cos(angle1);
				float c2 = Mathf.Cos(angle2);
				float s1 = Mathf.Sin(angle1);
				float s2 = Mathf.Sin(angle2);
				Vector3 p1 = clientspaceRoot.transform.position + r * (Vector3.right * c1 + Vector3.forward * s1);
				Vector3 p2 = clientspaceRoot.transform.position + r * (Vector3.right * c2 + Vector3.forward * s2);
				Gizmos.DrawLine(p1, p2);
			}
			colr.a=colr.a*0.5f;
			Gizmos.color = colr;
		}
	}
}
