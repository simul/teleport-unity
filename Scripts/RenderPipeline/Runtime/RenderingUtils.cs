using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace teleport
{
	public static class RenderingUtils
	{
		static Mesh s_FullscreenMesh = null;
		/// <summary>
		/// Returns a mesh that you can use with <see cref="CommandBuffer.DrawMesh(Mesh, Matrix4x4, Material)"/> to render full-screen effects.
		/// </summary>
		public static Mesh fullscreenMesh
		{
			get
			{
				if (s_FullscreenMesh == null)
				{
					FullScreenMeshStruct fullScreenMeshStruct = new FullScreenMeshStruct();
					fullScreenMeshStruct.horizontal_fov_degrees = 90.0F;
					fullScreenMeshStruct.vertical_fov_degrees = 90.0F;
					fullScreenMeshStruct.far_plane_distance=1.0F;
					
					s_FullscreenMesh = CreateFullscreenMesh(fullScreenMeshStruct);
				}

				return s_FullscreenMesh;
			}
		}
		public struct FullScreenMeshStruct
		{
			public float horizontal_fov_degrees ;
			public float vertical_fov_degrees ;
			public float far_plane_distance ;
			
		}
	
		public static Mesh CreateFullscreenMesh(FullScreenMeshStruct fullScreenMeshStruct)
		{
			float topV = 1.0f;
			float bottomV = 0.0f;
			float horizontal_fov_radians	= Mathf.PI / 180.0F* fullScreenMeshStruct.horizontal_fov_degrees;
			float vertical_fov_radians		= Mathf.PI / 180.0F * fullScreenMeshStruct.vertical_fov_degrees;
			float htan = fullScreenMeshStruct.far_plane_distance *Mathf.Tan(horizontal_fov_radians/2.0F);
			float vtan = fullScreenMeshStruct.far_plane_distance * Mathf.Tan(vertical_fov_radians/2.0F);
			float s = 0.5f;
			float u = 0.5f;
			s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
			s_FullscreenMesh.SetVertices(new List<Vector3>
			{
				new Vector3(u-s, u-s, 0.0f),
				new Vector3(u-s, u+s, 0.0f),
				new Vector3(u+s, u-s, 0.0f),
				new Vector3(u+s, u+s, 0.0f)
			});

			s_FullscreenMesh.SetUVs(0, new List<Vector2>
			{
				new Vector2(0.0f, bottomV),
				new Vector2(0.0f, topV),
				new Vector2(1.0f, bottomV),
				new Vector2(1.0f, topV)
			});
			s_FullscreenMesh.SetUVs(1, new List<Vector3>
			{
				new Vector3(-htan, -vtan, fullScreenMeshStruct.far_plane_distance),
				new Vector3(-htan,  vtan, fullScreenMeshStruct.far_plane_distance),
				new Vector3(htan, -vtan, fullScreenMeshStruct.far_plane_distance),
				new Vector3(htan,  vtan, fullScreenMeshStruct.far_plane_distance)
			});
			s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
			s_FullscreenMesh.UploadMeshData(true);
			return s_FullscreenMesh;
			
		}

	}
}
