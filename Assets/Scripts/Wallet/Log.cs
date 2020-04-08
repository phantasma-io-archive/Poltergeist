using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class Log
{
    public enum Level
    {
        Disabled,
        Logic,
        Networking,
        Debug1,
        Debug2,
        Debug3
    }

    public enum UnityDebugLogMode
    {
        Normal,
        Warning,
        Error
    }

    private static string FilePath;
    private static Level MaxLevel = Level.Networking;
    private static bool OverwriteOldContent = false;
    private static bool CompactMode = false;

    public static void Init(string fileName, Level maxLevel, bool forceWorkingFolderUsage = false, bool overwriteOldContent = false)
    {
        MaxLevel = maxLevel;

        OverwriteOldContent = overwriteOldContent;

        FilePath = Application.persistentDataPath;
        if (forceWorkingFolderUsage)
            FilePath = Path.GetFullPath(".");

        FilePath = Path.Combine(FilePath, fileName);
    }

    public static void SwitchToCompactMode(bool compactMode)
    {
        CompactMode = compactMode;
    }

    // WriteWarning() and WriteError() are two Write() wrappers,
    // corresponding to Unity Debug.LogWarning() and Debug.LogError().
    // They are made for better visibility in code.
    public static void WriteWarning(string message, Level level = Level.Logic)
    {
        Write(message, level, UnityDebugLogMode.Warning);
    }

    public static void WriteError(string message, Level level = Level.Logic)
    {
        Write(message, level, UnityDebugLogMode.Error);
    }

    public static void Write(string message, Level level = Level.Logic, UnityDebugLogMode unityDebugLogMode = UnityDebugLogMode.Normal)
    {
        if (MaxLevel != Level.Disabled && level <= MaxLevel)
        {
            FileMode _fileMode = FileMode.Append;

            if (OverwriteOldContent)
            {
                _fileMode = FileMode.Create;
                OverwriteOldContent = false;
            }

            using (FileStream _fileStream = File.Open(FilePath, _fileMode, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter _streamWriter = new StreamWriter(_fileStream))
                {
                    DateTime _now = DateTime.Now;

                    string _additional_padding = "";
                    switch (level)
                    {
                        case Level.Logic:
                            break;
                        case Level.Networking:
                            _additional_padding = "<-> ";
                            break;
                        case Level.Debug1:
                            _additional_padding = new String(' ', 8);
                            break;
                        case Level.Debug2:
                            _additional_padding = new String(' ', 12);
                            break;
                        case Level.Debug3:
                            _additional_padding = new String(' ', 16);
                            break;
                    }

                    // Prefix length: 28 symbols.
                    string _timestamp_prefix_local = "[" + _now.ToString("yyyy.MM.dd HH:mm:ss:ffff") + "]: " + _additional_padding;
                    string _timestamp_prefix_utc = "[" + _now.ToUniversalTime().ToString("yyyy.MM.dd HH:mm") + " UTC    ]  " + _additional_padding;
                    string _empty_prefix = new String(' ', 28) + _additional_padding;

                    int _line_count = 0;
                    foreach (var _line in message.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        _line_count++;

                        switch (_line_count)
                        {
                            case 1:
                                _streamWriter.WriteLine(_timestamp_prefix_local + _line);
                                break;
                            case 2:
                                _streamWriter.WriteLine(_timestamp_prefix_utc + _line);
                                break;
                            default:
                                _streamWriter.WriteLine(_empty_prefix + _line);
                                break;
                        }
                    }

                    if (!CompactMode)
                    {
                        if (_line_count < 2)
                            _streamWriter.WriteLine(_timestamp_prefix_utc);

                        _streamWriter.WriteLine("");
                    }

                    _streamWriter.Flush();
                }
            }
        }

        switch (unityDebugLogMode)
        {
            case UnityDebugLogMode.Normal:
                Debug.Log(message);
                break;
            case UnityDebugLogMode.Warning:
                Debug.LogWarning(message);
                break;
            case UnityDebugLogMode.Error:
                Debug.LogError(message);
                break;
        }
    }
}