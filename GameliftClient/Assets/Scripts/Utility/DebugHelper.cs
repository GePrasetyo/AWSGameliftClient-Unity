using UnityEngine;
using System;

public class DebugHelper
{
    public static void Warning(string msg)
    {
        Debug.LogWarning(msg);
    }

    public static void Error(string msg)
    {
        Debug.LogError(msg);
    }

    public static void Default(string msg)
    {
        Debug.Log(msg);
    }

    public static void Excpetion(Exception ex)
    {
        Debug.LogException(ex);
    }
}
