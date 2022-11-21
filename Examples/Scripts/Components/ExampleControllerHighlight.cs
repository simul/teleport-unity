using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	using InputID = System.UInt16;
	//! This example operates on a hand controller to highlight its buttons when they are pressed.
	public class ExampleControllerHighlight : MonoBehaviour, SessionSubcomponent
	{
		teleport.Teleport_Controller controller;
		Teleport_SessionComponent session;
		public GameObject ButtonA, ButtonB;
		public string ButtonAPath1, ButtonAPath2;
		public string ButtonBPath1, ButtonBPath2;
		Dictionary<InputID,GameObject> controls=new Dictionary<InputID, GameObject>();
		// Start is called before the first frame update. We can't guarantee Start() won't be called BEFORE the start of the SessionComponent leaving it uninitialized for this function.
		// Therefore we use SessionSubcomponent and OnSessionStart to enforce ordered init.
		public void OnSessionStart()
		{
			teleport.TeleportSettings settings = teleport.TeleportSettings.GetOrCreateSettings();
			controller =GetComponent<Teleport_Controller>();
			session = controller.session;
			controls.Clear();
			BindButton(ButtonA, ButtonAPath1, ButtonAPath2);
			BindButton(ButtonB, ButtonBPath1, ButtonBPath2);
		}
		void BindButton(GameObject b, string path1, string path2="")
		{
			teleport.TeleportSettings settings = teleport.TeleportSettings.GetOrCreateSettings();
			List<InputID> ax = new List<InputID>(settings.FindInputsByPath(path1));
			if (path2.Length > 0)
			{
				List<InputID> x = new List<InputID>(settings.FindInputsByPath(path2));
				ax.AddRange(x);
				HashSet<System.UInt16> left_ax = new HashSet<System.UInt16>();
				foreach (var v in ax)
					left_ax.Add(v);
				ax.Clear();
				foreach (var v in left_ax)
					ax.Add(v);
			}
			InputID[] InputIds = { };
			InputIds = ax.ToArray();
			if (b)
			{
				foreach (var id in InputIds)
					controls[id] = b;
				session.input.AddDelegate(InputIds, Highlight, InputEventType.Press);
				session.input.AddDelegate(InputIds, Unhighlight, InputEventType.Release);
			}
		}
		void Highlight(Input input, InputID inputId)
		{
			GameObject obj=null;
			if(controls.TryGetValue(inputId, out obj))
				session.GeometryStreamingService.SetNodeHighlighted(obj, true);
		}
		void Unhighlight(Input input, InputID inputId)
		{
			GameObject obj = null;
			if (controls.TryGetValue(inputId, out obj))
				session.GeometryStreamingService.SetNodeHighlighted(obj, false);
		}
		// Update is called once per frame
		void Update()
		{
        
		}
	}
}