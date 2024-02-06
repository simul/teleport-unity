using UnityEngine;
using System.Collections;

public static class ExtensionMethods
{
	// Note: Making the parameter "this GameObject variableName" makes it an
	// extension method for GameObject. Change it to Transform or whatever
	// to make it a part of Transform, etc etc.
	public static T GetOrAddComponent<T> (this GameObject obj) where T : Component
	{
		// Attempt to get component from GameObject
		T retreivedComp = obj.GetComponent<T>();

		if (retreivedComp != null)
			return retreivedComp;

		// This component wasn't found on the object, so add it.
		return obj.AddComponent<T>();
	}
}