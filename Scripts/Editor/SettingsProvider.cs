using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace teleport
{
	// Create TeleportSettingsProvider by deriving from SettingsProvider:
	class TeleportSettingsProvider : SettingsProvider
	{
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

		static string WebcamField(string label, string currentValue)
		{
			List<string> deviceNames = new List<String>();
			int selected = 0;
			for(int i = 0; i < WebCamTexture.devices.Length; ++i)
			{
				string name = WebCamTexture.devices[i].name;
				deviceNames.Add(name);
				if (name == currentValue)
				{
					selected = i;
				}
			}
			selected = EditorGUILayout.Popup(label, selected, deviceNames.ToArray());
			if (deviceNames.Count > 0)
			{
				return deviceNames[selected];
			}
			return "";
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
		Tuple<int, string, bool>[] headers =
		{
			Tuple.Create(1,  "SRT", false),
			Tuple.Create(3,  "General", false),
			Tuple.Create(5,  "Geometry", false),
			Tuple.Create(28, "Video", false),
			Tuple.Create(2,  "Audio", false),
			Tuple.Create(6,  "Debugging", false),
			Tuple.Create(4,  "Streamed Textures", false),
			Tuple.Create(2,  "Camera", false),
			Tuple.Create(5,  "Lighting", false),
			Tuple.Create(1,  "Input", false),
		};

		public override void OnGUI(string searchContext)
		{
			if (teleportSettings == null)
				teleportSettings = TeleportSettings.GetOrCreateSettings();
			// Use IMGUI to display UI:
			teleportSettings.cachePath = EditorGUILayout.TextField("Cache path", teleportSettings.cachePath);
			teleportSettings.defaultScene = EditorGUILayout.TextField("Default Scene", teleportSettings.defaultScene);
			teleportSettings.additiveScene = EditorGUILayout.TextField("Additive Scene", teleportSettings.additiveScene);
			teleportSettings.TagToStream = EditorGUILayout.TagField("Tag to Stream",teleportSettings.TagToStream);
			teleportSettings.LayersToStream = LayerMaskField("Layers", teleportSettings.LayersToStream);
			teleportSettings.highlightStreamables = EditorGUILayout.Toggle("Highlight Streamables", teleportSettings.highlightStreamables);
			teleportSettings.highlightStreamableColour = EditorGUILayout.ColorField("Highlight Colour", teleportSettings.highlightStreamableColour);
			teleportSettings.highlightNonStreamables = EditorGUILayout.Toggle("Highlight Non-Streamables", teleportSettings.highlightNonStreamables);
			teleportSettings.highlightNonStreamableColour = EditorGUILayout.ColorField("Highlight Non-Streamable Colour", teleportSettings.highlightNonStreamableColour);
			teleportSettings.moveUpdatesPerSecond = (uint)EditorGUILayout.IntField("Move Updates Per Second", (int)teleportSettings.moveUpdatesPerSecond);
			
			string tempPorts= EditorGUILayout.TextField("Signaling Port/s", teleportSettings.signalingPorts);

			// does this validate?
			string pattern = @"^(\d+)(,\d+)*$";
			Match m = Regex.Match(tempPorts, pattern, RegexOptions.IgnoreCase);
			if (m.Success)
				teleportSettings.signalingPorts= tempPorts;

			teleportSettings.connectionTimeout = EditorGUILayout.IntField("Timeout", teleportSettings.connectionTimeout);
			teleportSettings.clientIP = EditorGUILayout.TextField("Restrict Client IP", teleportSettings.clientIP);
			teleportSettings.webcam = WebcamField("Webcam", teleportSettings.webcam);
			teleportSettings.renderMainCamera = EditorGUILayout.Toggle("Render Main Camera", teleportSettings.renderMainCamera);
			teleportSettings.certPath = EditorGUILayout.TextField("SSL Cert Path", teleportSettings.certPath);
			teleportSettings.privateKeyPath = EditorGUILayout.TextField("Private Key Path", teleportSettings.privateKeyPath);

			foreach (var prop in typeof(teleport.ServerSettings).GetProperties())
			{
				EditorGUILayout.LabelField(prop.Name);
			}
			var orderedFields = typeof(teleport.ServerSettings).GetFields()
														 .OrderBy(field => field.MetadataToken).ToArray<System.Reflection.FieldInfo>();
		
			int row = 0;
			for(int i=0;i< headers.Length;i++)
			{
				var section =headers[i];
				bool res=EditorGUILayout.BeginFoldoutHeaderGroup(section.Item3, section.Item2);
				if (res != section.Item3)
				{
					section=headers[i]= Tuple.Create(section.Item1, section.Item2, res);
				}
				if (section.Item3)
				{
					for (int r = row; r < row + section.Item1; r++)
					{
						var field = orderedFields[r];
						if (field.FieldType == typeof(Int32))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.IntField(field.Name, (Int32)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(Int64))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.LongField(field.Name, (Int64)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(byte))
						{
							int ival=(int)((byte)field.GetValue(teleportSettings.serverSettings));
							byte bval = (byte)EditorGUILayout.IntField(field.Name, ival);
							field.SetValue(teleportSettings.serverSettings,bval);
						}
						else if (field.FieldType == typeof(float))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.FloatField(field.Name, (float)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(string))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.TextField(field.Name, field.GetValue(teleportSettings.serverSettings).ToString()));
						}
						else if (field.FieldType == typeof(bool))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.Toggle(field.Name, (bool)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(avs.VideoCodec))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.EnumPopup(field.Name, (avs.VideoCodec)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(avs.RateControlMode))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.EnumPopup(field.Name, (avs.RateControlMode)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(teleport.ControlModel))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.EnumPopup(field.Name, (teleport.ControlModel)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(teleport.BackgroundMode))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.EnumPopup(field.Name, (teleport.BackgroundMode)field.GetValue(teleportSettings.serverSettings)));
						}
						else if (field.FieldType == typeof(Color))
						{
							field.SetValue(teleportSettings.serverSettings, EditorGUILayout.ColorField(field.Name, (Color)field.GetValue(teleportSettings.serverSettings)));
						}
						else
							EditorGUILayout.LabelField(field.Name, field.FieldType.ToString() + " " + field.GetValue(teleportSettings.serverSettings).ToString());

					}
				}
				row += section.Item1;
				EditorGUILayout.EndFoldoutHeaderGroup();
			}
			// Force it to save:
			EditorUtility.SetDirty(teleportSettings);
		}

		public override void OnInspectorUpdate()
		{

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
