using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class TeleportRenderPipeline : RenderPipeline
{
	bool useDynamicBatching =false;
	bool useGPUInstancing =false;
	public TeleportRenderPipeline(	bool useDynamicBatching=false, bool useGPUInstancing=false, bool useSRPBatcher=false	)
	{
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true;
	}
	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
		foreach (var camera in cameras)
		{
			Render(context, camera);
		}
	}
	TeleportCameraRenderer renderer = new TeleportCameraRenderer();
	void Render(ScriptableRenderContext context, Camera camera)
	{
		renderer.Render(context, camera);
	}
}
