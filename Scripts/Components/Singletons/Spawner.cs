using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace teleport
{
    //! Base class for the singleton component that decides where players appear.
    //! Derive from this to implement your own logic.
    //! The Spawner component should be added to the same gameObject that the Monitor component is on.
    public class Spawner : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            if (gameObject.GetComponent<Monitor>() == null)
            {
                Debug.LogError("Spawner component should be on the same gameObject that the Monitor component is on.");
            }
        }

        //! Spawn is called to get the initial position of a player.
        //! Default behaviour is to look for SpawnPoint objects.
        public virtual bool Spawn(out Vector3 pos,out Quaternion rot)
        {
            int smallestUseCount = 32767;
            SpawnPoint choice = null;
            SpawnPoint[]  spawnPoints =GameObject.FindObjectsOfType<SpawnPoint>();
            foreach (var s in spawnPoints)
            {
                if (s.enabled && s.useCount < smallestUseCount)
                {
                    choice=s;
                    smallestUseCount=s.useCount;
                }
            }
            if (choice)
            {
                pos=choice.transform.position;
                rot=choice.transform.rotation;
                return true;
            }
            pos =new Vector3(0,0,0);
            rot =Quaternion.identity;
            Debug.LogError("No enabled spawnPounts were found. If there is a Spawner component, you should place some SpawnPoints in the scene for it to use.");
            return false;
        }
    }
}