/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/
using UnityEngine;
using System.Collections;

namespace MK.EdgeDetection.PostProcessing.Generic
{
	[System.Serializable]
	public struct Vector3Property
	{
		[field: SerializeField]
		private UnityEngine.Vector3 _value;
		public UnityEngine.Vector3 value
		{ 
			get { return _value; } 
		}

		public Vector3Property(float x, float y, float z)
		{
			this._value = new UnityEngine.Vector3(x, y, z);
		}

		public static implicit operator UnityEngine.Vector3(Vector3Property vector3Property)
		{
			return vector3Property._value;
		}

		public static implicit operator Vector3Property(UnityEngine.Vector3 value)
		{
			return new Vector3Property(value.x, value.y, value.z);
		}
	}
}