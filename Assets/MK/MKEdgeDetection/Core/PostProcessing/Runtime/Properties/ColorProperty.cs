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
	public struct ColorProperty
	{
		[field: SerializeField]
		private Color _value;
		public Color value
		{ 
			get { return _value; } 
		}

		[field: SerializeField]
		private bool _useAlpha;
		public bool useAlpha
		{ 
			get { return _useAlpha; } 
		}

		[field: SerializeField]
		private bool _useHDR;
		public bool useHDR 
		{ 
			get { return _useHDR; } 
		}

		public ColorProperty(float r, float g, float b, float a, bool useAlpha, bool useHDR)
		{
			this._value = new Color(r, g, b, a);
			this._useAlpha = useAlpha;
			this._useHDR = useHDR;
		}

		public static implicit operator Color(ColorProperty floatProperty)
		{
			return floatProperty._value;
		}

		public static ColorProperty Lerp(ColorProperty from, ColorProperty to, float t)
		{
			Color lerpedColor = Color.Lerp(from._value, to._value, t);
			return new ColorProperty(lerpedColor.r, lerpedColor.g, lerpedColor.b, lerpedColor.a, t > 0.5f ? to.useAlpha : from.useAlpha, t > 0.5f ? to.useHDR : from.useHDR);
		}
	}
}