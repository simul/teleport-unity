using System;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	[DisallowMultipleComponent]
	public class Teleport_AnimatorTracker : MonoBehaviour
	{
		#region DllImport

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateNodeAnimation(uid clientID, avs.ApplyAnimation update);

		#endregion //DllImport

		//Teleport_Streamable that is the root of the hierarchy this GameObject with an Animator component is on.
		public Teleport_Streamable hierarchyRoot;

		private Animator animator;
		private SkinnedMeshRenderer skinnedMeshRenderer;

		//Animation update that last occurred.
		private avs.ApplyAnimation lastAnimationUpdate;

		private AnimationClip lastPlayingAnimation;

		//PUBLIC FUNCTIONS

		//Sets the streamed playing animation on this node.
		//Called by an AnimationEvent added to all extracted animations while in play-mode.
		public void SetPlayingAnimation(AnimationClip playingClip)
		{
			SendAnimationUpdate(playingClip);
		}

		//Sends the currently playing animation to the client with the passed ID.
		public void SendPlayingAnimation(uid clientID)
		{
			Client_UpdateNodeAnimation(clientID, lastAnimationUpdate);
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

			skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
			if(!skinnedMeshRenderer)
			{
				Debug.LogWarning($"{nameof(Teleport_AnimatorTracker)} was added to GameObject \"{name}\", but there was no SkinnedMeshRenderer in the hierarchy of this GameObject. Deleting.");
				Destroy(this);
			}
		}

		private void Update()
		{
			if (hierarchyRoot.pollCurrentAnimation)
			{
				AnimatorClipInfo[] animatorClips = animator.GetCurrentAnimatorClipInfo(0);
				AnimationClip playingClip = animatorClips.Length != 0 ? animatorClips[0].clip : null;
				if (playingClip != null && lastPlayingAnimation != playingClip)
				{
					AnimatorStateInfo animatorState = animator.GetCurrentAnimatorStateInfo(0);
					//The animation system uses seconds, but we need milliseconds.
					double timestampOffset = (animatorState.normalizedTime % 1.0) * playingClip.length * 1000;
					SendAnimationUpdate(playingClip, -(long)timestampOffset);
				}
			}
		}

		//PRIVATE FUNCTIONS

		private void SendAnimationUpdate(AnimationClip playingClip, long timestampOffset = 0)
		{
			lastPlayingAnimation = playingClip;

			uid animationID = GeometrySource.GetGeometrySource().FindResourceID(playingClip);
			if(animationID == 0)
			{
				Debug.LogError($"Teleport: Animation update failure! Failed to update playing animation, as \"{playingClip.name}\" has not been extracted!");
				return;
			}

			lastAnimationUpdate = new avs.ApplyAnimation()
			{
				timestamp = CasterMonitor.GetUnixTimestamp() + timestampOffset,
				nodeID = GeometrySource.GetGeometrySource().FindResourceID(skinnedMeshRenderer.gameObject),
				animationID = animationID,
			};

			foreach(Teleport_SessionComponent session in hierarchyRoot.GetActiveSessions())
			{
				SendPlayingAnimation(session.GetClientID());
			}
		}
	}
}
