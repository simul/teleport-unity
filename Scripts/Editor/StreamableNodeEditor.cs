using UnityEditor;
using UnityEngine;

namespace teleport
{
	[CustomEditor(typeof(teleport.StreamableNode))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class StreamableNodeEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			teleport.StreamableNode streamableNode = (teleport.StreamableNode)target;
			bool enabled=GUI.enabled;
			GUI.enabled=false;
			EditorGUILayout.LongField((long)streamableNode.nodeID);
			EditorGUILayout.Vector3Field(" Linear Velocity",streamableNode.stageSpaceVelocity);
			EditorGUILayout.Vector3Field("Angular Velocity", streamableNode.stageSpaceAngularVelocity);
			GUI.enabled = enabled;
		}
	}
}