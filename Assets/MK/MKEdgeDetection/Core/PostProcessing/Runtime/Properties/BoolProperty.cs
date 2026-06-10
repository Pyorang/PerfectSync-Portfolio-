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
	public struct BoolProperty
	{
		[field: SerializeField]
		private bool _value;
		public bool value
		{ 
			get { return _value; } 
		}

		public BoolProperty(bool value)
		{
			this._value = value;
		}

		public static implicit operator bool(BoolProperty boolProperty)
		{
			return boolProperty._value;
		}

		public static implicit operator BoolProperty(bool value)
		{
			return new BoolProperty(value);
		}
	}
}