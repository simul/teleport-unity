using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SessionComponent : MonoBehaviour
{
    private List<Collider> streamedObjects = new List<Collider>();

    private void LateUpdate()
    {
        int layerMask = LayerMask.NameToLayer(CasterMonitor.GEOMETRY_LAYER_NAME);

        if(layerMask != -1)
        {
            layerMask = 1 << 8;
            List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, CasterMonitor.GetCasterSettings().detectionSphereRadius, layerMask));
            List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, CasterMonitor.GetCasterSettings().detectionSphereRadius + CasterMonitor.GetCasterSettings().detectionSphereBufferDistance, layerMask));

            List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedObjects));
            List<Collider> lostColliders = new List<Collider>(streamedObjects.Except(outerSphereCollisions));

            foreach(Collider collider in gainedColliders)
            {
                streamedObjects.Add(collider);
                ///TODO: Add collider.gameobject as a streamed object.
            }

            foreach(Collider collider in lostColliders)
            {
                streamedObjects.Remove(collider);
                ///TODO: Remove collider.gameobject as a streamed object.
            }
        }
        else
        {
            Debug.LogError("\"" + CasterMonitor.GEOMETRY_LAYER_NAME + "\" physics layer is not defined! Please create this layer mask, then assign it to the geometry you want to be streamed.");
        }
    }
}
