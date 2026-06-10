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
	public struct Texture2DProperty
	{
		[field: SerializeField]
		private Texture2D _value;
		public Texture2D value
		{ 
			get { return _value; } 
		}

		public Texture2DProperty(Texture2D value)
		{
			this._value = value;
		}

		public static implicit operator Texture2D(Texture2DProperty texture2DProperty)
		{
			return texture2DProperty._value;
		}

		public static implicit operator Texture2DProperty(Texture2D value)
		{
			return new Texture2DProperty(value);
		}
	}
}