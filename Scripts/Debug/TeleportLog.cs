using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	public class TeleportLog 
	{
		static HashSet<int> hashes=new HashSet<int>();
		// Only reports once.
		public static void LogErrorOnce(string s)
		{
			int hash = s.GetHashCode();
			if (hashes.Contains(hash))
				return;
			Debug.LogError(s);
			hashes.Add(hash);
		}
	}

}