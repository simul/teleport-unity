using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    //[RequireComponent(typeof(AudioListener))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
    public class Teleport_AudioCaptureComponent : MonoBehaviour
    {
        #region DLLImports
        [DllImport(TeleportServerDll.name)]
        static extern void Server_SetAudioSettings(ref AudioSettings audioSettings);
        [DllImport(TeleportServerDll.name)]
        static extern void Server_SendAudio(IntPtr data, UInt64 dataSize);
        #endregion

        TeleportSettings teleportSettings = null;
        bool running = false;
        bool initialized = false;
        int sampleRate = 0;

        void Start()
        {
           
        }

        void OnEnable()
        {
            teleportSettings = TeleportSettings.GetOrCreateSettings();
            running = true;
            initialized = false;
            sampleRate = UnityEngine.AudioSettings.outputSampleRate;
        }

        void OnDisable()
        {
            running = false;
            initialized = false;
        }

        void LateUpdate()
        {
            if (!running || !teleportSettings.serverSettings.isStreamingAudio)
            {
                return;
            }
            var heads = FindObjectsOfType<Teleport_Head>();
			if (heads.Length>0)
			{
				Vector3 pos = Vector3.zero;
				foreach (var head in heads)
				{
					pos += head.gameObject.transform.position;
				}
				pos /= heads.Length;
				gameObject.transform.position = pos;
			}
		}

        void Initialize()
        {
            var audioSettings = new AudioSettings();
            audioSettings.codec = avs.AudioCodec.PCM;
            audioSettings.sampleRate = (UInt32)sampleRate;
            audioSettings.bitsPerSample = 32;
            audioSettings.numChannels = 2;
			Server_SetAudioSettings(ref audioSettings);
            initialized = true;
        }

        // This function is called on the audio thread
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!running || !teleportSettings.serverSettings.isStreamingAudio || data.Length <= 0)
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
			Server_SendAudio(ptr, (UInt64)sizeInBytes);
            Marshal.FreeHGlobal(ptr);  
        }
    }
}
