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
	public struct EnumProperty<T> where T : System.Enum
	{
		[SerializeField]
		private T _value;
		public T value
		{ 
			get { return _value; } 
		}

		public EnumProperty(T value)
		{
			this._value = value;
		}

		public static implicit operator T(EnumProperty<T> enumProperty)
		{
			return enumProperty._value;
		}

		public static implicit operator EnumProperty<T>(T value)
		{
			return new EnumProperty<T>(value);
		}
	}
}