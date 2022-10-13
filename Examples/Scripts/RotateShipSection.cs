using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateShipSection : MonoBehaviour
{
	public float m_Speed = 5.0f;
	public bool m_StartRotation = false;
	public bool m_CounterClockwise = true;

	private Vector3 m_EulerAngleNormalised = new Vector3(0, 0, 1);
	private bool m_Rotating = false;

	private Quaternion m_Station1 = new Quaternion(0, 0, 0, 1);
	private Quaternion m_Station2 = new Quaternion(0, 0, +Mathf.Sqrt(2.0f) / 2.0f, Mathf.Sqrt(2.0f) / 2.0f);
	private Quaternion m_Station3 = new Quaternion(0, 0, 1, 0);
	private Quaternion m_Station4 = new Quaternion(0, 0, -Mathf.Sqrt(2.0f) / 2.0f, Mathf.Sqrt(2.0f) / 2.0f);

	Quaternion m_CurrentStation, m_NextStation;

	// Start is called before the first frame update
	void Start()
	{
		transform.rotation = new Quaternion();
		m_CurrentStation = m_Station1;
		m_NextStation = m_Station2;
	}

	// Update is called once per frame
	void Update()
	{
		if (m_StartRotation && !m_Rotating)
		{
			SetNextStation();
			m_StartRotation = false;
			m_Rotating = true;
		}

		if(m_Rotating)
		{
			RotateToNextStation(Time.deltaTime);
		}
	}

	void RotateToNextStation(float deltaTime)
	{
		transform.Rotate(m_EulerAngleNormalised * m_Speed * (m_CounterClockwise ? 1.0f : -1.0f) * deltaTime);

		if (m_NextStation == transform.rotation)//TODO: fix fp errors
		{
			m_Rotating = false;
			m_CurrentStation = m_NextStation;
			SetNextStation();
		}
	}

	void SetNextStation()
	{
		if (m_CurrentStation == m_Station1)
			m_NextStation = m_Station2;
		else if (m_CurrentStation == m_Station2)
			m_NextStation = m_Station3;
		else if (m_CurrentStation == m_Station3)
			m_NextStation = m_Station4;
		else if (m_CurrentStation == m_Station4)
			m_NextStation = m_Station1;
	}
}
