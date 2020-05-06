using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace teleport
{
	// Create TeleportSettingsProvider by deriving from SettingsProvider:
	class TeleportSettingsProvider : SettingsProvider
	{
		private static SerializedObject m_TeleportSettings=null;
		public static TeleportSettings teleportSettings = null;
		static LayerMask LayerMaskField(string label, LayerMask layerMask)
		{
			List<string> layers = new List<string>();
			List<int> layerNumbers = new List<int>();

			for (int i = 0; i < 32; i++)
			{
				string layerName = LayerMask.LayerToName(i);
				if (layerName != "")
				{
					layers.Add(layerName);
					layerNumbers.Add(i);
				}
			}
			int maskWithoutEmpty = 0;
			for (int i = 0; i < layerNumbers.Count; i++)
			{
				if (((1 << layerNumbers[i]) & layerMask.value) > 0)
					maskWithoutEmpty |= (1 << i);
			}
			maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
			int mask = 0;
			for (int i = 0; i < layerNumbers.Count; i++)
			{
				if ((maskWithoutEmpty & (1 << i)) > 0)
					mask |= (1 << layerNumbers[i]);
			}
			layerMask.value = mask;
			return layerMask;
		}
		public TeleportSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
			: base(path, scope)
		{
		}

		public static bool IsSettingsAvailable()
		{
			if (teleportSettings != null)
				return true;
			teleportSettings=TeleportSettings.GetOrCreateSettings();
			return teleportSettings != null;
		}

		public override void OnGUI(string searchContext)
		{
			if (teleportSettings == null)
				teleportSettings = TeleportSettings.GetOrCreateSettings();
			// Use IMGUI to display UI:
			teleportSettings.TagToStream = EditorGUILayout.TextField("Tag to Stream",teleportSettings.TagToStream);
			teleportSettings.LayersToStream = LayerMaskField("Layers", teleportSettings.LayersToStream);
			teleportSettings.discoveryPort = (uint)EditorGUILayout.IntField("Discovery Port", (int)teleportSettings.discoveryPort);
			teleportSettings.listenPort = (uint)EditorGUILayout.IntField("Listen Port", (int)teleportSettings.listenPort);
			teleportSettings.connectionTimeout = EditorGUILayout.IntField("Timeout", teleportSettings.connectionTimeout);
			//EditorGUILayout.PropertyField(m_TeleportSettings.FindProperty("TagToStream"));
			// Force it to save:
			EditorUtility.SetDirty(teleportSettings);
		}

		// Register the SettingsProvider
		[SettingsProvider]
		public static SettingsProvider CreateTeleportSettingsProvider()
		{
			if (IsSettingsAvailable())
			{
				var provider = new TeleportSettingsProvider("Project/Teleport VR", SettingsScope.Project);

				// Automatically extract all keywords from the Styles.
				//if(m_TeleportSettings==null)
				//	m_TeleportSettings = TeleportSettings.GetSerializedSettings();
				//provider.keywords = GetSearchKeywordsFromSerializedObject(m_TeleportSettings);
				return provider;
			}

			// Settings Asset doesn't exist yet; no need to display anything in the Settings window.
			return null;
		}
	}
}
