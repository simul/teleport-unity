using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace teleport
{
    public class TeleportDll
    {
        public const string teleport_server_dll = @"Teleport";
        static bool _initialized = false;
        [DllImport(teleport_server_dll)]
        private static extern void RegisterPlugin();

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}