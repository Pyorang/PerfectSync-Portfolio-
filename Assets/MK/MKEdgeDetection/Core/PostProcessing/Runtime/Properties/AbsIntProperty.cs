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
	public struct AbsIntProperty
	{
		[field: SerializeField]
		private int _value;
		public int value
		{ 
			get { return _value; } 
		}

		public AbsIntProperty(int value)
		{
			this._value = Mathf.Max(0, value);
		}

		public static implicit operator int(AbsIntProperty absIntProperty)
		{
			return absIntProperty._value;
		}

		public static implicit operator AbsIntProperty(int value)
		{
			return new AbsIntProperty(value);
		}
	}
}