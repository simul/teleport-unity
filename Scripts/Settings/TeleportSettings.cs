using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using teleport;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	// Create a new type of Settings Asset.
	[System.Serializable]
	public class TeleportSettings : ScriptableObject
	{
		static private TeleportSettings teleportSettings=null;
		// the cache path is global, shared with player instances.
		public string cachePath="";
		// We always store the settings in this path:
		public const string k_TeleportSettingsPath = "TeleportVR";
		public const string k_TeleportSettingsFilename = "TeleportSettings";
		//! Objects with this tag will be streamed; leaving it blank will cause it to just use the layer mask.
		public string TagToStream = "TeleportStreamable";
		public bool highlightStreamables=true;
		public Color highlightStreamableColour=new Color(.5F, .2F, 1.0F, .1F);
		public bool highlightNonStreamables = false;
		public Color highlightNonStreamableColour = new Color(0.5F, 1.0F, 1.0F, .1F);

		[Header("Connections")]
		public uint listenPort = 10500u;
		public uint discoveryPort = 10600u;
		public int connectionTimeout = 13000; //How many millseconds to wait before automatically disconnecting from the client.
		public string clientIP = "127.0.0.1";

		[Header("Security")]
		public string certPath = "";
		public string privateKeyPath = "";

		[Header("Geometry")]
		public uint moveUpdatesPerSecond = 20;

		public ServerSettings serverSettings = new ServerSettings();

		public LayerMask LayersToStream;  //! Mask of the physics layers the user can choose to stream.

		[Header("Input")]
		public List<InputDefinition> inputDefinitions = new List<InputDefinition>();

		/// <summary>
		/// Find the index of a given named input.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public System.UInt16 FindInput(string name)
		{
			for (int i = 0; i < inputDefinitions.Count; i++)
			{
				if(inputDefinitions[i].name==name)
					return (System.UInt16)i;
			}
			return (System.UInt16)(inputDefinitions.Count);
		}
		[Header("Utility")]
		public string defaultScene = "";
		public string additiveScene = "";

		[Header("Webcam")]
		public string webcam;

		[Header("Render Main Camera")]
		public bool renderMainCamera = true;

		public ColorSpace colorSpace =new ColorSpace();
#if UNITY_EDITOR
		public static void EnsureAssetPath(string requiredPath)
		{
			string dir= System.IO.Path.GetDirectoryName(requiredPath);
			dir=dir.Replace("\\","/");
			var settings_folders = dir.Split('/');
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
#endif
		public static TeleportSettings GetOrCreateSettings()
		{
			if(teleportSettings == null)
			{
				teleportSettings = Resources.Load<TeleportSettings>(k_TeleportSettingsPath + "/" + k_TeleportSettingsFilename);
			}

#if UNITY_EDITOR
			if(teleportSettings == null)
			{
				teleportSettings = CreateInstance<TeleportSettings>();
				teleportSettings.LayersToStream.value=LayerMask.NameToLayer("Default");
				EnsureAssetPath("Assets/Resources/" + k_TeleportSettingsPath);
				AssetDatabase.CreateAsset(teleportSettings, "Assets/Resources/" + k_TeleportSettingsPath + "/" + k_TeleportSettingsFilename + ".asset");
				AssetDatabase.SaveAssets();
			}
			teleportSettings.colorSpace = PlayerSettings.colorSpace;
			if(teleportSettings.cachePath=="")
				teleportSettings.cachePath = System.IO.Directory.GetParent(Application.dataPath).ToString().Replace("\\","/") + "/teleport_cache";

#endif
			return teleportSettings;
		}
	}
}