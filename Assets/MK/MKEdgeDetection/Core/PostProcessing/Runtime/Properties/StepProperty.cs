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
	public struct StepProperty
	{
		[field: SerializeField]
		private int _value;
		public int value
		{ 
			get { return _value; } 
		}

		[field: SerializeField]
		private int _minValue;
		public int minValue 
		{ 
			get { return _minValue; } 
		}

		[field: SerializeField]
		private int _maxValue;
		public int maxValue 
		{ 
			get { return _maxValue; } 
		}

		public StepProperty(int value, int minValue, int maxValue)
		{
			this._value = value;
			this._minValue = minValue;
			this._maxValue = maxValue;
			this._value = Mathf.Clamp(this._value, this._minValue, this._maxValue);
		}

		public static implicit operator int(StepProperty stepProperty)
		{
			return stepProperty._value;
		}

		public static StepProperty Lerp(StepProperty a, StepProperty b, float t)
		{
			return new StepProperty(t > 0.5f ? b._minValue : a._minValue, t > 0.5f ? b._minValue : a._minValue, t > 0.5f ? b._maxValue : a._maxValue);
		}
	}
}