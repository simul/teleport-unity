﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using uid = System.UInt64;

namespace avs
{
	public class TransformAnimation
	{
		public IntPtr name;
		public IntPtr path;
		public Int64 numBones;
		public TransformKeyframeList[] boneKeyframes;

		public float duration;          /// How long the animation lasts, must be greater than all keyframe time values.


	}

	public struct TransformKeyframeList
	{
		public Int16 boneIndex;

		public int positionAmount;
		public Vector3Keyframe[] positionKeyframes;

		public int numRotationKeyframes;
		public Vector4Keyframe[] rotationKeyframes;
	}

	public struct FloatKeyframe
	{
		public float time;
		public float value;
	}

	public struct Vector3Keyframe
	{
		public float time;
		public avs.Vector3 value;
	}

	public struct Vector4Keyframe
	{
		public float time;
		public avs.Vector4 value;
	}
}

#if UNITY_EDITOR
namespace teleport
{
	public static class AnimationExtractor
	{
		struct InterimAnimation
		{
			public AnimationCurve positionX;
			public AnimationCurve positionY;
			public AnimationCurve positionZ;

			public AnimationCurve rotationX;
			public AnimationCurve rotationY;
			public AnimationCurve rotationZ;
			public AnimationCurve rotationW;

			public AnimationCurve eulerRotationX;
			public AnimationCurve eulerRotationY;
			public AnimationCurve eulerRotationZ;
		}

		#region DLLImports

		[DllImport(TeleportServerDll.name)]
		private static extern void Server_StoreTransformAnimation(uid id,string path, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(TransformAnimationMarshaler))] avs.TransformAnimation animation);
		#endregion

		public static uid[] AddAnimations(Animator animator, GeometrySource.ForceExtractionMask forceMask)
		{
			uid[] animationIDs = new uid[0];
			if(!animator)
			{
				return animationIDs;
			}

			if(animator.isHuman)
			{
				animationIDs = ExtractHumanAnimationData(animator, forceMask);
			}
			else
			{
				animationIDs = ExtractNonHumanAnimationData(animator, forceMask);
			}

			return animationIDs;
		}

		public static uid[] ExtractHumanAnimationData(Animator animator, GeometrySource.ForceExtractionMask forceMask)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = animator.gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
			GameObject rootObject = GeometrySource.GetTopmostSkeletonRoot(skinnedMeshRenderer).gameObject;
			List<UnityEngine.Transform> bones=new List<UnityEngine.Transform> ();
			GeometrySource.GetBones(rootObject.transform, bones);
			//Store transform components, so they can be reset at the end.
			Transform[] transforms = animator.gameObject.GetComponentsInChildren<Transform>();
			Vector3[] storedPositions = new Vector3[transforms.Length];
			Quaternion[] storedRotations = new Quaternion[transforms.Length];
			for(int i = 0; i < transforms.Length; i++)
			{
				storedPositions[i] = transforms[i].localPosition;
				storedRotations[i] = transforms[i].localRotation;
			}

			HumanBone[] humanBones = animator.avatar.humanDescription.human;
			Dictionary<string, GameObject> humanToBone = new Dictionary<string, GameObject>();
			foreach(HumanBone bone in humanBones)
			{
				Transform boneTransform = FindChildTransformWithName(animator.gameObject.transform, bone.boneName);
				humanToBone[bone.humanName] = boneTransform.gameObject;
				//Debug.Log($"{bone.humanName} | {boneTransform.gameObject.name}");
			}

			GeometrySource geometrySource = GeometrySource.GetGeometrySource();

			if(animator.runtimeAnimatorController)
			{ 
				AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
				uid[] animationIDs = new uid[animationClips.Length];
				for(int i = 0; i < animationClips.Length; i++)
				{
					AnimationClip clip = animationClips[i];
					uid animationID = geometrySource.FindResourceID(clip);
					string path;
					if (!GeometrySource.GetResourcePath(clip, out path, true))
					{
						continue;
					}
					if (animationID != 0 && (forceMask & GeometrySource.ForceExtractionMask.FORCE_SUBRESOURCES) == GeometrySource.ForceExtractionMask.FORCE_NOTHING)
					{
						animationIDs[i] = animationID;
						continue;
					}
					//Generate an ID, if we don't have one.
					if (animationID == 0)
					{
						animationID = GeometrySource.Server_GetOrGenerateUid(path);
					}

					EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
					if(curveBindings.Length == 0)
					{
						continue;
					}

					float length_seconds=clip.length;
					Dictionary<string, InterimAnimation> nodeCurves = new Dictionary<string, InterimAnimation>();
					Dictionary <string, HumanLimit> limits= new Dictionary<string, HumanLimit>();
					foreach(EditorCurveBinding binding in curveBindings)
					{
						AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
						var words= binding.propertyName.Split(' ');
						//Attempt to extract transform animation data.
						int index = binding.propertyName.IndexOf("T.");
						if(index != -1)
						{
							string bindingName = binding.propertyName.Substring(0, index);
							nodeCurves.TryGetValue(bindingName, out InterimAnimation keyframe);

							if (keyframe.positionX == null || curve.keys.Length > keyframe.positionX.length)
								keyframe.positionX = curve;

							nodeCurves[bindingName] = keyframe;
							continue;
						}

						//Attempt to extract rotation animation data.
						index = binding.propertyName.IndexOf("Q.");
						if(index != -1)
						{
							string bindingName = binding.propertyName.Substring(0, index);
							nodeCurves.TryGetValue(bindingName, out InterimAnimation keyframe);

							if(keyframe.positionX == null||curve.keys.Length>keyframe.positionX.length)
								keyframe.positionX = curve;

							nodeCurves[bindingName] = keyframe;
							continue;
						}

						//Unity seems to have different names in the mechanim system and the human trait class for fingers.
						string muscleName = binding.propertyName;
						muscleName = muscleName.Replace('.', ' ');
						muscleName = muscleName.Replace("LeftHand", "Left");
						muscleName = muscleName.Replace("RightHand", "Right");

						//Rest of the possible properties use muscles, which need to be converted to bone names.
						int muscleIndex = System.Array.IndexOf(HumanTrait.MuscleName, muscleName);
						int boneIndex= HumanTrait.BoneFromMuscle(muscleIndex);
						if(boneIndex<0|| boneIndex>=HumanTrait.BoneName.Length)
						{
							continue;
						}
						string humanBoneName = HumanTrait.BoneName[boneIndex];

						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.positionX = curve;
						nodeCurves[humanBoneName] = nodeCurve;
					}

					avs.TransformAnimation newAnimation = new avs.TransformAnimation();
					newAnimation.duration=clip.length;
					newAnimation.numBones = nodeCurves.Count;
					newAnimation.boneKeyframes = new avs.TransformKeyframeList[newAnimation.numBones];
					int max_k=0;
					float max_t=0.0F;
					foreach (string humanName in nodeCurves.Keys)
					{
						InterimAnimation curves = nodeCurves[humanName];
						max_k=Math.Max(max_k,curves.positionX.length);
						max_t=Math.Max(max_t, curves.positionX.keys.Last().time);
					}
					if (!humanToBone.TryGetValue("Root", out GameObject gObject))
					{
						humanToBone["Root"]=rootObject;
					}
					int j = 0;
					foreach(string humanName in nodeCurves.Keys)
					{
						InterimAnimation curves = nodeCurves[humanName];
						GameObject boneObject;
						if(!humanToBone.TryGetValue(humanName, out boneObject))
						{
							//Debug.LogWarning($"Couldn't find bone gameobject with bone name of: {humanName}");
							continue;
						}

						if(!geometrySource.HasResource(boneObject))
						{
							Debug.LogWarning($"Couldn't find  {boneObject.name}");
							continue;
						}
						newAnimation.boneKeyframes[j].boneIndex = (Int16)Array.FindIndex(bones.ToArray(), x => x.transform == boneObject.transform);
						if ((int)newAnimation.boneKeyframes[j].boneIndex >= bones.Count)
						{
							Debug.LogWarning($"Couldn't find bone index for: {boneObject.name}");
							continue;
						}
						int num_k= curves.positionX.keys.Length;
						float end_t =  curves.positionX.keys.Last().time;
						newAnimation.boneKeyframes[j].positionAmount=num_k;
						newAnimation.boneKeyframes[j].numRotationKeyframes = num_k;
						newAnimation.boneKeyframes[j].positionKeyframes=new avs.Vector3Keyframe[num_k];
						newAnimation.boneKeyframes[j].rotationKeyframes = new avs.Vector4Keyframe[num_k];
						for (int k=0;k< num_k; k++)
						{
							float t = 0.0F;
							if(num_k>1)
								t=(float)k/(float)(num_k-1)* end_t;

							// for each time value:
							//You can't use SampleAnimation for sampling a sub-object of an animation. You have to sample the whole object (the parent object).
							clip.SampleAnimation(animator.gameObject, t);
							Vector3 pos= boneObject.transform.localPosition;
							Quaternion rotation = boneObject.transform.localRotation;
							// But if it's the root object, its Local transform may be relative to an intermediate parent that's
							// not streamed. So we want the transform relative to the animator gameObject.
							if(boneObject==rootObject)
							{
								pos=animator.transform.InverseTransformPoint(boneObject.transform.position);
								rotation = Quaternion.Inverse(animator.transform.rotation) * boneObject.transform.rotation;
								//rotation = Quaternion.FromToRotation(animator.transform.forward, boneObject.transform.forward);
							}
							if(float.IsNaN(t)|| float.IsNaN(pos.x)|| float.IsNaN(pos.y)| float.IsNaN(pos.z))
							{
								Debug.LogError("NaN in position keyframe");
								return animationIDs;
							}
							newAnimation.boneKeyframes[j].positionKeyframes[k].time=t;
							newAnimation.boneKeyframes[j].positionKeyframes[k].value.x = pos.x;
							newAnimation.boneKeyframes[j].positionKeyframes[k].value.y = pos.y;
							newAnimation.boneKeyframes[j].positionKeyframes[k].value.z = pos.z;
							if (float.IsNaN(rotation.w) || float.IsNaN(rotation.x) || float.IsNaN(rotation.y) | float.IsNaN(rotation.z))
							{
								Debug.LogError("NaN in rotation keyframe");
								return animationIDs;
							}
							newAnimation.boneKeyframes[j].rotationKeyframes[k].time = t;
							{ 
								newAnimation.boneKeyframes[j].rotationKeyframes[k].value.x =rotation.x;
								newAnimation.boneKeyframes[j].rotationKeyframes[k].value.y =rotation.y;
								newAnimation.boneKeyframes[j].rotationKeyframes[k].value.z =rotation.z;
								newAnimation.boneKeyframes[j].rotationKeyframes[k].value.w = rotation.w;
							}
						}
						++j;
					}

					//Create a new TransformAnimation where we ignore the unfilled values of the interim one.
					avs.TransformAnimation animation = new avs.TransformAnimation();
					animation.duration=clip.length;
					animation.name = Marshal.StringToCoTaskMemUTF8(clip.name);

					animation.numBones = j;
					animation.boneKeyframes = new avs.TransformKeyframeList[animation.numBones];
					for(int k = 0; k < j; k++)
					{
						animation.boneKeyframes[k] = newAnimation.boneKeyframes[k];
					}

					animationIDs[i] = animationID;

					//Add resource to the GeometrySource, so we know if it has been added before.
					geometrySource.AddResource(clip, animationID);
					//Store animation on unmanaged side.
					Server_StoreTransformAnimation(animationID, path,animation);
				}
				//Reset the animator's GameObject's transform.
				//Won't this just zero the values? Won't this be incorrect most of the time? Is this really necessary with the next block?
				AnimationClip emptyClip = new AnimationClip();
				emptyClip.SampleAnimation(animator.gameObject, 0.0f);

				//Reset transforms to their default stored values.
				for (int i = 0; i < transforms.Length; i++)
				{	
					transforms[i].localRotation= storedRotations[i];
					transforms[i].localPosition= storedPositions[i];
				}

				return animationIDs;
			}
			else
			{
				uid[] animationIDs = new uid[0];
				return animationIDs;
			}
		}

		public static uid[] ExtractNonHumanAnimationData(Animator animator, GeometrySource.ForceExtractionMask forceMask)
		{
			if(animator == null)
			{
				Debug.LogWarning($"Failed to extract non-human animations! Passed animator was null!");
				return new uid[0];
			}

			if(animator.runtimeAnimatorController == null)
			{
				Debug.LogWarning($"Failed to extract non-human animations from {animator.gameObject.name}! RuntimeAnimationController was null!");
				return new uid[0];
			}

			uid[] animationIDs = new uid[animator.runtimeAnimatorController.animationClips.Length];

			SkinnedMeshRenderer skinnedMeshRenderer = animator.gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
			if (!skinnedMeshRenderer)
			{
				Debug.LogWarning($"No skinned mesh renderer found for game object tree: {animator.gameObject.name}! All transforms will be used as bones, regardless whether they are a bone.");
			}

			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			for(int i = 0; i < animator.runtimeAnimatorController.animationClips.Length; i++)
			{
				AnimationClip clip = animator.runtimeAnimatorController.animationClips[i];

				string path;
				if (!GeometrySource.GetResourcePath(clip, out path, true))
				{
					continue;
				}
				uid animationID = geometrySource.FindResourceID(clip);
				if(animationID != 0 && (forceMask & GeometrySource.ForceExtractionMask.FORCE_SUBRESOURCES) == GeometrySource.ForceExtractionMask.FORCE_NOTHING)
				{
					animationIDs[i] = animationID;
					continue;
				}
				//Generate an ID, if we don't have one.
				if (animationID == 0)
				{
					animationID = GeometrySource.Server_GetOrGenerateUid(path);
				}

				avs.TransformAnimation animation = new avs.TransformAnimation();
				animation.name = Marshal.StringToCoTaskMemUTF8(clip.name);

				Dictionary<Transform, InterimAnimation> nodeCurves = new Dictionary<Transform, InterimAnimation>();
				var curveBindings= AnimationUtility.GetCurveBindings(clip);
				foreach (EditorCurveBinding curveBinding in curveBindings)
				{
					AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, curveBinding);

					Transform bone = animator.gameObject.transform.Find(curveBinding.path);
					//Don't create a curve if the bones doesn't actually exist.
					if(!bone)
					{
						continue;
					}

					//Don't create the curve if it is not a bone.
					//WARNING: Animations on non-bones are not handled!
					if(skinnedMeshRenderer && !skinnedMeshRenderer.bones.Contains(bone))
					{
						continue;
					}

					nodeCurves.TryGetValue(bone, out InterimAnimation interimAnimation);
					switch(curveBinding.propertyName)
					{
						case "m_LocalPosition.x":
							interimAnimation.positionX = curve;
							break;
						case "m_LocalPosition.y":
							interimAnimation.positionY = curve;
							break;
						case "m_LocalPosition.z":
							interimAnimation.positionZ = curve;
							break;
						case "m_LocalRotation.x":
							interimAnimation.rotationX = curve;
							break;
						case "m_LocalRotation.y":
							interimAnimation.rotationY = curve;
							break;
						case "m_LocalRotation.z":
							interimAnimation.rotationZ = curve;
							break;
						case "m_LocalRotation.w":
							interimAnimation.rotationW = curve;
							break;
						case "localEulerAnglesRaw.x":
							interimAnimation.eulerRotationX = curve;
							break;
						case "localEulerAnglesRaw.y":
							interimAnimation.eulerRotationY = curve;
							break;
						case "localEulerAnglesRaw.z":
							interimAnimation.eulerRotationZ = curve;
							break;
						case "m_LocalScale.x":
							break;
						case "m_LocalScale.y":
							break;
						case "m_LocalScale.z":
							break;
						default:
							Debug.Log("Unfound: " + curveBinding.propertyName);
							break;
					}
					nodeCurves[bone] = interimAnimation;
				}

				animation.boneKeyframes = new avs.TransformKeyframeList[nodeCurves.Count];
				for(int j = 0; j < nodeCurves.Count; j++)
				{
					Transform bone = nodeCurves.Keys.ToArray()[j];
					if(!geometrySource.HasResource(bone.gameObject))
					{
						Debug.LogWarning($"Bone \"{bone.name}\" has animation properties, but could not be found in the geometry source!");
						continue;
					}

					InterimAnimation interim = nodeCurves[bone];
					avs.TransformKeyframeList transformKeyframe = new avs.TransformKeyframeList();
					transformKeyframe.boneIndex = (Int16)Array.FindIndex(skinnedMeshRenderer.bones, x => x.transform == bone);

					if(interim.positionX != null)
					{
						transformKeyframe.positionKeyframes = new avs.Vector3Keyframe[interim.positionX.length];
						transformKeyframe.positionAmount = interim.positionX.length;
						for(int k = 0; k < interim.positionX.length; k++)
						{
							transformKeyframe.positionKeyframes[k].time = interim.positionX[k].time;
							transformKeyframe.positionKeyframes[k].value = new avs.Vector3(interim.positionX[k].value, interim.positionY[k].value, interim.positionZ[k].value);
						}
					}
					else if(interim.positionY != null || interim.positionZ != null)
					{
						Debug.LogWarning("Position animation without X-property, but other properties, caused the animation data to be ignored!");
					}

					if(interim.rotationX != null)
					{
						transformKeyframe.rotationKeyframes = new avs.Vector4Keyframe[interim.rotationX.length];
						transformKeyframe.numRotationKeyframes = interim.rotationX.length;
						for(int k = 0; k < interim.rotationX.length; k++)
						{
							transformKeyframe.rotationKeyframes[k].time = interim.rotationX[k].time; 
							transformKeyframe.rotationKeyframes[k].value = new avs.Vector4(interim.rotationX[k].value, interim.rotationY[k].value, interim.rotationZ[k].value, interim.rotationW[k].value);
						}
					}
					else if(interim.eulerRotationX != null|| interim.eulerRotationY!=null|| interim.eulerRotationZ!=null)
					{
						// create a set of all the times used:
						HashSet<float> kf_times=new HashSet<float>();
						if (interim.eulerRotationX != null)
						{
							foreach (var kf in interim.eulerRotationX.keys)
							{
								kf_times.Add(kf.time);
							}
						}
						if (interim.eulerRotationY != null)
						{
							foreach (var kf in interim.eulerRotationY.keys)
							{
								kf_times.Add(kf.time);
							}
						}
						if (interim.eulerRotationZ != null)
						{
							foreach (var kf in interim.eulerRotationZ.keys)
							{
								kf_times.Add(kf.time);
							}
						}
						transformKeyframe.numRotationKeyframes = kf_times.Count;
						transformKeyframe.rotationKeyframes = new avs.Vector4Keyframe[transformKeyframe.numRotationKeyframes];
						int k = 0;
						foreach (float t in kf_times)
						{
							transformKeyframe.rotationKeyframes[k].time = t; 
							float x= interim.eulerRotationX != null ? interim.eulerRotationX.Evaluate(t):0.0F;
							float y = interim.eulerRotationY != null ? interim.eulerRotationY.Evaluate(t) : 0.0F;
							float z = interim.eulerRotationZ != null ? interim.eulerRotationZ.Evaluate(t) : 0.0F;
							transformKeyframe.rotationKeyframes[k].value = Quaternion.Euler(x,y,z); //Convert from euler to quaternion to avs.Vector4.
							k++;
						}
					}
					else if(interim.rotationY != null || interim.rotationZ != null)
					{
						Debug.LogWarning("Quaternion rotation animation without X-property, but other properties, caused the animation data to be ignored!");
					}
					else if(interim.eulerRotationY != null || interim.eulerRotationZ != null)
					{
						Debug.LogWarning("Euler rotation animation without X-property, but other properties, caused the animation data to be ignored!");
					}

					animation.boneKeyframes[j] = transformKeyframe;
				}
				animation.numBones = animation.boneKeyframes.Length;

				animationIDs[i] = animationID;

				//Add resource to the GeometrySource, so we know if it has been added before.
				geometrySource.AddResource(clip, animationID);
				//Store animation on unmanaged side.
				Server_StoreTransformAnimation(animationID, path, animation);
			}

			return animationIDs;
		}

		private static Transform FindChildTransformWithName(Transform startTransform, string name)
		{
			Transform boneTransform = null;
			Queue<Transform> unexploredBones = new Queue<Transform>();
			unexploredBones.Enqueue(startTransform);

			while(boneTransform == null && unexploredBones.Count != 0)
			{
				Transform current = unexploredBones.Dequeue();
				boneTransform = current.Find(name);

				if(boneTransform == null)
				{
					for(int i = 0; i < current.childCount; i++)
					{
						unexploredBones.Enqueue(current.GetChild(i));
					}
				}
			}

			return boneTransform;
		}

		private static avs.FloatKeyframe[] GetKeyframeData(AnimationCurve curve)
		{
			avs.FloatKeyframe[] keyframes = new avs.FloatKeyframe[curve.keys.Length];

			int i = 0;
			foreach(Keyframe unityKeyframe in curve.keys)
			{
				keyframes[i] = new avs.FloatKeyframe
				{
					time = unityKeyframe.time,
					value = unityKeyframe.value
				};

				++i;
			}

			return keyframes;
		}
	}
}
#endif
