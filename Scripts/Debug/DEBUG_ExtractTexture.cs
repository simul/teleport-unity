using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DEBUG_ExtractTexture : MonoBehaviour
{
    public ComputeShader shader;
    public Texture2D[] sourceTextures;
    public RenderTexture[] renderTextures;

    void Start()
    {
        //Wipe the array on session start. Using a pre-existing render texture changes the format, and thus breaks the extraction.
        renderTextures = new RenderTexture[0];
        ExtractTextures();
    }

    private void Update()
    {
        if(Input.GetKeyUp(KeyCode.Keypad0)) //Save first render texture to file.
        {
            RenderTexture oldActive = RenderTexture.active;

            //ReadPixels(...) reads from the active render texture.
            RenderTexture.active = renderTextures[0];

            Texture2D saveTexture = new Texture2D(renderTextures[0].width, renderTextures[0].height, renderTextures[0].graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            saveTexture.ReadPixels(new Rect(0, 0, renderTextures[0].width, renderTextures[0].height), 0, 0);
            saveTexture.Apply();
            
            //Save to desktop.
            System.IO.File.WriteAllBytes("C:/Users/George/Desktop/" + sourceTextures[0].name + "_extracted.png", saveTexture.EncodeToPNG());

            //Set the active back to what it was.
            RenderTexture.active = oldActive;
        }
        else if(Input.GetKeyUp(KeyCode.R)) //Re-extract the textures.
        {
            ExtractTextures();
        }
    }

    private void ExtractTextures()
    {
        if(sourceTextures.Length == 0) return;

        //Resize the array, instead of simply creating a new one, as we want to keep the same render textures for quicker debugging.
        Array.Resize(ref renderTextures, sourceTextures.Length);
        for(int i = 0; i < renderTextures.Length; i++)
        {
            //If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
            if(renderTextures[i] == null) renderTextures[i] = new RenderTexture(sourceTextures[i].width, sourceTextures[i].height, 0);
            else
            {
                renderTextures[i].Release();
                renderTextures[i].width = sourceTextures[i].width;
                renderTextures[i].height = sourceTextures[i].height;
                renderTextures[i].depth = 0;
            }

            renderTextures[i].enableRandomWrite = true;
            renderTextures[i].Create();

            //Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
#if UNITY_EDITOR
            UnityEditor.TextureImporterType textureType = ((UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(sourceTextures[i]))).textureType;
            int kernelHandle = shader.FindKernel(textureType == UnityEditor.TextureImporterType.NormalMap ? "ExtractNormalMap" : "ExtractTexture");

            shader.SetTexture(kernelHandle, "Source", sourceTextures[i]);
            shader.SetTexture(kernelHandle, "Result", renderTextures[i]);
            shader.Dispatch(kernelHandle, sourceTextures[i].width / 8, sourceTextures[i].height / 8, 1);
#endif
        }
    }
}
