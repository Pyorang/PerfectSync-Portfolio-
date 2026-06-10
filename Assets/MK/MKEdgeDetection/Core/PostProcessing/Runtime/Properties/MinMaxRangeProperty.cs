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
	public struct MinMaxRangeProperty
	{
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

		[field: SerializeField]
		private float _minLimit;
		public float minLimit 
		{ 
			get { return _minLimit; } 
		}

		[field: SerializeField]
		private float _maxLimit;
		public float maxLimit 
		{ 
			get { return _maxLimit; } 
		}

		public MinMaxRangeProperty(float minValue, float maxValue, float minLimit, float maxLimit)
		{
			this._minValue = minValue;
			this._maxValue = maxValue;
			this._minLimit = minLimit;
			this._maxLimit = maxLimit;
		}

		public static MinMaxRangeProperty Lerp(MinMaxRangeProperty a, MinMaxRangeProperty b, float t)
		{
			MinMaxRangeProperty lerpedProperty = t > 0.5f ? b : a;
			lerpedProperty._maxValue = Mathf.Lerp(a.maxValue, b.maxValue, t);
			lerpedProperty._minValue = Mathf.Lerp(a.minValue, b.minValue, t);
			return lerpedProperty;
		}
	}
}