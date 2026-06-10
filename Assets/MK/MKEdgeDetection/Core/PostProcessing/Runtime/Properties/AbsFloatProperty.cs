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
	public struct AbsFloatProperty
	{
		[field: SerializeField]
		private float _value;
		public float value
		{ 
			get { return _value; } 
		}

		public AbsFloatProperty(float value)
		{
			this._value = Mathf.Max(0, value);
		}

		public static implicit operator float(AbsFloatProperty absFloatProperty)
		{
			return absFloatProperty._value;
		}

		public static implicit operator AbsFloatProperty(float value)
		{
			return new AbsFloatProperty(value);
		}
	}
}