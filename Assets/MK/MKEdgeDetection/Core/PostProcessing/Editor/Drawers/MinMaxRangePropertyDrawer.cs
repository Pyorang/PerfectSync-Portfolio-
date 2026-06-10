/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

namespace MK.EdgeDetection.PostProcessing.Generic.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxRangeProperty))]
    public class MinMaxRangePropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty minValueProperty = property.FindPropertyRelative("_minValue");
            SerializedProperty maxValueProperty = property.FindPropertyRelative("_maxValue");
            SerializedProperty minLimitProperty = property.FindPropertyRelative("_minLimit");
            SerializedProperty maxLimitProperty = property.FindPropertyRelative("_maxLimit");

            float minValue = minValueProperty.floatValue;
			float maxValue = maxValueProperty.floatValue;

            Rect minRect = new Rect(position.xMin + EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing, position.y, EditorGUIUtility.fieldWidth, position.height);
			Rect maxRect = new Rect(position.xMax - EditorGUIUtility.fieldWidth, position.y, EditorGUIUtility.fieldWidth, position.height);
            Rect sliderRect = new Rect(minRect.xMax + EditorGUIUtility.standardVerticalSpacing * 2, position.y, position.xMax - minRect.xMax - maxRect.width - EditorGUIUtility.standardVerticalSpacing * 4, position.height);

            EditorGUI.LabelField(position, label);
            minValue = EditorGUI.FloatField(minRect, minValue);
            maxValue = EditorGUI.FloatField(maxRect, maxValue);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, minLimitProperty.floatValue, maxLimitProperty.floatValue);

            if(minValue < maxValue)
                minValueProperty.floatValue = minValue;
            if(maxValue > minValue)
                maxValueProperty.floatValue = maxValue;

            EditorGUI.EndProperty();
        }
    }
}
#endif