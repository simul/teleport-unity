using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

using uid = System.UInt64;

namespace avs
{
	public class PropertyAnimation
	{
		public Int64 boneAmount;
		public PropertyKeyframe[] boneKeyframes;
	}

	public struct PropertyKeyframe
	{
		public uid nodeID;

		public int positionXAmount;
		public int positionYAmount;
		public int positionZAmount;
		public FloatKeyframe[] positionXKeyframes;
		public FloatKeyframe[] positionYKeyframes;
		public FloatKeyframe[] positionZKeyframes;

		public int rotationXAmount;
		public int rotationYAmount;
		public int rotationZAmount;
		public int rotationWAmount;
		public FloatKeyframe[] rotationXKeyframes;
		public FloatKeyframe[] rotationYKeyframes;
		public FloatKeyframe[] rotationZKeyframes;
		public FloatKeyframe[] rotationWKeyframes;
	}

	public class TransformAnimation
	{
		public IntPtr name;

		public Int64 boneAmount;
		public TransformKeyframe[] boneKeyframes;
	}

	public struct TransformKeyframe
	{
		public uid nodeID;

		public int positionAmount;
		public Vector3Keyframe[] positionKeyframes;

		public int rotationAmount;
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
		[DllImport("SimulCasterServer")]
		private static extern uid GenerateID();

		[DllImport("SimulCasterServer")]
		private static extern void StorePropertyAnimation(uid id, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropertyAnimationMarshaler))] avs.PropertyAnimation animation);
		[DllImport("SimulCasterServer")]
		private static extern void StoreTransformAnimation(uid id, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(TransformAnimationMarshaler))] avs.TransformAnimation animation);
		#endregion

		public static uid[] AddAnimations(Animator animator)
		{
			uid[] animationIDs = new uid[0];
			if(!animator)
				return animationIDs;

			if(animator.isHuman)
			{
				avs.TransformAnimation[] animations = ExtractHumanAnimationData(animator);

				animationIDs = new uid[animations.Length];
				for(int i = 0; i < animations.Length; i++)
				{
					uid id = GenerateID();
					animationIDs[i] = id;
					StoreTransformAnimation(id, animations[i]);
				}
			}
			else
			{
				avs.TransformAnimation[] animations = ExtractNonHumanAnimationData(animator);

				animationIDs = new uid[animations.Length];
				for(int i = 0; i < animations.Length; i++)
				{
					uid id = GenerateID();
					animationIDs[i] = id;
					StoreTransformAnimation(id, animations[i]);
				}
			}

			return animationIDs;
		}

		public static avs.TransformAnimation[] ExtractHumanAnimationData(Animator animator)
		{
			HumanBone[] humanBones = animator.avatar.humanDescription.human;
			Dictionary<string, GameObject> humanToBone = new Dictionary<string, GameObject>();

			foreach(HumanBone bone in humanBones)
			{
				Transform boneTransform = FindChildTransformWithName(animator.transform, bone.boneName);
				humanToBone[bone.humanName] = boneTransform.gameObject;
				//Debug.Log($"{bone.humanName} | {boneTransform.gameObject.name}");
			}

			AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
			avs.TransformAnimation[] animations = new avs.TransformAnimation[animationClips.Length];
			for(int i = 0; i < animationClips.Length; i++)
			{
				AnimationClip clip = animationClips[i];
				EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);

				Dictionary<string, InterimAnimation> nodeCurves = new Dictionary<string, InterimAnimation>();
				foreach(EditorCurveBinding binding in curveBindings)
				{
					AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

					//Attempt to extract transform animation data.
					int index = binding.propertyName.IndexOf("T.");
					if(index != -1)
					{
						string bindingName = binding.propertyName.Substring(0, index);
						nodeCurves.TryGetValue(bindingName, out InterimAnimation keyframe);

						if(binding.propertyName[index + 2] == 'x')
							keyframe.positionX = curve;
						else if(binding.propertyName[index + 2] == 'y')
							keyframe.positionY = curve;
						else if (binding.propertyName[index + 2] == 'z')
							keyframe.positionZ = curve;
						else
							Debug.LogWarning("Unknown binding property "+ binding.propertyName.Substring(index+2));

						nodeCurves[bindingName] = keyframe;
						continue;
					}

					//Attempt to extract rotation animation data.
					index = binding.propertyName.IndexOf("Q.");
					if(index != -1)
					{
						string bindingName = binding.propertyName.Substring(0, index);
						nodeCurves.TryGetValue(bindingName, out InterimAnimation keyframe);

						if(binding.propertyName[index + 2] == 'x')
							keyframe.rotationX = curve;
						else if(binding.propertyName[index + 2] == 'y')
							keyframe.rotationY = curve;
						else if(binding.propertyName[index + 2] == 'z')
							keyframe.rotationZ = curve;
						else
							keyframe.rotationW = curve;

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
					string humanBoneName = HumanTrait.BoneName[HumanTrait.BoneFromMuscle(muscleIndex)];

					index = binding.propertyName.IndexOf("Front-Back");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationZ = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Left-Right Twist");
					if(index != -1)
					{
						Debug.LogError("Left-Right Twist");
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationX = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Left-Right");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationY = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Up-Down");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationZ = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Down-Up");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationZ = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Twist In-Out");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationX = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("In-Out");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationY = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Stretch");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationZ = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Spread");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationY = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					index = binding.propertyName.IndexOf("Close");
					if(index != -1)
					{
						nodeCurves.TryGetValue(humanBoneName, out InterimAnimation nodeCurve);
						nodeCurve.rotationZ = curve;
						nodeCurves[humanBoneName] = nodeCurve;

						continue;
					}

					Debug.LogWarning($"Failed to extract animation curve \"{binding.propertyName}\" with {curve.keys.Length} keys.");
				}

				avs.TransformAnimation newAnimation = new avs.TransformAnimation();
				newAnimation.boneAmount = nodeCurves.Count;
				newAnimation.boneKeyframes = new avs.TransformKeyframe[newAnimation.boneAmount];

				int j = 0;
				foreach(string humanName in nodeCurves.Keys)
				{
					InterimAnimation curves = nodeCurves[humanName];

					GameObject boneObject;
					if(humanName == "Root")
						boneObject = animator.gameObject;
					else
					{
						if(!humanToBone.TryGetValue(humanName, out boneObject))
						{
							Debug.LogWarning($"Couldn't find bone gameobject with bone name of: {humanName}");
							continue;
						}
					}

					newAnimation.boneKeyframes[j].nodeID = GeometrySource.GetGeometrySource().FindResourceID(boneObject.transform);
					if(newAnimation.boneKeyframes[j].nodeID==0)
					{
						continue;
					} 
					int num_k=0;
					float max_t = 0.0F;
					if (curves.positionX!=null)
					{
						num_k= curves.positionX.keys.Length;
						max_t = Math.Max(max_t, curves.positionX.keys.Last().time);
					}
					if (curves.positionY != null)
					{
						num_k = Math.Max(num_k, curves.positionY.keys.Length);
						max_t = Math.Max(max_t,curves.positionY.keys.Last().time);
					}
					if (curves.positionZ != null)
					{ 
						num_k = Math.Max(num_k, curves.positionZ.keys.Length);
						max_t = Math.Max(max_t, curves.positionZ.keys.Last().time);
					}
					if (curves.rotationW != null)
					{	
						num_k = Math.Max(num_k, curves.rotationW.keys.Length);
						max_t = Math.Max(max_t, curves.rotationW.keys.Last().time);
					}
					if (curves.rotationX != null)
					{
						num_k = Math.Max(num_k, curves.rotationX.keys.Length);
						max_t = Math.Max(max_t, curves.rotationX.keys.Last().time);
					}
					if (curves.rotationY != null)
					{
						num_k = Math.Max(num_k, curves.rotationY.keys.Length);
						max_t = Math.Max(max_t, curves.rotationY.keys.Last().time);
					}
					if (curves.rotationZ != null)
					{
						num_k = Math.Max(num_k, curves.rotationZ.keys.Length);
						max_t = Math.Max(max_t, curves.rotationZ.keys.Last().time);
					}
					newAnimation.boneKeyframes[j].positionAmount=num_k;
					newAnimation.boneKeyframes[j].rotationAmount = num_k;
					newAnimation.boneKeyframes[j].positionKeyframes=new avs.Vector3Keyframe[num_k];
					newAnimation.boneKeyframes[j].rotationKeyframes = new avs.Vector4Keyframe[num_k];
					for (int k=0;k<num_k;k++)
					{
						float t=(float)k/(float)num_k*max_t;
						newAnimation.boneKeyframes[j].positionKeyframes[k].time=t;
						newAnimation.boneKeyframes[j].positionKeyframes[k].value.x = curves.positionX != null?curves.positionX.Evaluate(t):0.0F;
						newAnimation.boneKeyframes[j].positionKeyframes[k].value.y = curves.positionY != null ? curves.positionY.Evaluate(t) : 0.0F;
						newAnimation.boneKeyframes[j].positionKeyframes[k].value.z = curves.positionZ != null ? curves.positionZ.Evaluate(t) : 0.0F;
						newAnimation.boneKeyframes[j].rotationKeyframes[k].time = t;
						newAnimation.boneKeyframes[j].rotationKeyframes[k].value.x = curves.rotationX != null ? curves.rotationX.Evaluate(t) : 0.0F;
						newAnimation.boneKeyframes[j].rotationKeyframes[k].value.y = curves.rotationY != null ? curves.rotationY.Evaluate(t) : 0.0F;
						newAnimation.boneKeyframes[j].rotationKeyframes[k].value.z = curves.rotationZ != null ? curves.rotationZ.Evaluate(t) : 0.0F;
						newAnimation.boneKeyframes[j].rotationKeyframes[k].value.w = curves.rotationW != null ? curves.rotationW.Evaluate(t) : 1.0F;
					}
					++j;
				}
				// ignore the unfilled values:
				animations[i]= new avs.TransformAnimation();
				animations[i].boneAmount=j;
				animations[i].boneKeyframes = new avs.TransformKeyframe[animations[i].boneAmount];
				for (int k=0;k<j;k++)
				{
					animations[i].boneKeyframes[k] = newAnimation.boneKeyframes[k];
				}
				animations[i].name = Marshal.StringToBSTR(clip.name);
			}

			return animations;
		}

		public static avs.TransformAnimation[] ExtractNonHumanAnimationData(Animator animator)
		{
			if(animator == null)
			{
				Debug.LogWarning($"Null animator passed to ExtractNonHumanAnimationData(...).");
				return new avs.TransformAnimation[0];
			}

			if(animator.runtimeAnimatorController == null)
			{
				Debug.LogWarning($"Null runtimeAnimatorController for {animator.gameObject.name}.");
				return new avs.TransformAnimation[0];
			}

			avs.TransformAnimation[] animations = new avs.TransformAnimation[animator.runtimeAnimatorController.animationClips.Length];

			SkinnedMeshRenderer skinnedMeshRenderer = animator.gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
			if (!skinnedMeshRenderer)
			{
				Debug.LogWarning($"No skinned mesh renderer found for game object tree: {animator.gameObject.name}! If an bone is not found it will just assume it is not meant to be there.");
			}

			for(int i = 0; i < animator.runtimeAnimatorController.animationClips.Length; i++)
			{
				AnimationClip clip = animator.runtimeAnimatorController.animationClips[i];
				avs.TransformAnimation animation = new avs.TransformAnimation();
				animation.name = Marshal.StringToBSTR(clip.name);

				Dictionary<Transform, InterimAnimation> nodeCurves = new Dictionary<Transform, InterimAnimation>();
				foreach(EditorCurveBinding curveBinding in AnimationUtility.GetCurveBindings(clip))
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

				animation.boneKeyframes = new avs.TransformKeyframe[nodeCurves.Count];
				for(int j = 0; j < nodeCurves.Count; j++)
				{
					Transform bone = nodeCurves.Keys.ToArray()[j];
					InterimAnimation interim = nodeCurves[bone];

					avs.TransformKeyframe transformKeyframe = new avs.TransformKeyframe();
					transformKeyframe.nodeID = GeometrySource.GetGeometrySource().FindResourceID(bone);

					if(transformKeyframe.nodeID == 0)
					{
						Debug.LogWarning($"Bone \"{bone.name}\" has animation properties, but could not be found in the geometry source!");
						continue;
					}

					if(interim.positionX != null)
					{
						transformKeyframe.positionKeyframes = new avs.Vector3Keyframe[interim.positionX.length];
						transformKeyframe.positionAmount = interim.positionX.length;
						for(int k = 0; k < interim.positionX.length; k++)
						{
							transformKeyframe.positionKeyframes[k].time = interim.positionX[k].time * 1000; //Convert to milliseconds from seconds.
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
						transformKeyframe.rotationAmount = interim.rotationX.length;
						for(int k = 0; k < interim.rotationX.length; k++)
						{
							transformKeyframe.rotationKeyframes[k].time = interim.rotationX[k].time * 1000; //Convert to milliseconds from seconds.
							transformKeyframe.rotationKeyframes[k].value = new avs.Vector4(interim.rotationX[k].value, interim.rotationY[k].value, interim.rotationZ[k].value, interim.rotationW[k].value);
						}
					}
					else if(interim.eulerRotationX != null)
					{
						transformKeyframe.rotationKeyframes = new avs.Vector4Keyframe[interim.eulerRotationX.length];
						transformKeyframe.rotationAmount = interim.eulerRotationX.length;
						for(int k = 0; k < interim.eulerRotationX.length; k++)
						{
							transformKeyframe.rotationKeyframes[k].time = interim.eulerRotationX[k].time * 1000; //Convert to milliseconds from seconds.
							transformKeyframe.rotationKeyframes[k].value = Quaternion.Euler(interim.eulerRotationX[k].value, interim.eulerRotationY[k].value, interim.eulerRotationZ[k].value); //Convert from euler to quaternion to avs.Vector4.
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
				animation.boneAmount = animation.boneKeyframes.Length;

				animations[i] = animation;
			}

			return animations;
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
					time = unityKeyframe.time * 1000, //Convert to milliseconds from seconds.
					value = unityKeyframe.value
				};

				++i;
			}

			return keyframes;
		}
	}
}
