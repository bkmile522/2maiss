using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace Project_2maiss_Launcher;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;
    private readonly string _backendPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "2maiss_Backend.exe");

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        this.DataContext = _viewModel;
    }

    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9.]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        RegisterRawInput(hwnd);
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _viewModel.GetLang("DialogSelectScript"),
            Filter = _viewModel.GetLang("DialogFilter"),
            DefaultExt = ".bat"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _viewModel.GamePath = openFileDialog.FileName;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        System.Windows.MessageBox.Show(
            _viewModel.GetLang("MsgSaveSuccess"), 
            _viewModel.GetLang("MsgSaveSuccessTitle"), 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBind_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.BindingState = 1;
        _viewModel.BindStatus = _viewModel.GetLang("BindStep1");
    }

    private void KillBackendProcess()
    {
        try
        {
            Process[] existingProcesses = Process.GetProcessesByName("2maiss_Backend");
            foreach (var process in existingProcesses)
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    bool exited = process.WaitForExit(1500); 
                    if (!exited)
                    {
                        process.Kill();
                        process.WaitForExit(1000); 
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.NeedsBinding)
        {
            System.Windows.MessageBox.Show(
                _viewModel.GetLang("MsgNeedBind"), 
                _viewModel.GetLang("MsgNeedBindTitle"), 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.GamePath))
        {
            System.Windows.MessageBox.Show(
                _viewModel.GetLang("MsgNeedPath"), 
                _viewModel.GetLang("MsgNeedPathTitle"), 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _viewModel.SaveSettings();
        KillBackendProcess();

        if (File.Exists(_backendPath))
        {
            try 
            { 
                Process.Start(new ProcessStartInfo 
                { 
                    FileName = _backendPath, 
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, 
                    UseShellExecute = true 
                }); 
            }
            catch (Exception ex) 
            { 
                System.Windows.MessageBox.Show($"{_viewModel.GetLang("MsgStartFail")}{ex.Message}"); 
                return; 
            }
        }
        else 
        {
            System.Windows.MessageBox.Show(_viewModel.GetLang("MsgNoBackend"));
            return;
        }

        string batPath = _viewModel.GamePath;
        if (!string.IsNullOrWhiteSpace(batPath) && File.Exists(batPath))
        {
            try
            {
                Thread.Sleep(500); 
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WorkingDirectory = Path.GetDirectoryName(batPath), 
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"{_viewModel.GetLang("MsgBatFail")}{ex.Message}");
            }
        }

        this.WindowState = WindowState.Minimized;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_INPUT && _viewModel.BindingState > 0)
        {
            uint dataSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dataSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            if (dataSize > 0)
            {
                IntPtr rawData = Marshal.AllocHGlobal((int)dataSize);
                if (GetRawInputData(lParam, RID_INPUT, rawData, ref dataSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dataSize)
                {
                    RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(rawData);
                    if (header.dwType == RIM_TYPEMOUSE)
                    {
                        RAWMOUSE mouse = Marshal.PtrToStructure<RAWMOUSE>(new IntPtr(rawData.ToInt64() + Marshal.SizeOf(typeof(RAWINPUTHEADER))));
                        string deviceId = GetRawInputDeviceName(header.hDevice);

                        if (mouse.usButtonFlags > 0)
                        {
                            if (_viewModel.BindingState == 1)
                            {
                                _viewModel.DeviceRight = deviceId;
                                _viewModel.BindingState = 2;
                                _viewModel.BindStatus = _viewModel.GetLang("BindStep2");
                            }
                            else if (_viewModel.BindingState == 2 && deviceId != _viewModel.DeviceRight)
                            {
                                _viewModel.DeviceLeft = deviceId;
                                _viewModel.BindingState = 0;
                                _viewModel.BindStatus = _viewModel.GetLang("BindSuccess");
                                _viewModel.SaveSettings(); 
                                _viewModel.CheckBindingStatus();
                            }
                        }
                    }
                }
                Marshal.FreeHGlobal(rawData);
            }
        }
        return IntPtr.Zero;
    }

    private const int WM_INPUT = 0x00FF;
    private const int RID_INPUT = 0x10000003;
    private const int RIM_TYPEMOUSE = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }
    
    [StructLayout(LayoutKind.Explicit)]
    internal struct RAWMOUSE 
    { 
        [FieldOffset(0)] public ushort usFlags; 
        [FieldOffset(4)] public uint ulButtons; 
        [FieldOffset(4)] public ushort usButtonFlags; 
        [FieldOffset(6)] public ushort usButtonData; 
        [FieldOffset(8)] public uint ulRawButtons; 
        [FieldOffset(12)] public int lLastX; 
        [FieldOffset(16)] public int lLastY; 
        [FieldOffset(20)] public uint ulExtraInformation; 
    }

    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    private void RegisterRawInput(IntPtr hwnd)
    {
        RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
        rid[0].usUsagePage = 0x01; 
        rid[0].usUsage = 0x02; 
        rid[0].dwFlags = 0x00000100;
        rid[0].hwndTarget = hwnd;
        RegisterRawInputDevices(rid, 1, Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
    }

    private string GetRawInputDeviceName(IntPtr hDevice)
    {
        uint size = 0;
        GetRawInputDeviceInfo(hDevice, 0x20000007, IntPtr.Zero, ref size);
        if (size == 0) return "";
        int byteCount = (int)size * Marshal.SystemDefaultCharSize;
        IntPtr pName = Marshal.AllocHGlobal(byteCount);
        GetRawInputDeviceInfo(hDevice, 0x20000007, pName, ref size);
        string name = Marshal.PtrToStringAuto(pName) ?? "";
        Marshal.FreeHGlobal(pName);
        return name.Replace("\0", "");
    }
}