﻿using System;
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
        static extern void InitializeAudioEncoder(uid clientID, ref SCServer.AudioEncodeParams audioEncodeParams);
        [DllImport("SimulCasterServer")]
        static extern void SendAudio(uid clientID, IntPtr data, UInt64 dataSize);
        #endregion

        public uid clientID = 0;

        TeleportSettings teleportSettings = null;
        bool running = false;

        void Start()
        {
            teleportSettings = TeleportSettings.GetOrCreateSettings();
            //double startTick = AudioSettings.dspTime;
            // sampleRate = AudioSettings.outputSampleRate;
        }

        private void OnEnable()
        {

        }

        void OnDisable()
        {
            
        }

        void LateUpdate()
        {
            if (!teleportSettings.casterSettings.isStreamingAudio)
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
                    running = true;
                }
                else
                {
                    running = false;
                }
            }
        }

        void Initialize()
        {
            var audioParams = new SCServer.AudioEncodeParams();
            audioParams.codec = avs.AudioCodec.PCM;
            InitializeAudioEncoder(clientID, ref audioParams);
        }

        // This function is called on the audio thread
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!running || data.Length <= 0)
                return;

            var sizeInBytes = data.Length * 4;
            IntPtr ptr = Marshal.AllocHGlobal(sizeInBytes);
            Marshal.Copy(data, 0, ptr, data.Length);
            // Send audio to the client
            SendAudio(clientID, ptr, (UInt64)sizeInBytes);
            Marshal.FreeHGlobal(ptr);
        }
    }
}
