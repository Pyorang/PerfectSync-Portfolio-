/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.EdgeDetection.PostProcessing.Generic.Editor
{
    public static class Utility
    {
        public static T[] GetAllValuesOfEnum<T>() where T : System.Enum
        {
            return (T[]) Enum.GetValues(typeof(T));
        }

        public static bool ValidateDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, params TKey[] skippedValidationValues)
        where TKey : System.Enum
        {
            bool isValid = true;

            List<TKey> allPossibleKeys = GetAllValuesOfEnum<TKey>().ToList();
            foreach(TKey key in skippedValidationValues)
                allPossibleKeys.Remove(key);

            foreach(TKey key in allPossibleKeys)
            {
                if(!dictionary.ContainsKey(key))
                {
                    UnityEngine.Debug.LogError($"Key {key} is missing in dictionary, but required for validation...");
                    isValid = false;
                    break;
                }
            }

            return isValid;
        }
    }
}
#endif