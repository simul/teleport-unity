using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	public static class TeleportLog
	{
		//Contains the hashes of the log messages we have received.
		private static HashSet<int> logHashes = new HashSet<int>();
		//Contains the hashes of the warning messages we have received.
		private static HashSet<int> warningHashes = new HashSet<int>();
		//Contains the hashes of the error messages we have received.
		private static HashSet<int> errorHashes = new HashSet<int>();

		//Clear stored error hashes, so we can print the messages again.
		public static void ClearHashes()
		{
			errorHashes.Clear();
		}

		//Prints a log message, if we have never printed the message before.
		public static void LogOnce(string message)
		{
			int hash = message.GetHashCode();
			if(logHashes.Contains(hash))
			{
				return;
			}

			Debug.Log(message);
			logHashes.Add(hash);
		}

		//Prints a warning message, if we have never printed the message before.
		public static void LogWarningOnce(string message)
		{
			int hash = message.GetHashCode();
			if(warningHashes.Contains(hash))
			{
				return;
			}

			Debug.LogWarning(message);
			warningHashes.Add(hash);
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
