using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SCServer;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace teleport
{
	// Create a new type of Settings Asset.
	[System.Serializable]
	public class TeleportSettings : ScriptableObject
	{
		static private TeleportSettings teleportSettings=null;
		// We always store the settings in this path:
		public const string k_TeleportSettingsPath = "TeleportVR";
		public const string k_TeleportSettingsFilename = "TeleportSettings";
		//! Objects with this tag will be streamed; leaving it blank will cause it to just use the layer mask.
		public string TagToStream = "TeleportStreamable";

		[Header("Connections")]
		public uint listenPort = 10500u;
		public uint discoveryPort = 10600u;
		public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.
										  //! Mask of the physics layers the user can choose to stream.

		[Header("Geometry")]
		public uint moveUpdatesPerSecond = 20;

		public CasterSettings casterSettings =new CasterSettings();
		public LayerMask LayersToStream;
		public static void EnsureAssetPath(string requiredPath)
		{
			var settings_folders = requiredPath.Split('/');
			string path = "";
			string fullpath = "";
			for (int i = 0; i < settings_folders.Length; i++)
			{
				fullpath = path + (i > 0 ? "/" : "") + settings_folders[i];
				if (!AssetDatabase.IsValidFolder(fullpath))
				{
					AssetDatabase.CreateFolder(path, settings_folders[i]);
				}
				path = fullpath;
			}
		}
		public static TeleportSettings GetOrCreateSettings()
		{
			if(teleportSettings==null)
				teleportSettings=Resources.Load<TeleportSettings>(k_TeleportSettingsPath + "/" + k_TeleportSettingsFilename);
#if UNITY_EDITOR
			if (teleportSettings == null)
			{
				teleportSettings = ScriptableObject.CreateInstance<TeleportSettings>();
				EnsureAssetPath("Assets/Resources" + k_TeleportSettingsPath);
				AssetDatabase.CreateAsset(teleportSettings, "Assets/Resources/" + k_TeleportSettingsPath + "/"+ k_TeleportSettingsFilename+".asset");
				AssetDatabase.SaveAssets();
			}
#endif
			return teleportSettings;
		}
	}
}