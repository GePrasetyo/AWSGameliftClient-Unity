using UnityEngine;
using System;
using Random = UnityEngine.Random;

public class ServerLoggingFile : MonoBehaviour
{
    public static string Logfilename = "";

    void OnEnable()
    {
        Application.logMessageReceived += Log;

        Debug.Log("Log Recording Start");
    }
    void OnDisable() { Application.logMessageReceived -= Log; }

    public void Log(string logString, string stackTrace, LogType type)
    {
        if (Logfilename == "")
        {
            string d = System.IO.Directory.GetCurrentDirectory().ToString() + "/logs";
            System.IO.Directory.CreateDirectory(d);
            Logfilename = string.Format("{0}/server_{1}.txt", d, Random.Range(0, 2000));
        }

        try
        {
            System.IO.File.AppendAllText(Logfilename, DateTime.UtcNow +"(" +type+ ") : " +logString + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError("Error writing log : " +e);
        }
    }
}