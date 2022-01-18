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
        [DllImport("TeleportServer")]
        static extern void SetAudioSettings(ref AudioSettings audioSettings);
        [DllImport("TeleportServer")]
        static extern void SendAudio(IntPtr data, UInt64 dataSize);
        #endregion

        TeleportSettings teleportSettings = null;
        bool running = false;
        bool initialized = false;
        int sampleRate = 0;

        void Start()
        {
            teleportSettings = TeleportSettings.GetOrCreateSettings();
            running = true;
            initialized = false;
            sampleRate = UnityEngine.AudioSettings.outputSampleRate;
        }

        private void OnEnable()
        {

        }

        void OnDisable()
        {
            running = false;
            initialized = false;
        }

        void LateUpdate()
        {
            var heads = FindObjectsOfType<Teleport_Head>();
            Vector3 pos = Vector3.zero;
            foreach (var head in heads)
            {
                pos += head.gameObject.transform.position;
            }
            pos /= heads.Length;
            gameObject.transform.position = pos;
        }

        void Initialize()
        {
            var audioSettings = new AudioSettings();
            audioSettings.codec = avs.AudioCodec.PCM;
            audioSettings.sampleRate = (UInt32)sampleRate;
            audioSettings.bitsPerSample = 32;
            audioSettings.numChannels = 2;
            SetAudioSettings(ref audioSettings);
            initialized = true;
        }

        // This function is called on the audio thread
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!running || !teleportSettings.casterSettings.isStreamingAudio || data.Length <= 0)
            {
                return;
            }

            if (!initialized)
            {
                Initialize();
            }

            var sizeInBytes = data.Length * 4;
            IntPtr ptr = Marshal.AllocHGlobal(sizeInBytes);
            Marshal.Copy(data, 0, ptr, data.Length);
            // Send audio to the client
            SendAudio(ptr, (UInt64)sizeInBytes);
            Marshal.FreeHGlobal(ptr);  
        }
    }
}
