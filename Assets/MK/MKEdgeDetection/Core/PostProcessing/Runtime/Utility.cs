/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace MK.EdgeDetection.PostProcessing.Generic
{
    public static class Utility
    {
        public static void Destroy(UnityEngine.Object obj, bool isEditor)
        {
            if(obj != null)
            {
                if(isEditor)
                {
                    if(Application.isPlaying)
                        UnityEngine.Object.Destroy(obj);
                    else
                        UnityEngine.Object.DestroyImmediate(obj);
                }
                else
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
        }
    }
}