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
	public struct Vector4Property
	{
		[field: SerializeField]
		private UnityEngine.Vector4 _value;
		public UnityEngine.Vector4 value
		{ 
			get { return _value; } 
		}

		public Vector4Property(float x, float y, float z, float w)
		{
			this._value = new UnityEngine.Vector4(x, y, z, w);
		}

		public static implicit operator UnityEngine.Vector4(Vector4Property vector4Property)
		{
			return vector4Property._value;
		}

		public static implicit operator Vector4Property(UnityEngine.Vector4 value)
		{
			return new Vector4Property(value.x, value.y, value.z, value.w);
		}
	}
}