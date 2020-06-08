using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	public static class ShadowUtils
	{
		public static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
		{
			// Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
			// apply z reversal to projection matrix. We need to do it manually here.
			if (SystemInfo.usesReversedZBuffer)
			{
				proj.m20 = -proj.m20;
				proj.m21 = -proj.m21;
				proj.m22 = -proj.m22;
				proj.m23 = -proj.m23;
			}

			Matrix4x4 worldToShadow = proj * view;

			var textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m00 = 0.5f;
			textureScaleAndBias.m11 = 0.5f;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m03 = 0.5f;
			textureScaleAndBias.m23 = 0.5f;
			textureScaleAndBias.m13 = 0.5f;

			// Apply texture scale and offset to save a MAD in shader.
			return textureScaleAndBias * worldToShadow;
		}
		public static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view, int cascadeIndex, int atlasWidth, int atlasHeight, int shadowResolution)
		{
			Matrix4x4 shadowMatrix = GetShadowTransform(proj, view);
			ApplySliceTransform(ref shadowMatrix, cascadeIndex, atlasWidth, atlasHeight, shadowResolution);
			return shadowMatrix;
		}
		public static void GetShadowTransforms(ref Matrix4x4[] WorldToShadow, VisibleLight light, int atlasSize, int numCascades)
		{
			for (int i = 0; i < 4; i++)
			{
				WorldToShadow[i]=CalcShadowMatrix( light);
				ApplySliceTransform(ref WorldToShadow[i], i, atlasSize, atlasSize, atlasSize / 2);
			}
		}
		public static void ApplySliceTransform(ref Matrix4x4 shadowTransform, int cascadeIndex, int atlasSize, int split,int shadowResolution)
		{
			int offsetX = (cascadeIndex % split) * shadowResolution;
			int offsetY = (cascadeIndex / split) * shadowResolution;
			Matrix4x4 sliceTransform = Matrix4x4.identity;
			float oneOverAtlasSize = 1.0f / atlasSize;
			sliceTransform.m00 = shadowResolution * oneOverAtlasSize;
			sliceTransform.m11 = shadowResolution * oneOverAtlasSize;
			sliceTransform.m03 = offsetX * oneOverAtlasSize;
			sliceTransform.m13 = offsetY * oneOverAtlasSize;

			// Apply shadow slice scale and offset
			shadowTransform = sliceTransform * shadowTransform;
		}

		public static Vector4 GetShadowBias( ref VisibleLight shadowLight, int shadowLightIndex, Vector4 shadowBias,bool supportsSoftShadows,Matrix4x4 lightProjectionMatrix, float shadowResolution)
		{
			if (shadowLightIndex < 0 )
			{
				Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
				return Vector4.zero;
			}
			Vector4 outputBias = new Vector4(0, 0, 0, 0);
			float frustumSize;
			float clipSize;
			if (shadowLight.lightType == LightType.Directional)
			{
				// Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere
				// We use 1.0f/ instead of 2.0f as in Unity's URP.
				frustumSize = 1.0f / lightProjectionMatrix.m00;
				clipSize = lightProjectionMatrix.m22/2.0f;
			}
			else if (shadowLight.lightType == LightType.Spot)
			{
				// For perspective projections, shadow texel size varies with depth
				// It will only work well if done in receiver side in the pixel shader. Currently UniversalRP
				// do bias on caster side in vertex shader. When we add shader quality tiers we can properly
				// handle this. For now, as a poor approximation we do a constant bias and compute the size of
				// the frustum as if it was orthogonal considering the size at mid point between near and far planes.
				// Depending on how big the light range is, it will be good enough with some tweaks in bias
				frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range;
				clipSize = lightProjectionMatrix.m22 / 2.0f;
			}
			else
			{
				Debug.LogWarning("Only spot and directional shadow casters are supported in universal pipeline");
				frustumSize = 0.0f;
				clipSize = 0.0f;
			}

			// depth and normal bias scale is in shadowmap texel size in world space
			float texelSize = frustumSize / shadowResolution;
			// NOTE the following - we're attempting to repro Unity's default behaviour with our "clipSize" variable...
			float depthBias = shadowBias.x * clipSize;
			float normalBias = shadowBias.y * texelSize;

			if (supportsSoftShadows)
			{
				// TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
				// This is not true with PCF. Ideally we need to do either
				// cone base bias (based on distance to center sample)
				// or receiver place bias based on derivatives.
				// For now we scale it by the PCF kernel size (5x5)
				const float kernelRadius = 2.5f;
				depthBias *= kernelRadius;
				normalBias *= kernelRadius;
			}
			outputBias.x = depthBias;
			outputBias.y = 1.0F;
			outputBias.z = normalBias;
			outputBias.w = 0.0F;
			return outputBias;
		}
		public static Matrix4x4 CalcShadowMatrix( VisibleLight light)
		{
			Vector4 lightPos = light.localToWorldMatrix.GetColumn(3);
			lightPos.w = 1.0F;
			Vector4 lightDir = -light.localToWorldMatrix.GetColumn(2);
			lightDir.w = 0.0F;
			Matrix4x4 worldToShadow = new Matrix4x4();
			worldToShadow = light.localToWorldMatrix.inverse;
			if (light.lightType == LightType.Spot)
			{
				Matrix4x4 lightToWorld = light.localToWorldMatrix;
				Vector3 matScale = new Vector3(light.range, light.range, light.range);
				lightToWorld *= Matrix4x4.Scale(matScale);
				Matrix4x4 worldToLight = lightToWorld.inverse;
				Vector4 spotDir = worldToLight.GetRow(2);
				float tanVal = (float)(System.Math.Tan(3.1415926536F / 180.0F * light.spotAngle / 2.0F));
				spotDir *= 2.0F * tanVal;
				Vector4 spotRow = new Vector4(spotDir.x, spotDir.y, spotDir.z, spotDir.w);
				worldToLight.SetRow(3, spotRow);

				spotRow *= 2.0F;
				worldToLight.SetRow(3, spotRow);
				worldToShadow = worldToLight;
			}
			return worldToShadow;
		}
	}
}
