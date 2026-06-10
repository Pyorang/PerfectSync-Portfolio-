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
	public struct IntProperty
	{
		[field: SerializeField]
		private int _value;
		public int value
		{ 
			get { return _value; } 
		}

		public IntProperty(int value)
		{
			this._value = value;
		}

		public static implicit operator int(IntProperty intProperty)
		{
			return intProperty._value;
		}

		public static implicit operator IntProperty(int value)
		{
			return new IntProperty(value);
		}
	}
}