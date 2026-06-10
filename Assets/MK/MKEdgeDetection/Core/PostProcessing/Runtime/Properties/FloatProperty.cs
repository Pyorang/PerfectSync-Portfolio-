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
	public struct FloatProperty
	{
		[field: SerializeField]
		private float _value;
		public float value
		{ 
			get { return _value; } 
		}

		public FloatProperty(float value)
		{
			this._value = value;
		}

		public static implicit operator float(FloatProperty floatProperty)
		{
			return floatProperty._value;
		}

		public static implicit operator FloatProperty(float value)
		{
			return new FloatProperty(value);
		}
	}
}