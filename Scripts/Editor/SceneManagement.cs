using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace teleport
{
    public class SceneManagement
    {
        [MenuItem("Teleport VR/Open Default Scene")]
        public static void OpenResourceWindow()
        {
            var settings = TeleportSettings.GetOrCreateSettings();
            string extension = ".unity";
            string scene = settings.defaultScene;
            if (scene.Length > 0 && !scene.EndsWith(extension))
            {
                scene += extension;
            }
            EditorSceneManager.OpenScene(scene);

            scene = settings.additiveScene;
               
            if (scene.Length > 0)
            {
                if (!scene.EndsWith(extension))
                {
                    scene += extension;
                }
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
            }
        }
    }
}