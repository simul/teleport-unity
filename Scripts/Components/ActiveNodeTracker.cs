using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{ 
	//This component is automatically added to streamable GameObjects and their children.
	[DisallowMultipleComponent]
	public class ActiveNodeTracker : MonoBehaviour
	{
		public delegate void Report(ActiveNodeTracker t,bool activate);

		double _timeAtLastChange=0.0;
		public double timeAtLastChange
		{
			get
			{
				return _timeAtLastChange;
			}
		}
		public Report report;
		void Start()
		{
        
		}
		void Update()
		{

		}
		void OnDisable()
		{
			_timeAtLastChange = Time.fixedTimeAsDouble;
			if (report != null)
			{ 
				report(this,false);
			}
		}

		void OnEnable()
		{
			_timeAtLastChange = Time.fixedTimeAsDouble;
			if (report != null)
			{
				report(this, true);
			}
		}
	}
}