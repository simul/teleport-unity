using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	public static class TeleportLog
	{
		//Contains the hashes of the error messages we have received.
		private static HashSet<int> errorHashes = new HashSet<int>();

		//Clear stored error hashes, so we can print the messages again.
		public static void ClearHashes()
		{
			errorHashes.Clear();
		}

		//Prints an error message, if we have never printed the message before.
		public static void LogErrorOnce(string message)
		{
			int hash = message.GetHashCode();
			if(errorHashes.Contains(hash))
			{
				return;
			}
			
			Debug.LogError(message);
			errorHashes.Add(hash);
		}
	}
}
