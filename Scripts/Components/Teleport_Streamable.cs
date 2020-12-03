using System.Collections;
using System.Collections.Generic;
using teleport;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    // This component is AUTOMATICALLY added to a gameObject that meets the criteria for geometry streaming.
    [DisallowMultipleComponent]
    public class Teleport_Streamable : MonoBehaviour
    {
    // If the gameObject.
        private uid uid=0;
        /// <summary>
        /// child objects that have no collision, so are streamed automatically with this one.
        /// </summary>
        public List<GameObject> includedChildren = new List<GameObject>();

        public uid GetUid()
        {
            if (uid == 0)
            {
                uid = GeometrySource.GetGeometrySource().FindResourceID(this);
            }
            return uid;
        }
        public void SetUid(uid u)
        {
            if(uid!=0&&u!=uid)
            {
                Debug.LogError("Already have uid " + uid + " but overriding it with uid " + u);
            }
            uid = u;
        }
        static void IterateIncludedChildren(List<GameObject> includedChildren, GameObject gameObject)
        {
            includedChildren.Add(gameObject);
            for (int i=0;i<gameObject.transform.childCount;i++)
            {
                GameObject child=gameObject.transform.GetChild(i).gameObject;
                if(child.GetComponent<Collider>()==null)
                    IterateIncludedChildren(includedChildren, child);
            }
        }
        // Start is called before the first frame update
        void Start()
        {
            //examine the node children of this gameObject.
            // Those without collision will be automatically streamed based on its status.
            includedChildren.Clear();
            if (gameObject)
            {
                IterateIncludedChildren(includedChildren, gameObject);
            }
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
                if (s.GeometryStreamingService != null)
                {
                    s.GeometryStreamingService.StopStreamingGameObject(gameObject);
                }
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