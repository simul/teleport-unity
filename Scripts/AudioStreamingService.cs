using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;


namespace teleport
{
    // Place holder for later
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
