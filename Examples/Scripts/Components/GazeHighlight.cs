using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeHighlight : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

	GameObject highlightedObject = null;
	GameObject flagObject = null;
	public GameObject flagPrefab;
	// Update is called once per frame
	void Update()
	{
		// Bit shift the index of the layer (8) to get a bit mask
		int layerMask = 1 << 8;

		// This would cast rays only against colliders in layer 8.
		// But instead we want to collide against everything except layer 8. The ~ operator does this, it inverts a bitmask.
		layerMask = ~layerMask;
		RaycastHit hit;
		teleport.Teleport_SessionComponent session = gameObject.GetComponentInParent<teleport.Teleport_SessionComponent>();

		if (highlightedObject == null)
		{
			// Does the ray intersect any objects excluding the player layer
			if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, layerMask))
			{
				GazeHighlightable gh=hit.collider.GetComponent<GazeHighlightable>();
				if (gh)
				{
					//Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
					//Debug.Log("GazeHighlight " + hit.collider.gameObject.name);
					highlightedObject=hit.collider.gameObject;
					session.GeometryStreamingService.SetNodeHighlighted(highlightedObject, true);

					// add a child object:
					Vector3 position= highlightedObject.transform.position;
					position.y+=1.0F;
					flagObject=UnityEngine.Object.Instantiate(flagPrefab, position, Quaternion.identity, highlightedObject.transform);
				}
			}
		}
		else
		{
			bool res= Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, layerMask);
			if ((!res||!hit.collider||hit.collider.gameObject!=highlightedObject)&&(!hit.collider||hit.collider.gameObject!= flagObject))
			{
				session.GeometryStreamingService.SetNodeHighlighted(highlightedObject, false);
				highlightedObject = null;
				UnityEngine.Object.Destroy(flagObject);
			}
		}
	}
}
