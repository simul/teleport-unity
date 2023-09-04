using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Teleport_Controller : MonoBehaviour
	{
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
		teleport.Teleport_SessionComponent session = default;
		public void SetSession(teleport.Teleport_SessionComponent s)
		{
			session = s;
		}

		///UNITY MESSAGES

		private void OnEnable()
		{
		}
		private void Update()
		{
		}
	}
}
