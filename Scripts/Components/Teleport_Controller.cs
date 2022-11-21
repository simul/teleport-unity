using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Teleport_Controller : MonoBehaviour
	{
		public const int MAX_CONTROLLERS = 2;

		[Tooltip("GameObject with the controller's mesh component.")]
		public GameObject controllerModel = default;
		[Tooltip("Animation played when the trigger is pressed.")]
		public AnimationClip triggerPressAnimation = default;
		[Tooltip("Override for the animation's current time.")]
		public avs.AnimationTimeControl pressAnimationTimeOverride = avs.AnimationTimeControl.ANIMATION_TIME;

		[Tooltip("A full or partial OpenXR path expressed as a Regular Expression.\nThe client should try to match this path with one of its available pose inputs.")]
		/// <summary>
		/// A full or partial OpenXR path expressed as a Regular Expression(https://en.wikipedia.org/wiki/Regular_expression). The client should try to match this path with one or more of its available inputs.
		/// </summary>
		public string poseRegexPath;
		uid _uid=0;
		public uid uid
		{
			 get
			{
				return _uid;
			}
		}
		public teleport.Teleport_SessionComponent session = default;

		///UNITY MESSAGES

		private void OnEnable()
		{
		}

		private void Update()
		{
		}
	}
}
