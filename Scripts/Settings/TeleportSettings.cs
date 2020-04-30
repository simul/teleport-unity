using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
		public const string k_TeleportSettingsPath = "TeleportVR/TeleportSettings";
		//! Objects with this tag will be streamed; leaving it blank will cause it to just use the layer mask.
		public string TagToStream = "TeleportStreamable";
		//! Mask of the physics layers the user can choose to stream.
		public LayerMask LayersToStream;
		public static TeleportSettings GetOrCreateSettings()
		{
			if(teleportSettings==null)
				teleportSettings=Resources.Load<TeleportSettings>(k_TeleportSettingsPath);
#if UNITY_EDITOR
			if (teleportSettings == null)
			{
				teleportSettings = ScriptableObject.CreateInstance<TeleportSettings>();
				AssetDatabase.CreateAsset(teleportSettings, "Assets/Resources/" + k_TeleportSettingsPath+".asset");
				AssetDatabase.SaveAssets();
			}
#endif
			return teleportSettings;
		}
	}
}