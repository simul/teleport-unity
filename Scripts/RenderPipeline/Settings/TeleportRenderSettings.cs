namespace teleport
{
	[System.Serializable]
	public class TeleportRenderSettings
	{
		[System.Serializable]
		public struct Directional
		{
			public TextureSize atlasSize;
		}

		public enum TextureSize
		{
			_256 = 256, _512 = 512, _1024 = 1024,
			_2048 = 2048, _4096 = 4096, _8192 = 8192
		}
		
		public Directional directional = new Directional
		{
			atlasSize = TextureSize._1024
		};
	}
}
