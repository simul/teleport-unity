using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace teleport
{
    public class CasterResourceWindow : EditorWindow
    {
        //References to assets.
        private GeometrySource geometrySource;
        private ComputeShader textureShader;

        //List of found/extracted data.
        private GameObject[] streamedObjects = new GameObject[0];
        private RenderTexture[] renderTextures = new RenderTexture[0];

        //GUI variables that control user-changeable properties.
        private Vector2 scrollPosition_gameObjects;
        private Vector2 scrollPosition_textures;
        private bool foldout_gameObjects = false;
        private bool foldout_textures = false;

        [MenuItem("Window/Caster Resource Window")]
        public static void OpenResourceWindow()
        {
            CasterResourceWindow window = GetWindow<CasterResourceWindow>(false, "Caster Resource Window");
            window.Show();
        }

        private void Awake()
        {
            geometrySource = GeometrySource.GetGeometrySource();

            string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
            textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));
        }

        private void OnGUI()
        {
            foldout_gameObjects = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_gameObjects, "GameObjects (" + streamedObjects.Length + ")");
            if(foldout_gameObjects)
            {
                scrollPosition_gameObjects = EditorGUILayout.BeginScrollView(scrollPosition_gameObjects);

                foreach(GameObject gameObject in streamedObjects)
                {
                    EditorGUILayout.BeginHorizontal();

                    using(new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(gameObject, typeof(GameObject), true);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            foldout_textures = EditorGUILayout.BeginFoldoutHeaderGroup(foldout_textures, "Textures (" + renderTextures.Length + ")");
            if(foldout_textures)
            {
                scrollPosition_textures = EditorGUILayout.BeginScrollView(scrollPosition_textures);

                foreach(RenderTexture renderTexture in renderTextures)
                {
                    if(!renderTexture) break;

                    EditorGUILayout.BeginHorizontal();

                    using(new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(renderTexture, typeof(RenderTexture), true);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.BeginHorizontal();

            if(GUILayout.Button("Find Streamed Objects")) FindStreamedObjects();
            if(GUILayout.Button("Extract Object Data")) ExtractGeometryData();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if(GUILayout.Button("Clear Cached Data"))
            {
                geometrySource.ClearData();
                streamedObjects = new GameObject[0];

                foreach(RenderTexture texture in renderTextures)
                {
                    if(texture) texture.Release();
                }
                renderTextures = new RenderTexture[0];
            }

            //Create a new file explorer window, displaying the folder where compressed textures are saved.
            if(GUILayout.Button("Open Compressed Texture Folder"))
            {
                //File explorer will treat paths with forward slashes as invalid.
                System.Diagnostics.Process.Start("explorer.exe", geometrySource.compressedTexturesFolderPath.Replace('/', '\\'));
            }

            if(GUILayout.Button("Force Extract"))
            {
                geometrySource.ClearData();
                ExtractGeometryData();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void FindStreamedObjects()
        {
            CasterMonitor monitor = CasterMonitor.GetCasterMonitor();

            streamedObjects = GameObject.FindGameObjectsWithTag(monitor.tagToStream);
            streamedObjects = streamedObjects.Where(x => (monitor.layersToStream & (1 << x.layer)) != 0).ToArray();
        }

        private void ExtractGeometryData()
        {
            FindStreamedObjects();

            foreach(GameObject gameObject in streamedObjects)
            {
                geometrySource.AddNode(gameObject, true);
            }

            ExtractTextures();
        }

        private void ExtractTextures()
        {
            //According to the Unity docs, we need to call Release() on any render textures we are done with.
            for(int i = geometrySource.texturesWaitingForExtraction.Count; i < renderTextures.Length; i++)
            {
                if(renderTextures[i]) renderTextures[i].Release();
            }
            //Resize the array, instead of simply creating a new one, as we want to keep the same render textures for quicker debugging.
            Array.Resize(ref renderTextures, geometrySource.texturesWaitingForExtraction.Count);

            //Extract texture data with a compute shader, rip the data, and store in the native C++ plugin.
            for(int i = 0; i < renderTextures.Length; i++)
            {
                Texture2D sourceTexture = (Texture2D)geometrySource.texturesWaitingForExtraction[i].unityTexture;

                //If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
                if(renderTextures[i] == null) renderTextures[i] = new RenderTexture(sourceTexture.width, sourceTexture.height, 0);
                else
                {
                    renderTextures[i].Release();
                    renderTextures[i].width = sourceTexture.width;
                    renderTextures[i].height = sourceTexture.height;
                    renderTextures[i].depth = 0;
                }

                renderTextures[i].enableRandomWrite = true;
                renderTextures[i].name = geometrySource.texturesWaitingForExtraction[i].id.ToString();
                renderTextures[i].Create();

                //Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
                UnityEditor.TextureImporterType textureType = ((UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(sourceTexture))).textureType;
                int kernelHandle = textureShader.FindKernel((textureType == UnityEditor.TextureImporterType.NormalMap ? "ExtractNormalMap" : "ExtractTexture") + (UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma ? "Gamma" : "Linear"));

                textureShader.SetTexture(kernelHandle, "Source", sourceTexture);
                textureShader.SetTexture(kernelHandle, "Result", renderTextures[i]);
                textureShader.Dispatch(kernelHandle, sourceTexture.width / 8, sourceTexture.height / 8, 1);

                //Rip data from render texture, and store in GeometryStore.
                {
                    //Read pixel data into Texture2D, so that it can be read.
                    Texture2D readTexture = new Texture2D(renderTextures[i].width, renderTextures[i].height, renderTextures[i].graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                    {
                        RenderTexture oldActive = RenderTexture.active;

                        RenderTexture.active = renderTextures[i];
                        readTexture.ReadPixels(new Rect(0, 0, renderTextures[i].width, renderTextures[i].height), 0, 0);
                        readTexture.Apply();

                        RenderTexture.active = oldActive;
                    }
                    Color32[] pixelData = readTexture.GetPixels32();

                    avs.Texture textureData = geometrySource.texturesWaitingForExtraction[i].textureData;
                    textureData.format = avs.TextureFormat.RGBA8;
                    textureData.bytesPerPixel = 4;

                    int byteSize = Marshal.SizeOf<byte>();
                    textureData.dataSize = (uint)(pixelData.Length * 4 * byteSize);
                    textureData.data = Marshal.AllocCoTaskMem((int)textureData.dataSize);

                    int byteOffset = 0;
                    foreach(Color32 pixel in pixelData)
                    {
                        Marshal.WriteByte(textureData.data, byteOffset, pixel.r);
                        byteOffset += byteSize;

                        Marshal.WriteByte(textureData.data, byteOffset, pixel.g);
                        byteOffset += byteSize;

                        Marshal.WriteByte(textureData.data, byteOffset, pixel.b);
                        byteOffset += byteSize;

                        Marshal.WriteByte(textureData.data, byteOffset, pixel.a);
                        byteOffset += byteSize;
                    }

                    geometrySource.AddTextureData(sourceTexture, textureData);

                    Marshal.FreeCoTaskMem(textureData.data);
                }
            }

            geometrySource.CompressTextures();
        }
    }
}
