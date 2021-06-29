using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	public static class GlobalIlluminationExtractor 
	{
		static Texture giTexture=null;
		static public Texture GetTexture()
		{
			if(giTexture==null)
			{
				giTexture=LightmapSettings.lightmaps[0].lightmapColor;
				//Resources.Load("TeleportLightmapRenderTexture") as Texture;
			}
			return giTexture;
		}
	}
}