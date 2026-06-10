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
    [CustomPropertyDrawer(typeof(Texture2DProperty))]
    public class Texture2DPropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            valueProperty.objectReferenceValue = EditorGUI.ObjectField(position, label, valueProperty.objectReferenceValue, typeof(Texture2D), false);
            EditorGUI.EndProperty();
        }
    }
}
#endif