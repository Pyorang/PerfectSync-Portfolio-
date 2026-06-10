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
	public struct Vector2Property
	{
		[field: SerializeField]
		private UnityEngine.Vector2 _value;
		public UnityEngine.Vector2 value
		{ 
			get { return _value; } 
		}

		public Vector2Property(float x, float y)
		{
			this._value = new UnityEngine.Vector2(x, y);
		}

		public static implicit operator UnityEngine.Vector2(Vector2Property vector2Property)
		{
			return vector2Property._value;
		}

		public static implicit operator Vector2Property(UnityEngine.Vector2 value)
		{
			return new Vector2Property(value.x, value.y);
		}
	}
}