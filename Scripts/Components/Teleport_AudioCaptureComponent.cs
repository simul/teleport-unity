using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    [RequireComponent(typeof(AudioListener))]
    public class Teleport_AudioCaptureComponent : MonoBehaviour
    {
        #region DLLImports
        [DllImport("SimulCasterServer")]
        static extern void InitializeAudioEncoder(uid clientID, ref SCServer.AudioParams audioEncodeParams);
        [DllImport("SimulCasterServer")]
        static extern void SendAudio(uid clientID, IntPtr data, UInt64 dataSize);
        #endregion

        public uid clientID = 0;
        bool running = false;
        int sampleRate = 0;
        TeleportSettings teleportSettings = null;

        void Start()
        {
            teleportSettings = TeleportSettings.GetOrCreateSettings();
      
            clientID = 0;
            running = true;
            sampleRate = AudioSettings.outputSampleRate;
        }

        private void OnEnable()
        {

        }

        void OnDisable()
        {
            running = false;
            clientID = 0;
        }

        void LateUpdate()
        {
            
        }

        void Initialize()
        {
            var audioParams = new SCServer.AudioParams();
            audioParams.codec = avs.AudioCodec.PCM;
            audioParams.sampleRate = (UInt32)sampleRate;
            audioParams.bitsPerSample = 16;
            audioParams.numChannels = 2;
            InitializeAudioEncoder(clientID, ref audioParams);
        }

        // This function is called on the audio thread
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!running || !teleportSettings.casterSettings.isStreamingAudio || data.Length <= 0)
            {
                return;
            }

            // for now just get latest client
            uid id = Teleport_SessionComponent.GetLastClientID();

            if (id != clientID)
            {
                clientID = id;

                if (clientID != 0)
                {
                    Initialize();
                }
            }

            if (clientID != 0)
            {
                var sizeInBytes = data.Length * 4;
                IntPtr ptr = Marshal.AllocHGlobal(sizeInBytes);
                Marshal.Copy(data, 0, ptr, data.Length);
                // Send audio to the client
                SendAudio(clientID, ptr, (UInt64)sizeInBytes);
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
