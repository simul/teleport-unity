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
		int selGridInt = 0;
		private GUIStyle scrollwindowStyle =null;
		private GUIStyle titleStyle=null;
        private GUIStyle verticalScrollbarStyle = null; 
        private void OnGUI()
        {
            if (titleStyle == null|| titleStyle.fontStyle!=FontStyle.Bold) 
			{
				titleStyle = new GUIStyle(GUI.skin.label);
				titleStyle.fontSize = (GUI.skin.label.fontSize * 5) / 4;
				titleStyle.fontStyle = FontStyle.Bold;
				scrollwindowStyle = new GUIStyle( GUI.skin.box);
				verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);

            }
			//EditorGUILayout.Separator();
			//EditorGUILayout.BeginFoldoutHeaderGroup(true,GUIContent.none);
			//EditorGUILayout.EndFoldoutHeaderGroup();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Client Sessions", titleStyle);

			List<string> sessionNames=new List<string>();
			List<uid> sessionUids = new List<uid>();
			foreach (var s in Teleport_SessionComponent.sessions)
			{
				sessionNames.Add(s.Key.ToString());
				sessionUids.Add(s.Key);
			}
			scrollPosition_client =EditorGUILayout.BeginScrollView(scrollPosition_client, false, true, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Width(200));

			selGridInt = GUILayout.SelectionGrid(selGridInt, sessionNames.ToArray(), sessionNames.Count);
			if (selGridInt >= 0 && selGridInt < sessionUids.Count)
				selected_client_uid = sessionUids[selGridInt];
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
			ClientPanel(selected_client_uid);
			EditorGUILayout.EndHorizontal();
		}
		private Vector2 scrollPosition_streamed;
		int selectTexture=-1;
		private void ClientPanel(uid clientID)
		{
			Teleport_SessionComponent session = Teleport_SessionComponent.GetSessionComponent(clientID);
			if(session==null)
				return;
            GeometryStreamingService geometryStreamingService = session.GeometryStreamingService;
            if (geometryStreamingService == null)
                return;
            EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Status", titleStyle);
			EditorGUILayout.LabelField("Uid", clientID.ToString());
			EditorGUILayout.LabelField("IP Address", session.GetClientIP());
			EditorGUILayout.LabelField("Signal", Teleport_SessionComponent.Client_GetSignalingState(clientID).ToString());
            EditorGUILayout.LabelField("Stream", Teleport_SessionComponent.Client_GetStreamingState(clientID).ToString());
            var networkStats=session.GetNetworkStats();
			EditorGUILayout.LabelField("available bandwidth",string.Format("{0:F3} mb/s", networkStats.bandwidth));
			EditorGUILayout.LabelField("avg bandwidth used" ,string.Format("{0:F3} mb/s", networkStats.avgBandwidthUsed));
			EditorGUILayout.LabelField("max bandwidth used" ,string.Format("{0:F3} mb/s", networkStats.maxBandwidthUsed));
			//session.ShowOverlay(0, 0, clientFont);
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			EditorGUILayout.LabelField("Inputs", titleStyle);
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			foreach (var id in session.input.GetFloatStateIDs())
			{
				var def = teleportSettings.inputDefinitions[id];
				EditorGUILayout.LabelField(def.name + ": " + session.input.GetFloatState(id).ToString());
			}
			foreach (var id in session.input.GetBooleanStateIDs())
			{
				var def = teleportSettings.inputDefinitions[id];
				EditorGUILayout.LabelField(def.name + ": " + session.input.GetBooleanState(id).ToString());
			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Textures", titleStyle);
			if (session.sceneCaptureComponent)
			{
				List<string> textureNames=new List<string>();
				List<Texture> textures = new List<Texture>();
				if (session.sceneCaptureComponent.videoTexture)
				{	
					textureNames.Add("Video Texture");
					textures.Add(session.sceneCaptureComponent.videoTexture);
				}
				if (session.sceneCaptureComponent.UnfilteredCubeTexture)
				{
					textureNames.Add("Unfiltered Environment");
					textures.Add(session.sceneCaptureComponent.UnfilteredCubeTexture);
				}
				if (session.sceneCaptureComponent.SpecularCubeTexture)
				{
					textureNames.Add("Specular");
					textures.Add(session.sceneCaptureComponent.SpecularCubeTexture);
				}
				if (session.sceneCaptureComponent.DiffuseCubeTexture)
				{
					textureNames.Add("Diffuse");
					textures.Add(session.sceneCaptureComponent.DiffuseCubeTexture);
				}
				int sel = GUILayout.SelectionGrid(selectTexture, textureNames.ToArray(), 1);
				if (sel != selectTexture)
				{
					selectTexture=sel;
					UnityEditor.Selection.activeObject=textures[sel];
					UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Inspector");
				}
				if (sel >= 0 && sel < textures.Count && UnityEditor.Selection.activeObject != textures[sel])
					selectTexture = sel = -1;
			}
			EditorGUILayout.EndVertical();
			//GUILayout.Box()
			{
				EditorGUILayout.BeginVertical();
				int nodeCount = geometryStreamingService.GetStreamedObjectCount();
				EditorGUILayout.LabelField(string.Format("{0} Nodes", nodeCount), titleStyle);
				scrollPosition_streamed = EditorGUILayout.BeginScrollView(scrollPosition_streamed);//,false, true, verticalScrollbarStyle, verticalScrollbarStyle, GUI.skin.textField);

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
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}
	}
}