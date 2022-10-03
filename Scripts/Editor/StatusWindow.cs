using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace teleport
{
	using uid = System.UInt64;
	public class StatusWindow : EditorWindow
	{
		[MenuItem("Teleport VR/Live Server Status")]
		public static void OpenStatusWindow()
		{
			StatusWindow window = GetWindow<StatusWindow>(false, "Teleport Server Status");
			window.minSize = new Vector2(600, 200);
			window.Show();
		}
		private Vector2 scrollPosition_client;
		uid selected_client_uid=0;
		private GUIStyle scrollwindowStyle = new GUIStyle();
		private void OnGUI()
		{
			scrollwindowStyle = GUI.skin.box;
			EditorGUILayout.Separator();
			EditorGUILayout.BeginFoldoutHeaderGroup(true,GUIContent.none);
			EditorGUILayout.EndFoldoutHeaderGroup();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Client Sessions");
			
			scrollPosition_client=EditorGUILayout.BeginScrollView(scrollPosition_client, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, scrollwindowStyle, GUILayout.Width(200));
			foreach (var s in Teleport_SessionComponent.sessions)
			{
				if (EditorGUILayout.LinkButton(s.Key.ToString()))
				{ 
					selected_client_uid=s.Key;
				}
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
			ClientPanel(selected_client_uid);
			EditorGUILayout.EndHorizontal();
		}
		private Vector2 scrollPosition_streamed;
		private void ClientPanel(uid clientID)
		{
			Teleport_SessionComponent session = Teleport_SessionComponent.GetSessionComponent(clientID);
			if(session==null)
				return;
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Uid", clientID.ToString());
			EditorGUILayout.LabelField("IP Address", session.GetClientIP());
			var networkStats=session.GetNetworkStats();
			EditorGUILayout.LabelField("available bandwidth",string.Format("{0:F3} mb/s", networkStats.bandwidth));
			EditorGUILayout.LabelField("avg bandwidth used" ,string.Format("{0:F3} mb/s", networkStats.avgBandwidthUsed));
			EditorGUILayout.LabelField("max bandwidth used" ,string.Format("{0:F3} mb/s", networkStats.maxBandwidthUsed));
			//session.ShowOverlay(0, 0, clientFont);
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			GeometryStreamingService geometryStreamingService =session.GeometryStreamingService;
			int nodeCount = geometryStreamingService.GetStreamedObjectCount();
			EditorGUILayout.LabelField("Nodes", string.Format("{0}", nodeCount));

			scrollPosition_streamed=EditorGUILayout.BeginScrollView(scrollPosition_streamed,false, true, GUI.skin.verticalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.textField);

			List<GameObject> streamedGameObjects = geometryStreamingService.GetStreamedObjects();
			//List nodes to the maximum.
			EditorGUILayout.BeginVertical();
			for (int i = 0; i < streamedGameObjects.Count; i++)
			{
				GameObject node = streamedGameObjects[i];
				uid nodeID = geometrySource.FindResourceID(node);
				EditorGUILayout.LabelField(string.Format("{0}", nodeID),string.Format("{0}", node.name));
			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}
	}
}