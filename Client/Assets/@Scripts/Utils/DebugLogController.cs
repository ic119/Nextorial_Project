using UnityEngine;

public class DebugLogController
{
    public static void GenerateLogMessage<T>(string message)
    {
        string word = string.Format(">>>>> UnityLog_[{0}]:{1}", typeof(T).Name, message);
        UnityEngine.Debug.Log(word);
    }
    public static void GenerateErrorMessage<T>(string message)
    {
        string word = string.Format(">>>>> UnityLog_[{0}]:{1}", typeof(T).Name, message);
        UnityEngine.Debug.LogError(word);
    }
}