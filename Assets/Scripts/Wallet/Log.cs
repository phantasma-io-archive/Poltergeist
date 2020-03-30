using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class Log
{
    public enum DetailsLevel
    {
        LogicLevel,
        NetworkingLevel,
        LowLevel1,
        LowLevel2,
        LowLevel3
    }

    private static string FilePath = "uninitialized.log";
    private static DetailsLevel MaxDetailsLevel = DetailsLevel.NetworkingLevel;
    private static bool OverwriteOldContent = false;
    private static bool CompactMode = false;

    public static void Init(string filePath, DetailsLevel maxDetailsLevel, bool overwriteOldContent = false)
    {
        FilePath = filePath;
        MaxDetailsLevel = maxDetailsLevel;

        OverwriteOldContent = overwriteOldContent;
    }

    public static void SwitchToCompactMode(bool compactMode)
    {
        CompactMode = compactMode;
    }

    public static void Write(string message, DetailsLevel detailsLevel = DetailsLevel.LogicLevel)
    {
        if(detailsLevel > MaxDetailsLevel)
            return;

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
                switch (detailsLevel)
                {
                    case DetailsLevel.LogicLevel:
                        break;
                    case DetailsLevel.NetworkingLevel:
                        _additional_padding = "<-> ";
                        break;
                    case DetailsLevel.LowLevel1:
                        _additional_padding = new String(' ', 8);
                        break;
                    case DetailsLevel.LowLevel2:
                        _additional_padding = new String(' ', 12);
                        break;
                    case DetailsLevel.LowLevel3:
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
}