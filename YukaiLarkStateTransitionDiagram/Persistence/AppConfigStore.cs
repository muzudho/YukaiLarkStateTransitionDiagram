namespace YukaiLarkStateTransitionDiagram.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

/// <summary>
/// アプリケーション設定を永続化するためのストアです。
/// </summary>
public sealed class AppConfig
{
    public const int MaxRecentFiles = 10;

    public string? LastOpenedFile { get; set; }
    public List<string> RecentFiles { get; set; } = new();

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        LastOpenedFile = fullPath;
        RecentFiles = (RecentFiles ?? new List<string>())
            .Where(file => !string.Equals(file, fullPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(fullPath)
            .Where(File.Exists)
            .Take(MaxRecentFiles)
            .ToList();
    }
}

public static class AppConfigStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static string ConfigDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YukaiLarkStateTransitionDiagram");

    private static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var config = JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
            config.RecentFiles = (config.RecentFiles ?? new List<string>())
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(AppConfig.MaxRecentFiles)
                .ToList();
            if (config.LastOpenedFile is not null && !File.Exists(config.LastOpenedFile))
            {
                config.LastOpenedFile = config.RecentFiles.FirstOrDefault();
            }
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, Options);
            File.WriteAllText(ConfigPath, json, Utf8NoBom);
        }
        catch
        {
            // 設定保存に失敗しても、図の保存・読込操作は成功扱いにする。
        }
    }
}