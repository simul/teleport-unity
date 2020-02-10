using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Teleport Render Pipeline Asset")]
public class TeleportRenderPipelineAsset : RenderPipelineAsset
{
	protected override RenderPipeline CreatePipeline()
	{
		return new TeleportRenderPipeline();
	}
}
