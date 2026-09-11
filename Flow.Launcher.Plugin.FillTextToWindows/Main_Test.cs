using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;

namespace Flow.Launcher.Plugin.FillTextToWindows;

public class Main_Test
{
    public static void Main()
    {
        test_parse_key();
    }

    public static void Start()
    {
    }

    private static void test_parse_key()
    {
        var samples = new List<string>
        {
            "Ctrl++",
            "Ctrl+\\+",
            "Ctrl + \\+",
            "Shift+Tab",
            "Down, Down, Enter",
            "Ctrl+A Delete",
            "Ctrl+, ,",
        };

        foreach (var sample in samples)
        {
            if (KeyParser.TryParse(sample, out var chords, out var error))
            {
                Console.WriteLine($"{sample,-20} -> {string.Join(", ", chords.Select(chord => chord.Text))}");
            }
            else
            {
                Console.WriteLine($"{sample,-20} -> 错了：{error}");
            }
        }
    }
}
