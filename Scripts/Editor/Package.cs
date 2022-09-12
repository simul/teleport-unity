using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace teleport
{ 
	public class Package : MonoBehaviour
	{
		[MenuItem("Teleport VR/Export Package")]
		public static void MenuExportPackage()
		{
			ExportPackage("TeleportVRUnity.unitypackage");
		}
		public static void ExportPackageCmd()
		{
			Application.SetStackTraceLogType(LogType.Error | LogType.Assert | LogType.Exception | LogType.Warning | LogType.Log, StackTraceLogType.None);
			string f = CommandLineReader.GetCustomArgument("Filename");
			f = f.Replace("\"", "");
			f = f.Replace("\\", "/");
			UnityEngine.Debug.Log("ExportPackageCmdLine " + f );
			ExportPackage(f);
		}
		public static void ExportPackage(string fileName)
		{
			List<string> paths = new List<string>();
			ExportPackageOptions options = ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies;
			paths.Add("Assets/Teleport");
			AssetDatabase.ExportPackage(paths.ToArray(), fileName, options);
			UnityEngine.Debug.Log("Exported: " + fileName);
		}
	}
}