using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace teleport
{
	public class TextCanvasGizmo : MonoBehaviour
	{
		[DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
		static void DrawTextCanvas(TextCanvas textCanvas, GizmoType gizmoType)
		{
			Color colr = textCanvas.colour;
			Gizmos.color = colr;
			Gizmos.matrix = textCanvas.transform.localToWorldMatrix;
			float x=textCanvas.width/2.0f;
			float y=textCanvas.height/2.0f;
			Vector3 c1=new Vector3(-x,0,-y);
			Vector3 c2=new Vector3(x,0,-y);
			Vector3 c3=new Vector3(x,0,y);
			Vector3 c4=new Vector3(-x,0,y);
			Gizmos.DrawLine(c1,c2);
			Gizmos.DrawLine(c2,c3);
			Gizmos.DrawLine(c3,c4);
			Gizmos.DrawLine(c4,c1);
			
			Vector3 l1,l2;
			Color colr2 = new Color(1.0f, 0.6f, 0.2f, 0.5f);
			Gizmos.color = colr2;
			for (int i = 0; i < 10; i++)
			{
				float interp=(float)i/10.0f;
				l1=c1*(1.0f-interp)+c4*interp;
				l2=c2*(1.0f-interp)+c3*interp;
				Gizmos.DrawLine(l1,l2);
			}
			//TextGizmo.Instance.DrawText(Camera.current,textCanvas.transform.position,textCanvas.text);
		}
	}
}