using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{ 
	/// <summary>
	/// Tags this object as the root of a skeleton. Meshes bound to its children will use this as the root, so the the skeleton is shared.
	/// </summary>
	public class SkeletonRoot : MonoBehaviour
	{
		public string assetPath;
		// Start is called before the first frame update
		void Start()
		{
        
		}
		// Editor-only component.
		private void Reset()
		{
			//hideFlags = HideFlags.DontSaveInBuild;
		}

		// Update is called once per frame
		void Update()
		{
        
		}
	}
}
