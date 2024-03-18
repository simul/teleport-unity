using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	//! Component to store a gameObject's streamable properties, e.g. priority.
	class StreamableProperties : MonoBehaviour
	{
		public uint _renderingMask =0;
		//! Don't use as a streaming root, even if it has a collider.
		public bool streamOnlyWithParent=false;
		public bool smoothMotionAtClient=true;
		public bool includeChildren = true;
		bool _isStationary=true;
		public bool isStationary
		{
			get
			{
				return _isStationary;
			}
			#if UNITY_EDITOR
			set
			{
				_isStationary=value;
			}
			#endif
		}
		public uint RenderingMask
		{
		get
			{
				_renderingMask = 0;
				var renderers=GetComponents<Renderer>();
				foreach (Renderer renderer in renderers)
				{
					// Previously we &'d with the existing mask, but that causes bad behaviour if the mask is left in the wrong state and the object is saved.
					_renderingMask |= renderer.renderingLayerMask;
				}
				return _renderingMask;
			}
			set
			{
				_renderingMask=value;
			}
		}
	}
}
