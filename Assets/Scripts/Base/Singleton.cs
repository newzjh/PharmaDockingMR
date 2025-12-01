// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;


public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _Instance;

    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                T[] talls=Resources.FindObjectsOfTypeAll<T>();
                if (talls != null && talls.Length > 0)
                {
                    _Instance = talls[0];
                }
            }
            return _Instance;
        }
    }
}
