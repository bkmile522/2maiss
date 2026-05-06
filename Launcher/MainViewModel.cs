using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Collections.Generic;
using System.Reflection;

namespace Project_2maiss_Launcher;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

public class KeyOption
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class LanguageOption
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IniFile _ini;
    private readonly string _iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private Dictionary<string, string> _lang = new();
    public Dictionary<string, string> Lang
    {
        get => _lang;
        set { _lang = value; OnPropertyChanged(); }
    }

    public string GetLang(string key)
    {
        if (Lang == null) return key;
        return Lang.TryGetValue(key, out var val) ? val : key;
    }

    public List<LanguageOption> AvailableLanguages { get; } = new List<LanguageOption>
    {
        new LanguageOption { Name = "English", Code = "en-US" }
    };

    private string _selectedLanguage = "en-US";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                LoadLanguage(_selectedLanguage);
            }
        }
    }

    private string _gamePath = "";
    public string GamePath { get => _gamePath; set { _gamePath = value; OnPropertyChanged(); } }

    private string _port = "COM33";
    public string Port { get => _port; set { _port = value; OnPropertyChanged(); } }

    private string _baudRate = "115200";
    public string BaudRate { get => _baudRate; set { _baudRate = value; OnPropertyChanged(); } }

    private string _txIntervalMs = "2";
    public string TxIntervalMs { get => _txIntervalMs; set { _txIntervalMs = value; OnPropertyChanged(); } }

    private string _interceptKey = "119";
    public string InterceptKey { get => _interceptKey; set { _interceptKey = value; OnPropertyChanged(); } }

    public List<KeyOption> AvailableKeys { get; } = new List<KeyOption>
    {
        new KeyOption { Name = "F1", Code = "112" },
        new KeyOption { Name = "F2", Code = "113" },
        new KeyOption { Name = "F3", Code = "114" },
        new KeyOption { Name = "F4", Code = "115" },
        new KeyOption { Name = "F5", Code = "116" },
        new KeyOption { Name = "F6", Code = "117" },
        new KeyOption { Name = "F7", Code = "118" },
        new KeyOption { Name = "F8", Code = "119" },
        new KeyOption { Name = "F9", Code = "120" },
        new KeyOption { Name = "F10", Code = "121" },
        new KeyOption { Name = "F11", Code = "122" },
        new KeyOption { Name = "F12", Code = "123" },
        new KeyOption { Name = "Home", Code = "36" },
        new KeyOption { Name = "End", Code = "35" },
        new KeyOption { Name = "Insert", Code = "45" },
        new KeyOption { Name = "Delete", Code = "46" }
    };

    private double _limitRadius = 500.0;
    public double LimitRadius { get => _limitRadius; set { if (value < 0) value = 0; _limitRadius = value; OnPropertyChanged(); OnPropertyChanged(nameof(LimitRadiusText)); } }
    public string LimitRadiusText => string.Format(GetLang("PxFormat"), LimitRadius);

    private double _aoeRadius = 120.0;
    public double AoeRadius { get => _aoeRadius; set { if (value < 0) value = 0; _aoeRadius = value; OnPropertyChanged(); OnPropertyChanged(nameof(AoeRadiusText)); } }
    public string AoeRadiusText => string.Format(GetLang("PxFormat"), AoeRadius);

    private double _singleRadius = 30.0;
    public double SingleRadius { get => _singleRadius; set { if (value < 0) value = 0; _singleRadius = value; OnPropertyChanged(); OnPropertyChanged(nameof(SingleRadiusText)); } }
    public string SingleRadiusText => string.Format(GetLang("PxFormat"), SingleRadius);

    private bool _enhancePointer = true;
    public bool EnhancePointer { get => _enhancePointer; set { _enhancePointer = value; OnPropertyChanged(); } }

    private double _sensRight = 1.5;
    public double SensRight { get => _sensRight; set { if (value < 0) value = 0; _sensRight = value; OnPropertyChanged(); OnPropertyChanged(nameof(SensRightText)); } }
    public string SensRightText => string.Format(GetLang("SensRightFormat"), SensRight);

    private double _sensLeft = 1.5;
    public double SensLeft { get => _sensLeft; set { if (value < 0) value = 0; _sensLeft = value; OnPropertyChanged(); OnPropertyChanged(nameof(SensLeftText)); } }
    public string SensLeftText => string.Format(GetLang("SensLeftFormat"), SensLeft);

    public List<string> MouseActions { get; } = new List<string> { "Left", "Right", "Middle", "X1", "X2" };

    private string _actionSingleRight = "Left";
    public string ActionSingleRight { get => _actionSingleRight; set { _actionSingleRight = value; OnPropertyChanged(); } }

    private string _actionAoeRight = "Right";
    public string ActionAoeRight { get => _actionAoeRight; set { _actionAoeRight = value; OnPropertyChanged(); } }

    private string _actionSingleLeft = "Right";
    public string ActionSingleLeft { get => _actionSingleLeft; set { _actionSingleLeft = value; OnPropertyChanged(); } }

    private string _actionAoeLeft = "Left";
    public string ActionAoeLeft { get => _actionAoeLeft; set { _actionAoeLeft = value; OnPropertyChanged(); } }

    private string _bindStatus = "Ready.";
    public string BindStatus { get => _bindStatus; set { _bindStatus = value; OnPropertyChanged(); } }
    
    public int BindingState { get; set; } = 0; 
    public string DeviceRight { get; set; } = "";
    public string DeviceLeft { get; set; } = "";

    private bool _needsBinding;
    public bool NeedsBinding { get => _needsBinding; set { _needsBinding = value; OnPropertyChanged(); } }

    public MainViewModel()
    {
        _ini = new IniFile(_iniPath);
        LoadSettings();
        LoadLanguage(_selectedLanguage);
    }

    private void LoadLanguage(string langCode)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();
            string? targetResourceName = System.Linq.Enumerable.FirstOrDefault(names, n => n.EndsWith($"{langCode}.json"));

            if (targetResourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(targetResourceName);
                if (stream != null)
                {
                    using StreamReader reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    Lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            else
            {
                Lang = new Dictionary<string, string>();
            }
        }
        catch
        {
            Lang = new Dictionary<string, string>();
        }

        BindStatus = GetLang("BindReady");
        
        OnPropertyChanged(nameof(LimitRadiusText));
        OnPropertyChanged(nameof(AoeRadiusText));
        OnPropertyChanged(nameof(SingleRadiusText));
        OnPropertyChanged(nameof(SensRightText));
        OnPropertyChanged(nameof(SensLeftText));
    }

    public void CheckBindingStatus()
    {
        bool iniExists = File.Exists(_iniPath);
        bool missingDevices = string.IsNullOrWhiteSpace(DeviceRight) || string.IsNullOrWhiteSpace(DeviceLeft);
        NeedsBinding = !iniExists || missingDevices;
    }

    public void LoadSettings()
    {
        _selectedLanguage = _ini.Read("system", "language", "en-US");
        OnPropertyChanged(nameof(SelectedLanguage));

        GamePath = _ini.Read("launcher", "gamepath", "");
        TxIntervalMs = _ini.Read("hardware", "txintervalms", "2");
        BaudRate = _ini.Read("hardware", "baudrate", "115200");
        DeviceRight = _ini.Read("hardware", "deviceright", "");
        DeviceLeft = _ini.Read("hardware", "deviceleft", "");
        InterceptKey = _ini.Read("system", "interceptkey", "119");
        
        string rawPort = _ini.Read("hardware", "port", @"\\.\COM33");
        Port = rawPort.StartsWith(@"\\.\") ? rawPort.Substring(4) : rawPort;
        
        if (double.TryParse(_ini.Read("geometry", "limitradius", "500.0"), out double limit)) LimitRadius = limit;
        if (double.TryParse(_ini.Read("gameplay", "aoeradius", "120.0"), out double aoe)) AoeRadius = aoe;
        if (double.TryParse(_ini.Read("gameplay", "singleradius", "30.0"), out double singleRad)) SingleRadius = singleRad;
        EnhancePointer = _ini.Read("gameplay", "enhancepointerprecision", "1") == "1";
        
        if (double.TryParse(_ini.Read("controls_right", "sensitivity", "1.5"), out double sr)) SensRight = sr;
        if (double.TryParse(_ini.Read("controls_left", "sensitivity", "1.5"), out double sl)) SensLeft = sl;

        ActionSingleRight = _ini.Read("controls_right", "actionsingle", "Left");
        ActionAoeRight = _ini.Read("controls_right", "actionaoe", "Right");
        ActionSingleLeft = _ini.Read("controls_left", "actionsingle", "Right");
        ActionAoeLeft = _ini.Read("controls_left", "actionaoe", "Left");

        CheckBindingStatus();
    }

    public void SaveSettings()
    {
        _ini.Write("system", "language", SelectedLanguage);

        _ini.Write("launcher", "gamepath", GamePath);
        _ini.Write("hardware", "txintervalms", TxIntervalMs);
        _ini.Write("hardware", "baudrate", BaudRate);
        _ini.Write("hardware", "deviceright", DeviceRight);
        _ini.Write("hardware", "deviceleft", DeviceLeft);

        string safePortText = Port?.Trim() ?? "COM33";
        string formattedPort = safePortText.StartsWith(@"\\.\") ? safePortText : $@"\\.\{safePortText}";
        _ini.Write("hardware", "port", formattedPort);
        
        _ini.Write("system", "interceptkey", InterceptKey);
        _ini.Write("geometry", "limitradius", LimitRadius.ToString("0.000000"));
        _ini.Write("gameplay", "aoeradius", AoeRadius.ToString("0.000000"));
        _ini.Write("gameplay", "singleradius", SingleRadius.ToString("0.000000"));
        _ini.Write("gameplay", "enhancepointerprecision", EnhancePointer ? "1" : "0");
        
        _ini.Write("controls_right", "sensitivity", SensRight.ToString("0.000000"));
        _ini.Write("controls_right", "offsetx", "0.000000");
        _ini.Write("controls_right", "offsety", "0.000000");
        _ini.Write("controls_right", "actionsingle", ActionSingleRight);  
        _ini.Write("controls_right", "actionaoe", ActionAoeRight);    
        
        _ini.Write("controls_left", "sensitivity", SensLeft.ToString("0.000000"));
        _ini.Write("controls_left", "offsetx", "0.000000");
        _ini.Write("controls_left", "offsety", "0.000000");
        _ini.Write("controls_left", "actionsingle", ActionSingleLeft);  
        _ini.Write("controls_left", "actionaoe", ActionAoeLeft);      
    }
}