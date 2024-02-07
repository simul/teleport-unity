using UnityEngine;
using System.Collections.Generic;
 
public class TextGizmo
{
    #region Singleton
    private class Singleton
    {
        static Singleton()
        {
            if (instance == null)
            {
                instance = new TextGizmo();
            }
        }
       
        internal static readonly TextGizmo instance;
    }
   
    public static TextGizmo Instance { get { return Singleton.instance; } }
   
    #endregion
   
    private const int CHAR_TEXTURE_HEIGHT = 11;
    private const int CHAR_TEXTURE_WIDTH = 8;
    private const string characters = " !#%'()+,-.0123456789;=abcdefghijklmnopqrstuvwxyz_{}~\\?\":/*";
 
	struct FontMap
	{
		public Dictionary<char,teleport.Glyph> glyphs;
	}
	struct FontAtlas
	{
		public Dictionary<int,FontMap> fontMaps;
	}
    private Dictionary<string,FontAtlas> fontAtlases;
    private Dictionary<char, string> specialChars;
 
    private TextGizmo()
    {
        specialChars = new Dictionary<char, string>();
        specialChars.Add('\\', "backslash");
        specialChars.Add('?', "questionmark");
        specialChars.Add('"', "quotes");
        specialChars.Add(':', "colon");
        specialChars.Add('/', "slash");
        specialChars.Add('*', "star");
	}
	private FontMap GetFontMap(Font font,int size)
	{
		string fontAssetPath = UnityEditor.AssetDatabase.GetAssetPath(font);
		string resourcePath=fontAssetPath.Replace("Assets/","");
		teleport.InteropFontAtlas interopFontAtlas;
		FontAtlas fontAtlas;
		FontMap fontMap;
		if ( fontAtlases.TryGetValue(resourcePath,out fontAtlas))
		{
			
		}
		else
		{
			interopFontAtlas=new teleport.InteropFontAtlas();
			fontAtlas=new FontAtlas();
			fontAtlases.Add(resourcePath,fontAtlas);
			if(!teleport.GeometrySource.Server_GetFontAtlas(resourcePath,interopFontAtlas))
				return new FontMap();
			for (int i = 0; i < interopFontAtlas.fontMaps.Length;i++)
			{
				teleport.InteropFontMap interopFontMap=interopFontAtlas.fontMaps[i];
				fontAtlas.fontMaps.Add(interopFontAtlas.fontMaps[i].size,new FontMap());
				fontMap=fontAtlas.fontMaps[interopFontAtlas.fontMaps[i].size];
				for (int j = 0; j < interopFontMap.fontGlyphs.Length; j++)
				{
					fontMap.glyphs.Add((char)(32+j),interopFontMap.fontGlyphs[j]);
				}
			}
		}
		if (fontAtlas.fontMaps.TryGetValue(size, out fontMap))
		{
			return fontMap;
		}
		return new FontMap();
    }

	public void DrawText(Font font,int size,Camera camera, Vector3 position, object message)
	{
		DrawText(font,size,camera, position, message != null ? message.ToString() : "(null)");
	}
	public void DrawText(Font font,int size,Camera camera, Vector3 position, string format, params object[] args)
	{
		DrawText(font,size,camera, position, string.Format(format, args));
	}

	private void DrawText(Font font,int size,Camera camera, Vector3 position, string text)
    {  
        Vector3 screenPoint = camera.WorldToScreenPoint(position);
        Vector3 offset = Vector3.zero;
        for(int c = 0, n = text.Length; c < n; ++c)
        {  
            if ('\n'.Equals(text[c]))
            {
                offset.y += CHAR_TEXTURE_HEIGHT + 2;
                offset.x = 0;
                continue;
            }
            else
			{
				FontMap fontMap=GetFontMap(font,size);
				if(fontMap.glyphs.ContainsKey(text[c]))
				{
					//Gizmos.DrawIcon(camera.ScreenToWorldPoint(screenPoint + offset), fontMap.glyphs[text[c]]);
	                offset.x += CHAR_TEXTURE_WIDTH;
				}
			}
        }
    }
}
