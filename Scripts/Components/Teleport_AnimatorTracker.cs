using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.Animations;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using uid = System.UInt64;

namespace teleport
{
	[DisallowMultipleComponent]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Teleport_AnimatorTracker : MonoBehaviour
	{
		#region DllImport

		[DllImport(TeleportServerDll.name)]
		private static extern void Client_UpdateNodeAnimation(uid clientID, avs.ApplyAnimation update);

		#endregion //DllImport

		//teleport.StreamableRoot that is the root of the hierarchy this GameObject with an Animator component is on.
		public teleport.StreamableRoot hierarchyRoot;

		private Animator animator;
		private Transform skeletonRootTransform;

		//Animation update that last occurred.
		private avs.ApplyAnimation lastAnimationUpdate;

		//PUBLIC FUNCTIONS

		//Sets the streamed playing animation on this node.
		//Called by an AnimationEvent added to all extracted animations while in play-mode.
		public void SetPlayingAnimation(AnimationClip playingClip)
		{
			//SendAnimationUpdate(playingClip, 0, 1.0f,1.0f);
		}

		//Sends the currently playing animation to the client with the passed ID.
		public void SendPlayingAnimation(Teleport_SessionComponent session)
		{
			avs.ApplyAnimation anim= lastAnimationUpdate;
			Client_UpdateNodeAnimation(session.GetClientID(), anim);
		}

		//UNITY MESSAGES

		private void Awake()
		{
			animator = GetComponentInChildren<Animator>();
			if(!animator)
			{
				Debug.LogWarning($"{nameof(Teleport_AnimatorTracker)} was added to GameObject \"{name}\", but there was no Animator in the hierarchy of this GameObject. Deleting.");
				Destroy(this);
			}

			var skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
			if(!skinnedMeshRenderer)
			{
				Debug.LogWarning($"{nameof(Teleport_AnimatorTracker)} was added to GameObject \"{name}\", but there was no SkinnedMeshRenderer in the hierarchy of this GameObject. Deleting.");
				Destroy(this);
			}

			skeletonRootTransform = GeometrySource.GetTopmostSkeletonRoot(skinnedMeshRenderer);
		}
		class ClipTracker
		{
			public float weight=0.0f;
			public bool updated=false;
			public AnimationClip clip;
		};
		class LayerTracker
		{
			public ClipTracker currentClip =new ClipTracker();
			public ClipTracker nextClip =new ClipTracker();
			public int clipCount=0;
			public float transitionDuration;
		}
		List<LayerTracker> layerTrackers = new List<LayerTracker>();
		byte count=0;
		// Polling animation.
		//		Each object has zero or more animation layers.
		//		Each layer has, optionally, a current clip and a next clip it is transitioning to.
		// Our LayerTracker looks out for these to change for each layer.
		private void Update()
		{
			if(hierarchyRoot== null||animator==null)
				return;
			count--;
			if (hierarchyRoot.pollCurrentAnimation&& animator.runtimeAnimatorController!=null)
			{
				while(layerTrackers.Count<animator.layerCount)
				{
					layerTrackers.Add(new LayerTracker());
				}
				// For each layer, ensure that any change to current or next clip is sent.
				for (int layer=0; layer<animator.layerCount; layer++)
				{
					bool changed = false;
					string layerInfo = "Animator Layer " + layer;
					LayerTracker layerTracker= layerTrackers[layer];
					AnimatorClipInfo[] currentAnimatorClips = animator.GetCurrentAnimatorClipInfo(layer);
					AnimatorStateInfo currentAnimatorState = animator.GetCurrentAnimatorStateInfo(layer);
					AnimatorClipInfo[] nextAnimatorClips = animator.GetNextAnimatorClipInfo(layer);
					AnimatorStateInfo nextAnimatorState = animator.GetNextAnimatorStateInfo(layer);
					AnimatorTransitionInfo transitionInfo=animator.GetAnimatorTransitionInfo(layer);
					if (layerTracker.clipCount != currentAnimatorClips.Length)
					{
						changed = true;
						layerTracker.clipCount=currentAnimatorClips.Length;
					}
					if (layerTracker.transitionDuration != transitionInfo.duration)
					{
						changed = true;
						layerTracker.transitionDuration = transitionInfo.duration;
						layerInfo+=", transition "+ transitionInfo.duration;
					}
					AnimationClip currentClip=null,nextClip=null;
					if (currentAnimatorClips.Length > 0)
						currentClip=currentAnimatorClips[0].clip;
					if (nextAnimatorClips.Length > 0)
						nextClip = nextAnimatorClips[0].clip;
					if (layerTracker.currentClip.clip != currentClip)
					{
						changed = true;
						layerTracker.currentClip.clip = currentClip;
					}
					if (layerTracker.nextClip.clip != nextClip)
					{
						changed = true;
						layerTracker.nextClip.clip = nextClip;
					}
					layerInfo+=" Current: "+currentAnimatorClips.Length+" clips.";
					foreach (var clipInfo in currentAnimatorClips)
					{
						layerInfo+=" "+clipInfo.clip.name+"("+ clipInfo.weight+")";
					}
					layerInfo += " Next: " + nextAnimatorClips.Length + " clips.";
					foreach (var clipInfo in nextAnimatorClips)
					{
						layerInfo += " " + clipInfo.clip.name + "(" + clipInfo.weight + ")";
					}
					if(changed)
					{
						// if we have a "next", this is what we send, with the timestamp at the END of the transition.
						if(nextAnimatorClips.Length>0)
						{
							double transitionOffsetUs=transitionInfo.duration*1000000.0;
							Int64 transitionTimestampOffsetUs=(Int64)transitionOffsetUs;
							// We want to know what animation time the animation will be at when the timestamp is at the end of the transition.

							// First, get animation time NOW. The normalized time goes from 0 to 1, and also counts loops. e.g. 2.5 is halfway through the third loop.
							float animTimeAtTimestampS = nextAnimatorState.normalizedTime * nextAnimatorState.length * nextAnimatorState.speed;
							// But we want the time, not now, but at the end of the transition.

							// If duration is in seconds, we must translate that from real time to animation time, using the speed value.
							if(transitionInfo.durationUnit==DurationUnit.Fixed)
							{
								animTimeAtTimestampS += transitionInfo.duration * nextAnimatorState.speed;
							}
							else if (transitionInfo.durationUnit == DurationUnit.Normalized)
							{
							// a normalized transition is expressed as a multiple of the "source state" animation length.
								animTimeAtTimestampS += transitionInfo.duration * currentAnimatorState.length * nextAnimatorState.speed;
							}
							SendAnimationUpdate(layer, nextAnimatorClips[0].clip, teleport.Monitor.GetSessionTimestampNowUs() + transitionTimestampOffsetUs,animTimeAtTimestampS,nextAnimatorState.speed, nextAnimatorState.loop);
						}
						else if (currentAnimatorClips.Length > 0)
						{
							float animTimeAtTimestampS = currentAnimatorState.normalizedTime * currentAnimatorState.length * currentAnimatorState.speed;
							SendAnimationUpdate(layer, currentAnimatorClips[0].clip, teleport.Monitor.GetSessionTimestampNowUs(), animTimeAtTimestampS, currentAnimatorState.speed, currentAnimatorState.loop);
						}
					}
					foreach (var clipInfo in currentAnimatorClips)
					{ 
						/*	ClipTracker clipTracker;
							clipTracker.updated = true;
							clipTracker.weight = clipInfo.weight;
							AnimationClip playingClip = clipInfo.clip;
							if (playingClip != null)
							{
								AnimatorStateInfo animatorState = animator.GetCurrentAnimatorStateInfo(0);
								//We want animTimeAtTimestamp: at the current time, where is this animation in its sequence?
								double timestampOffset = (animatorState.normalizedTime % 1.0) * playingClip.length;/// animatorState.speed;

								SendAnimationUpdate(playingClip, (float)timestampOffset, animatorState.speed, clipInfo.weight);
							}*/
					}
					/*foreach (var clipTracker in clipTrackers)
					{
						if (clipTracker.Value.updated)
							continue;
						// This animation is no longer playing:
						SendAnimationUpdate(clipTracker.Key, 0.0F, 0.0F, 0.0F);
					}*/
					if (changed&&layer==0)
					{
					//UnityEngine.Debug.Log(layerInfo);
					}
				}
				if (layerTrackers.Count > animator.layerCount)
				{
					layerTrackers.RemoveRange(animator.layerCount,layerTrackers.Count-animator.layerCount);
				}
			}
		}

		//PRIVATE FUNCTIONS

		private void SendAnimationUpdate(int animLayer,AnimationClip playingClip,Int64 timestampUs, float animTimeAtTimestamp, float speed,bool loop)
		{
			uid animationID = GeometrySource.GetGeometrySource().FindResourceID(playingClip);
			if(animationID == 0)
			{
				Debug.LogError($"Teleport: Animation update failure! Failed to update playing animation, as \"{playingClip.name}\" has not been extracted!");
				return;
			}

			lastAnimationUpdate = new avs.ApplyAnimation()
			{
				animationLayer=animLayer,
				timestampUs = timestampUs,
				nodeID = GeometrySource.GetGeometrySource().FindResourceID(skeletonRootTransform.gameObject),
				animationID = animationID,
				animTimeAtTimestamp= animTimeAtTimestamp,
				speedUnitsPerSecond = speed,
				loop=loop
			};

			foreach(Teleport_SessionComponent session in hierarchyRoot.GetActiveSessions())
			{
				SendPlayingAnimation(session);
			}
		}
	}
}
