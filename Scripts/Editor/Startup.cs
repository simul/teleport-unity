using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.CompilerServices;

// to play, Ctrl-ZERO rather than Ctrl-P
// (the zero key, is near the P key, so it's easy to remember)
// simply insert the actual name of your opening scene
// "__preEverythingScene" on the second last line of code below.
namespace teleport
{ 
	[InitializeOnLoad]
	public static class Startup
	{
		private static string GetThisFilePath([CallerFilePath] string path = null)
		{
			return path;
		}

		public static string GizmosPath;
		static Startup()
		{
			var thisPath = System.IO.Path.GetDirectoryName(GetThisFilePath());
			GizmosPath = System.IO.Path.GetFullPath(thisPath + "/../../Resources/Gizmos") ;
		}
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void OnBeforeSceneLoadRuntimeMethod()
		{
			//	Debug.Log("Before first Scene loaded");
		}
		// click command-0 to go to the prelaunch scene and then play
		[MenuItem("Teleport VR/Start Scene %5", false, 2004)]
		public static void PlayDefaultScene()
		{
			if (EditorApplication.isPlaying == true)
			{
				EditorApplication.isPlaying = false;
				return;
			}
			var g= GeometrySource.GetGeometrySource();
			if(g==null)
				return;
			if(g.CheckForErrors()==false)
			{
				Debug.LogError("GeometrySource.CheckForErrors() failed. Run will not proceed.");
				EditorUtility.DisplayDialog("Warning","This scene has errors.","OK");
				return;
			}
			//EditorApplication.SaveCurrentSceneIfUserWantsTo();
			//EditorApplication.OpenScene(
			//	"Assets/stuff/Scenes/__preEverythingScene.unity");
			EditorApplication.isPlaying = true;
		}
	}
}