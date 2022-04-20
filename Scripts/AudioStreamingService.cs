using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;


namespace teleport
{
	// Place holder for later
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	class AudioStreamingService
    {
        private readonly Teleport_SessionComponent session;
        private readonly TeleportSettings teleportSettings;

        public AudioStreamingService(Teleport_SessionComponent parentComponent)
        {
            session = parentComponent;

            teleportSettings = TeleportSettings.GetOrCreateSettings();
        }

        public void UpdateAudioStreaming()
        {
            
        }
    }
}
