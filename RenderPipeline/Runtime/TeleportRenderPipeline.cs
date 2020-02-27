using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class TeleportRenderPipeline : RenderPipeline
{ 
	bool useDynamicBatching =false;
	bool useGPUInstancing =false;

	public const string CUBEMAP_CAM_PREFIX = "TeleportCubemapCam"; 

	// Map to deal with counter for each client
	private Dictionary<string, int> cubemapCamIDCounters;

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
			bool isCubemapCam = camera.name.Contains(CUBEMAP_CAM_PREFIX);

			if (isCubemapCam)
			{
				bool isLastCubeFace = false;
				if (cubemapCamIDCounters.ContainsKey(camera.name))
				{
					if (cubemapCamIDCounters[camera.name] == 5)
					{
						cubemapCamIDCounters[camera.name] = 0;
						isLastCubeFace = true;
					}
					else
					{
						cubemapCamIDCounters[camera.name]++;
					}
				}
				else
				{
					cubemapCamIDCounters.Add(camera.name, 1);
				}
				RenderToCubemap(context, camera, isLastCubeFace);
			}
			else
			{
				Render(context, camera);
			}
		}
	}

	TeleportCameraRenderer renderer = new TeleportCameraRenderer();

	void Render(ScriptableRenderContext context, Camera camera)
	{
		renderer.Render(context, camera);
	}

	void RenderToCubemap(ScriptableRenderContext context, Camera camera, bool isLastCubeFace = false)
	{
		renderer.RenderToCubemap(context, camera, isLastCubeFace);
	}
}
