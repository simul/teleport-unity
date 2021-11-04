using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace teleport
{
	//! Component to store a gameObject's streamable properties, e.g. priority.
	class StreamableProperties : MonoBehaviour
	{
		//! Priority. Values greater than or equal to zero are essential to functionality and *must* be streamed. 
		//! Negative values are optional, and the more negative, the less important they are (determining order of sending to client).
		//! The larger the priority value, the earlier the object is sent.
		public int priority=0;
	}
}
