using UnityEditor;
using UnityEngine;
using System;
using System.Collections;

public class LaunchConifersInstaller
{
    static LaunchConifersInstaller()
    {
        EditorApplication.update += Update;
    }


    static void Update()
    {
        EditorApplication.update -= Update;

        if( !EditorApplication.isPlayingOrWillChangePlaymode )
        {
            ConifersInstaller.Init();
        }
    }
}
