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
	public struct RangeProperty
	{
		[field: SerializeField]
		private float _value;
		public float value
		{ 
			get { return _value; } 
		}

		[field: SerializeField]
		private float _minValue;
		public float minValue 
		{ 
			get { return _minValue; } 
		}

		[field: SerializeField]
		private float _maxValue;
		public float maxValue 
		{ 
			get { return _maxValue; } 
		}

		public RangeProperty(float value, float minValue, float maxValue)
		{
			this._value = value;
			this._minValue = minValue;
			this._maxValue = maxValue;
			this._value = Mathf.Clamp(this._value, this._minValue, this._maxValue);
		}

		public static implicit operator float(RangeProperty rangeProperty)
		{
			return rangeProperty._value;
		}

		public static RangeProperty Lerp(RangeProperty a, RangeProperty b, float t)
		{
			return new RangeProperty(Mathf.Lerp(a, b, t), Mathf.Lerp(a._minValue, b._minValue, t), Mathf.Lerp(a._maxValue, b._maxValue, t));
		}
	}
}