using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	public static class GlobalIlluminationExtractor 
	{
		static Texture [] giTextures = null;
		static public Texture [] GetTextures()
		{
			if (giTextures != null)
			{
				for(int i=0;i<giTextures.Length; i++)
				{
					if (giTextures[i] == null)
					{ 
						giTextures = null;
						break;
					}
				}
			}
			if (giTextures == null || giTextures.Length != LightmapSettings.lightmaps.Length)
			{
				if (LightmapSettings.lightmaps.Length == 0)
				{
					giTextures = null;
					return null;
				}
				giTextures = new Texture[LightmapSettings.lightmaps.Length];
				for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
				{
					giTextures[i] = LightmapSettings.lightmaps[i].lightmapColor;
				}
            }
            return giTextures;
		}
	}
}