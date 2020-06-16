using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEditor;

namespace teleport
{
	public partial class TeleportCameraRenderer
	{
		partial void StartSample(ScriptableRenderContext context, string name);
		partial void EndSample(ScriptableRenderContext context, string name);

		partial void DrawGizmos(ScriptableRenderContext context, Camera camera);
		partial void PrepareForSceneWindow(ScriptableRenderContext context, Camera camera);

#if UNITY_EDITOR
		partial void StartSample(ScriptableRenderContext context, string name)
		{
			var buffer = new CommandBuffer();
			buffer.name = name;
			Profiler.BeginSample("Editor Only");
			buffer.BeginSample(name);
			context.ExecuteCommandBuffer(buffer);
			buffer.Clear();
			Profiler.EndSample();
			buffer.Release();
		}
		partial void EndSample(ScriptableRenderContext context, string name)
		{
			var buffer = new CommandBuffer();
			buffer.name = name;
			buffer.EndSample(name);
			context.ExecuteCommandBuffer(buffer);
			buffer.Clear();
			buffer.Release();
		}
		partial void PrepareForSceneWindow(ScriptableRenderContext context, Camera camera)
		{
			if (camera.cameraType == CameraType.SceneView)
			{
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			}
		}

		static ShaderTagId[] unsupportedShaderTagIds = {
	new ShaderTagId("")
	};

		static Material errorMaterial;

		partial void DrawGizmos(ScriptableRenderContext context, Camera camera)
		{
			if (Handles.ShouldRenderGizmos())
			{
				context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
				context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
			}
		}
		void DrawUnsupportedShaders(ScriptableRenderContext context, Camera camera)
		{
			if (errorMaterial == null)
			{
				errorMaterial =
					new Material(Shader.Find("Hidden/InternalErrorShader"));
			}
			var drawingSettings = new DrawingSettings(
				unsupportedShaderTagIds[0], new SortingSettings(camera)
			)
			{
				overrideMaterial = errorMaterial
			};
		}

#endif
	}
}