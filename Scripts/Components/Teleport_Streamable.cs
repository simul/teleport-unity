using System.Collections;
using System.Collections.Generic;
using teleport;
using UnityEngine;

namespace teleport
{
    // This component is AUTOMATICALLY added to a gameObject that meets the criteria for geometry streaming.
    public class Teleport_Streamable : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
        private void OnDestroy()
        {
            // Are there sessions still using this object?
            List<Teleport_SessionComponent> ss = new List<Teleport_SessionComponent>();
            foreach (var s in sessions)
                ss.Add(s);
            foreach (var s in ss)
            {
                s.GeometryStreamingService.StopStreamingGameObject(gameObject);
            }
        }
        HashSet<Teleport_SessionComponent> sessions=new HashSet<Teleport_SessionComponent>();
        public void AddStreamingClient(Teleport_SessionComponent s)
        {
            sessions.Add(s);
        }
        public void RemoveStreamingClient(Teleport_SessionComponent s)
        {
            sessions.Remove(s);
        }
    }
}