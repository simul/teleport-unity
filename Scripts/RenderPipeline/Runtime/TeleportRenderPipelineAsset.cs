using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	[CreateAssetMenu(menuName = "Rendering/Teleport Render Pipeline Asset")]
	public class TeleportRenderPipelineAsset : RenderPipelineAsset
	{
		protected override RenderPipeline CreatePipeline()
		{
			return new TeleportRenderPipeline(renderSettings);
		}
		[SerializeField]
		TeleportRenderSettings renderSettings = default;
	}
}