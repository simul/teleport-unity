using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

public class TeleportRenderPipeline : RenderPipeline
{ 
	bool useDynamicBatching =false;
	bool useGPUInstancing =false;

	public const string CUBEMAP_CAM_PREFIX = "TeleportCubemapCam";

	TeleportCameraRenderer renderer = new TeleportCameraRenderer();

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

	void Render(ScriptableRenderContext context, Camera camera)
	{
		if (camera.name.Contains(CUBEMAP_CAM_PREFIX))
		{
			int index = camera.name.IndexOf(CUBEMAP_CAM_PREFIX, System.StringComparison.Ordinal);
			if (index < 0)
			{
				Debug.LogError("Cubemap camera name does not contain the client id!");
			}
			string clientIDStr = camera.name.Remove(index, CUBEMAP_CAM_PREFIX.Length);
			uid clientID = uid.Parse(clientIDStr);
			renderer.RenderToCubemap(context, camera, clientID);
		}
		else
		{
			renderer.Render(context, camera);
		}
	}
}
