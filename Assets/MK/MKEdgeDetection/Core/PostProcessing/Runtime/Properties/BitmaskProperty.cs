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
	public struct BitmaskProperty<T> where T : System.Enum
	{
		[SerializeField]
		private T _value;
		public T value
		{ 
			get { return _value; } 
		}

		public BitmaskProperty(T value)
		{
			this._value = value;
		}

		public static implicit operator T(BitmaskProperty<T> bitmaskProperty)
		{
			return bitmaskProperty._value;
		}

		public static implicit operator BitmaskProperty<T>(T value)
		{
			return new BitmaskProperty<T>(value);
		}
	}
}