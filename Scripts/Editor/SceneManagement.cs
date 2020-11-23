using System.Collections;
using System.Collections.Generic;
using UnityEditor;
namespace teleport
{
    public class SceneManagement
    {
        [MenuItem("Teleport VR/Open Default Scene")]
        public static void OpenResourceWindow()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TeleportSettings.GetOrCreateSettings().defaultScene);
        }
    }
}