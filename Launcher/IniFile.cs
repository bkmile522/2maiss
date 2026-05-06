using System.IO;
using System.Collections.Generic;

namespace Project_2maiss_Launcher;

public class IniFile
{
    private string _path;
    private Dictionary<string, Dictionary<string, string>> _data = new();

    public IniFile(string path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_path)) return;
        
        string currentSection = "";
        foreach (var line in File.ReadAllLines(_path))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                currentSection = trimmed.Substring(1, trimmed.Length - 2);
                if (!_data.ContainsKey(currentSection)) 
                    _data[currentSection] = new Dictionary<string, string>();
            }
            else
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    if (!_data.ContainsKey(currentSection)) 
                        _data[currentSection] = new Dictionary<string, string>();
                    _data[currentSection][parts[0].Trim()] = parts[1].Trim();
                }
            }
        }
    }

    public string Read(string section, string key, string defaultValue = "")
    {
        if (_data.ContainsKey(section) && _data[section].ContainsKey(key))
            return _data[section][key];
        return defaultValue;
    }

    public void Write(string section, string key, string value)
    {
        if (!_data.ContainsKey(section)) 
            _data[section] = new Dictionary<string, string>();
            
        _data[section][key] = value;
        Save();
    }

    private void Save()
    {
        using var writer = new StreamWriter(_path);
        foreach (var section in _data)
        {
            writer.WriteLine($"[{section.Key}]");
            foreach (var kvp in section.Value)
            {
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
            }
            writer.WriteLine();
        }
    }
}