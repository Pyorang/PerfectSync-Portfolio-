/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace MK.EdgeDetection.PostProcessing.Generic.Editor
{
    public abstract class BitmaskPropertyDrawer<T> : UnityEditor.PropertyDrawer where T : System.Enum
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {            
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            valueProperty.enumValueFlag = (int) (System.Object) EditorGUI.EnumFlagsField(position, label, (T) (System.Object) valueProperty.enumValueFlag);
            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif