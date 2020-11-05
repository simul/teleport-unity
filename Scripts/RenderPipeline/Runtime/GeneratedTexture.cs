using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	class GeneratedTexture
	{
		// Recreate Unity's unityAttenuation 1024x1 R16 texture, because Unity doesn't expose it.
		static public GeneratedTexture unityAttenuationTexture = new GeneratedTexture(1024, 1, "Teleport/StandardAttenuation", RenderTextureFormat.R16);
		// Recreate Unity's soft texture as Unity doesn't expose it.
		static public GeneratedTexture unitySoftTexture = new GeneratedTexture(128, 128, "Teleport/StandardSoftTexture", RenderTextureFormat.ARGB32);

		public RenderTexture renderTexture = null;
		Shader shader = null;
		Material material = null;
		string shaderName="";
		int width = 0;
		int height = 0;
		RenderTextureFormat renderTextureFormat;
		public GeneratedTexture(int w,int h,string shad, RenderTextureFormat fmt)
		{
			width = w;
			height = h;
			shaderName = shad;
			renderTextureFormat = fmt;
		}
		public void EnsureTexture(ScriptableRenderContext context)
		{
			if (renderTexture == null)
			{
				int mipct = 1;
				int size = Math.Min(width, height);
				while ((1 << (mipct-1)) <size)
					mipct++;
				RenderTextureDescriptor renderTextureDescriptor=new RenderTextureDescriptor();
				renderTextureDescriptor.dimension = TextureDimension.Tex2D;
				renderTextureDescriptor.width = width;
				renderTextureDescriptor.height = height;
				renderTextureDescriptor.colorFormat = renderTextureFormat;
				renderTextureDescriptor.mipCount = mipct;
				renderTextureDescriptor.volumeDepth = 1;
				renderTextureDescriptor.msaaSamples = 1;
				renderTexture = new RenderTexture(renderTextureDescriptor);
				renderTexture.autoGenerateMips = true;
				if (material == null)
				{
					shader = Shader.Find(shaderName);
					if (shader != null)
					{
						material = new Material(shader);
					}
					else
					{
						Debug.LogError(shaderName+" resource not found!");
						return;
					}
				}
			}
			// Without this check the texture can be invalid.
			if (!renderTexture.IsCreated())
			{
				CommandBuffer buffer = CommandBufferPool.Get(shaderName);
				buffer.SetRenderTarget(renderTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

				buffer.Blit(Texture2D.whiteTexture, renderTexture, material);
				context.ExecuteCommandBuffer(buffer);
			}
		}
	}
}
