/*************************************************************************
 * Author: bkmile522
 * GitHub: https://https://github.com/bkmile522
 * Description: 2maiss Backend
 ************************************************************************/
#include <iostream>
#include <winsock2.h>
#include <windows.h>
#pragma comment(lib, "ws2_32.lib")
#include <mmsystem.h>
#pragma comment(lib, "winmm.lib")

#include <cmath>
#include <string>
#include <unordered_map>
#include <set>
#include <vector>
#include <chrono>
#include <atomic>
#include <thread>
#include <mutex>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <shellapi.h>
#include "resource.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "ini.h"

std::atomic<bool> g_isRunning{true};
std::atomic<bool> isIntercepting{false};
std::atomic<bool> isGameHalted{true};    

HWND g_hWnd = nullptr;
#define WM_APP_RELOAD_HOTKEY (WM_USER + 2)
#define WM_APP_RELOAD_CONFIG (WM_USER + 3)

std::string cfgPort = "\\\\.\\COM33";
int cfgBaudRate = 115200;
std::atomic<int> cfgTxIntervalMs{2}; 

std::string cfgDeviceR = ""; 
std::string cfgDeviceL = ""; 

float cfgLogicalWidth = 1920.0f;
float cfgLogicalHeight = 1080.0f;
float cfgLimitRadius = 500.0f;
float cfgAoERadius = 120.0f;
float cfgSingleRadius = 60.0f;
bool cfgEnhancePointerPrecision = true;

int cfgInterceptKey = 119; 
float cfgSensitivityR = 1.5f;
float cfgSensitivityL = 1.5f;
float cfgOffsetX_R = 0.0f;
float cfgOffsetY_R = 0.0f;
float cfgOffsetX_L = 0.0f;
float cfgOffsetY_L = 0.0f;

enum class MouseAction { None, Left, Right, Middle, X1, X2 };
MouseAction cfgActionSingleR = MouseAction::Left;
MouseAction cfgActionAoER = MouseAction::Right;
MouseAction cfgActionSingleL = MouseAction::Right;
MouseAction cfgActionAoEL = MouseAction::Left;

MouseAction ParseAction(const std::string& str) {
    if (str == "Right") return MouseAction::Right;
    if (str == "Middle") return MouseAction::Middle;
    if (str == "X1") return MouseAction::X1;
    if (str == "X2") return MouseAction::X2;
    return MouseAction::Left;
}

std::string ActionToStr(MouseAction act) {
    switch (act) {
        case MouseAction::Right: return "Right";
        case MouseAction::Middle: return "Middle";
        case MouseAction::X1: return "X1";
        case MouseAction::X2: return "X2";
        default: return "Left";
    }
}

struct MouseState {
    bool left = false, right = false, middle = false, x1 = false, x2 = false;
    float x = 1920.0f / 2.0f;
    float y = 1080.0f / 2.0f;
    uint64_t activeMask = 0;

    bool IsDown(MouseAction action) const {
        switch(action) {
            case MouseAction::Left: return left;
            case MouseAction::Right: return right;
            case MouseAction::Middle: return middle;
            case MouseAction::X1: return x1;
            case MouseAction::X2: return x2;
            default: return false;
        }
    }
};

MouseState mStateR;
MouseState mStateL;

HANDLE g_hMouseR = nullptr; 
HANDLE g_hMouseL = nullptr; 
std::atomic<uint64_t> touchStateMask{0}; 

struct PointOffset {
    float dx;
    float dy;
};
std::vector<PointOffset> g_aoeOffsets;
std::vector<PointOffset> g_singleOffsets;

void InitAoEOffsets() {
    g_aoeOffsets.clear();
    g_aoeOffsets.push_back({0.0f, 0.0f}); 

    auto addRing = [](float r, int count) {
        if (r <= 0) return;
        float step = 2.0f * 3.14159265f / count;
        for (int i = 0; i < count; ++i) {
            g_aoeOffsets.push_back({r * std::cos(i * step), r * std::sin(i * step)});
        }
    };

    addRing(cfgAoERadius, 32);
    addRing(cfgAoERadius * 0.66f, 24);
    addRing(cfgAoERadius * 0.33f, 12);
}

void InitSingleOffsets() {
    g_singleOffsets.clear();
    g_singleOffsets.push_back({0.0f, 0.0f}); 

    auto addRing = [](float r, int count) {
        if (r <= 0) return;
        float step = 2.0f * 3.14159265f / count;
        for (int i = 0; i < count; ++i) {
            g_singleOffsets.push_back({r * std::cos(i * step), r * std::sin(i * step)});
        }
    };

    addRing(cfgSingleRadius, 32);
    addRing(cfgSingleRadius * 0.66f, 24);
    addRing(cfgSingleRadius * 0.33f, 12);
}

void WriteLog(const std::string& msg) {
    static std::ofstream logFile("2maiss.log", std::ios::app);
    if (!logFile.is_open()) return;
    auto now = std::chrono::system_clock::now();
    auto time = std::chrono::system_clock::to_time_t(now);
    logFile << std::put_time(std::localtime(&time), "[%H:%M:%S] ") << msg << std::endl;
    logFile.flush();
}

SOCKET g_udpSocket;
sockaddr_in g_destAddr;
std::recursive_mutex g_serialMutex; 
HANDLE hSerial = INVALID_HANDLE_VALUE;
char serialRecvBuffer[16];
int serialRecvLen = 0;

void InitUDPClient() {
    WSADATA wsaData; WSAStartup(MAKEWORD(2, 2), &wsaData);
    g_udpSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    g_destAddr.sin_family = AF_INET; g_destAddr.sin_port = htons(52222); g_destAddr.sin_addr.s_addr = inet_addr("127.0.0.1");
}

void InitSerialPort() {
    std::lock_guard<std::recursive_mutex> lock(g_serialMutex);
    if (hSerial != INVALID_HANDLE_VALUE) { CloseHandle(hSerial); hSerial = INVALID_HANDLE_VALUE; }
    hSerial = CreateFileA(cfgPort.c_str(), GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hSerial == INVALID_HANDLE_VALUE) return;
    DCB dcb = {0}; dcb.DCBlength = sizeof(dcb); GetCommState(hSerial, &dcb);
    dcb.BaudRate = cfgBaudRate; dcb.ByteSize = 8; dcb.StopBits = ONESTOPBIT; dcb.Parity = NOPARITY; SetCommState(hSerial, &dcb);
    COMMTIMEOUTS timeouts = {0}; timeouts.ReadIntervalTimeout = MAXDWORD; timeouts.WriteTotalTimeoutConstant = 10;
    SetCommTimeouts(hSerial, &timeouts);
}

void SendTouchPacket() {
    if (isGameHalted.load()) return;
    std::lock_guard<std::recursive_mutex> lock(g_serialMutex);
    if (hSerial == INVALID_HANDLE_VALUE) return;
    char packet[9]; packet[0] = '(';
    uint64_t temp = touchStateMask.load(); 
    for (int i = 0; i < 7; i++) { packet[i + 1] = (char)(temp & 0x1F); temp >>= 5; }
    packet[8] = ')';
    DWORD bw; WriteFile(hSerial, packet, 9, &bw, NULL);
}

void ProcessSerialInput() {
    std::lock_guard<std::recursive_mutex> lock(g_serialMutex);
    if (hSerial == INVALID_HANDLE_VALUE) return;
    char buffer[64]; DWORD bytesRead;
    if (ReadFile(hSerial, buffer, sizeof(buffer), &bytesRead, NULL) && bytesRead > 0) {
        for (DWORD i = 0; i < bytesRead; i++) {
            char r = buffer[i]; if (r == '{') serialRecvLen = 0;
            serialRecvBuffer[serialRecvLen++] = r;
            if (r == '}') {
                if (serialRecvLen == 6) { 
                    char cmd = serialRecvBuffer[3];
                    if (cmd == 0x4C) isGameHalted.store(true);
                    else if (cmd == 0x41) { isGameHalted.store(false); SendTouchPacket(); }
                    else if (cmd == 0x72 || cmd == 0x6B) {           
                        char reply[6] = { '(', serialRecvBuffer[1], serialRecvBuffer[2], serialRecvBuffer[3], serialRecvBuffer[4], ')' };
                        DWORD bw; WriteFile(hSerial, reply, 6, &bw, NULL);
                    }
                }
                serialRecvLen = 0;
            }
        }
    }
}

void IOThreadFunc() {
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_TIME_CRITICAL);
    
    auto interval = std::chrono::milliseconds(cfgTxIntervalMs.load());
    auto nextSendTime = std::chrono::high_resolution_clock::now() + interval;

    while (g_isRunning.load()) {
        ProcessSerialInput();
        auto now = std::chrono::high_resolution_clock::now();
        
        if (now >= nextSendTime) {
            SendTouchPacket();
            nextSendTime += interval;
            if (now > nextSendTime) {
                nextSendTime = now + interval;
            }
        }

        now = std::chrono::high_resolution_clock::now();
        auto timeToWait = nextSendTime - now;
        
        if (timeToWait > std::chrono::milliseconds(1)) {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        } else if (timeToWait > std::chrono::nanoseconds(0)) {
            std::this_thread::yield();
        }
    }
}

mINI::INIFile configFile("config.ini");
mINI::INIStructure ini;

void LoadConfig() {
    if (configFile.read(ini)) {
        std::string oldPort = cfgPort; int oldBaud = cfgBaudRate; int oldKey = cfgInterceptKey;

        if (ini["hardware"].has("port")) cfgPort = ini["hardware"]["port"];
        if (ini["hardware"].has("baudrate")) cfgBaudRate = std::stoi(ini["hardware"]["baudrate"]);
        if (ini["hardware"].has("txintervalms")) cfgTxIntervalMs.store(std::stoi(ini["hardware"]["txintervalms"]));
        if (ini["hardware"].has("deviceright")) cfgDeviceR = ini["hardware"]["deviceright"];
        if (ini["hardware"].has("deviceleft")) cfgDeviceL = ini["hardware"]["deviceleft"];
        
        if (ini["system"].has("interceptkey")) cfgInterceptKey = std::stoi(ini["system"]["interceptkey"]);

        if (ini["geometry"].has("limitradius")) cfgLimitRadius = std::stof(ini["geometry"]["limitradius"]);
        if (ini["gameplay"].has("aoeradius")) cfgAoERadius = std::stof(ini["gameplay"]["aoeradius"]);
        if (ini["gameplay"].has("singleradius")) cfgSingleRadius = std::stof(ini["gameplay"]["singleradius"]);
        if (ini["gameplay"].has("enhancepointerprecision")) cfgEnhancePointerPrecision = (ini["gameplay"]["enhancepointerprecision"] == "1");

        if (ini["controls_right"].has("sensitivity")) cfgSensitivityR = std::stof(ini["controls_right"]["sensitivity"]);
        if (ini["controls_right"].has("offsetx")) cfgOffsetX_R = std::stof(ini["controls_right"]["offsetx"]);
        if (ini["controls_right"].has("offsety")) cfgOffsetY_R = std::stof(ini["controls_right"]["offsety"]);
        if (ini["controls_right"].has("actionsingle")) cfgActionSingleR = ParseAction(ini["controls_right"]["actionsingle"]);
        if (ini["controls_right"].has("actionaoe")) cfgActionAoER = ParseAction(ini["controls_right"]["actionaoe"]);

        if (ini["controls_left"].has("sensitivity")) cfgSensitivityL = std::stof(ini["controls_left"]["sensitivity"]);
        if (ini["controls_left"].has("offsetx")) cfgOffsetX_L = std::stof(ini["controls_left"]["offsetx"]);
        if (ini["controls_left"].has("offsety")) cfgOffsetY_L = std::stof(ini["controls_left"]["offsety"]);
        if (ini["controls_left"].has("actionsingle")) cfgActionSingleL = ParseAction(ini["controls_left"]["actionsingle"]);
        if (ini["controls_left"].has("actionaoe")) cfgActionAoEL = ParseAction(ini["controls_left"]["actionaoe"]);

        if (oldPort != cfgPort || oldBaud != cfgBaudRate) InitSerialPort();
        
        if (oldKey != cfgInterceptKey && g_hWnd) {
            PostMessage(g_hWnd, WM_APP_RELOAD_HOTKEY, 0, 0);
        }
        
        g_hMouseR = nullptr; g_hMouseL = nullptr;
        
        mStateR.x = (cfgLogicalWidth / 2.0f) + cfgOffsetX_R; mStateR.y = (cfgLogicalHeight / 2.0f) + cfgOffsetY_R;
        mStateL.x = (cfgLogicalWidth / 2.0f) + cfgOffsetX_L; mStateL.y = (cfgLogicalHeight / 2.0f) + cfgOffsetY_L;
        
    } else {
        ini["hardware"]["port"] = cfgPort; 
        ini["hardware"]["baudrate"] = std::to_string(cfgBaudRate);
        ini["hardware"]["txintervalms"] = std::to_string(cfgTxIntervalMs.load());
        ini["hardware"]["deviceright"] = ""; 
        ini["hardware"]["deviceleft"] = "";
        
        ini["system"]["interceptkey"] = std::to_string(cfgInterceptKey);
        ini["geometry"]["limitradius"] = std::to_string(cfgLimitRadius) + "0000"; 
        ini["gameplay"]["aoeradius"] = std::to_string(cfgAoERadius) + "0000";
        ini["gameplay"]["singleradius"] = std::to_string(cfgSingleRadius) + "0000";
        ini["gameplay"]["enhancepointerprecision"] = cfgEnhancePointerPrecision ? "1" : "0";

        ini["controls_right"]["sensitivity"] = std::to_string(cfgSensitivityR);
        ini["controls_right"]["offsetx"] = std::to_string(cfgOffsetX_R) + "0000";
        ini["controls_right"]["offsety"] = std::to_string(cfgOffsetY_R) + "0000";
        ini["controls_right"]["actionsingle"] = ActionToStr(cfgActionSingleR);
        ini["controls_right"]["actionaoe"] = ActionToStr(cfgActionAoER);

        ini["controls_left"]["sensitivity"] = std::to_string(cfgSensitivityL);
        ini["controls_left"]["offsetx"] = std::to_string(cfgOffsetX_L) + "0000";
        ini["controls_left"]["offsety"] = std::to_string(cfgOffsetY_L) + "0000";
        ini["controls_left"]["actionsingle"] = ActionToStr(cfgActionSingleL);
        ini["controls_left"]["actionaoe"] = ActionToStr(cfgActionAoEL);

        (void)configFile.generate(ini);
        WriteLog("Created default config.ini.");
    }
    
    InitAoEOffsets();
    InitSingleOffsets();
}

void ConfigWatcherThread() {
    std::filesystem::file_time_type lastWrite;
    try { lastWrite = std::filesystem::last_write_time("config.ini"); } catch(...) {}
    while (g_isRunning.load()) {
        for (int i = 0; i < 20 && g_isRunning.load(); ++i) std::this_thread::sleep_for(std::chrono::milliseconds(100));
        if (!g_isRunning.load()) break;
        try {
            auto currentWrite = std::filesystem::last_write_time("config.ini");
            if (currentWrite > lastWrite) { 
                lastWrite = currentWrite; 
                if (g_hWnd) PostMessage(g_hWnd, WM_APP_RELOAD_CONFIG, 0, 0); 
            }
        } catch(...) {}
    }
}

unsigned char* lutData = nullptr; int lutWidth = 0, lutHeight = 0, lutChannels = 0;

inline uint32_t RGB2INT(uint8_t r, uint8_t g, uint8_t b) { return (r << 16) | (g << 8) | b; }

std::unordered_map<uint32_t, uint64_t> colorToMaskMap = {
    {RGB2INT(12, 242, 120), 1ULL << 17}, 
    {RGB2INT(12, 242, 161), 1ULL << 16}, 
    {RGB2INT(242, 93, 12), 1ULL << 0},  
    {RGB2INT(188, 242, 12), 1ULL << 1},
    {RGB2INT(25, 12, 242), 1ULL << 2},  
    {RGB2INT(242, 12, 93), 1ULL << 3}, 
    {RGB2INT(242, 12, 133), 1ULL << 4}, 
    {RGB2INT(12, 39, 242), 1ULL << 5},
    {RGB2INT(228, 242, 12), 1ULL << 6}, 
    {RGB2INT(242, 52, 12), 1ULL << 7}, 
    {RGB2INT(242, 12, 12), 1ULL << 18},  
    {RGB2INT(242, 174, 12), 1ULL << 19},
    {RGB2INT(12, 161, 242), 1ULL << 20}, 
    {RGB2INT(228, 12, 242), 1ULL << 21}, 
    {RGB2INT(242, 12, 52), 1ULL << 22},  
    {RGB2INT(242, 12, 215), 1ULL << 23},
    {RGB2INT(12, 201, 242), 1ULL << 24}, 
    {RGB2INT(242, 133, 12), 1ULL << 25}, 
    {RGB2INT(242, 215, 12), 1ULL << 26}, 
    {RGB2INT(25, 242, 12), 1ULL << 27},
    {RGB2INT(12, 242, 242), 1ULL << 28}, 
    {RGB2INT(188, 12, 242), 1ULL << 29}, 
    {RGB2INT(242, 12, 174), 1ULL << 30}, 
    {RGB2INT(147, 12, 242), 1ULL << 31},
    {RGB2INT(12, 242, 201), 1ULL << 32}, 
    {RGB2INT(66, 242, 12), 1ULL << 33}, 
    {RGB2INT(106, 242, 12), 1ULL << 15}, 
    {RGB2INT(147, 242, 12), 1ULL << 8},
    {RGB2INT(12, 242, 79), 1ULL << 9},  
    {RGB2INT(12, 79, 242), 1ULL << 10}, 
    {RGB2INT(106, 12, 242), 1ULL << 11}, 
    {RGB2INT(66, 12, 242), 1ULL << 12},
    {RGB2INT(12, 120, 242), 1ULL << 13}, 
    {RGB2INT(12, 242, 39), 1ULL << 14}
};

uint64_t GetMaskFromLUT(float x, float y) {
    if (!lutData) return 0;
    int ix = (int)x, iy = (int)y;
    if (ix < 0 || ix >= lutWidth || iy < 0 || iy >= lutHeight) return 0;
    int index = (iy * lutWidth + ix) * 4; 
    uint32_t color = RGB2INT(lutData[index], lutData[index + 1], lutData[index + 2]);
    auto it = colorToMaskMap.find(color);
    if (it != colorToMaskMap.end()) return it->second;
    return 0;
}

uint64_t GetMasksWithAoE(float x, float y) {
    uint64_t mask = 0;
    for (const auto& offset : g_aoeOffsets) {
        mask |= GetMaskFromLUT(x + offset.dx, y + offset.dy);
    }
    return mask;
}

uint64_t GetMasksWithSingle(float x, float y) {
    uint64_t mask = 0;
    for (const auto& offset : g_singleOffsets) {
        mask |= GetMaskFromLUT(x + offset.dx, y + offset.dy);
    }
    return mask;
}

void UpdateVirtualMousePos(MouseState& state, LONG dx, LONG dy, float sens) {
    float fdx = (float)dx * sens; 
    float fdy = (float)dy * sens;
    if (cfgEnhancePointerPrecision) {
        float speed = std::sqrt(fdx*fdx + fdy*fdy);
        float accel = 1.0f + (speed / 15.0f); fdx *= accel; fdy *= accel;
    }
    state.x += fdx; state.y += fdy;
    
    float centerX = cfgLogicalWidth / 2.0f;
    float centerY = cfgLogicalHeight / 2.0f;
    
    float diffX = state.x - centerX, diffY = state.y - centerY;
    float dist = sqrt(diffX * diffX + diffY * diffY);
    if (dist > cfgLimitRadius) {
        state.x = centerX + (diffX / dist) * cfgLimitRadius;
        state.y = centerY + (diffY / dist) * cfgLimitRadius;
    }
}

HHOOK g_hMouseHook = nullptr;

LRESULT CALLBACK LLMouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION && isIntercepting.load()) {
        MSLLHOOKSTRUCT* pMouseStruct = (MSLLHOOKSTRUCT*)lParam;
        if ((pMouseStruct->flags & LLMHF_INJECTED) != 0 || 
            (pMouseStruct->flags & LLMHF_LOWER_IL_INJECTED) != 0) {
            return CallNextHookEx(g_hMouseHook, nCode, wParam, lParam);
        }
        return 1; 
    }
    return CallNextHookEx(g_hMouseHook, nCode, wParam, lParam);
}

void ToggleInterceptMode() {
    isIntercepting = !isIntercepting;
    if (isIntercepting) {
        SetCursorPos(99999, 99999); 
        POINT pt; GetCursorPos(&pt);
        RECT rect = { pt.x, pt.y, pt.x + 1, pt.y + 1 };
        ClipCursor(&rect);
        WriteLog("Intercept Toggled ON - Cursor Clipped to Bottom-Right");
    } else {
        ClipCursor(NULL);
        WriteLog("Intercept Toggled OFF - Cursor Freed");
    }
}

std::string GetDeviceHardwareID(HANDLE hDevice) {
    if (!hDevice) return "";
    UINT size = 0; GetRawInputDeviceInfoA(hDevice, RIDI_DEVICENAME, nullptr, &size);
    if (size == 0) return "";
    std::string name(size, '\0');
    if (GetRawInputDeviceInfoA(hDevice, RIDI_DEVICENAME, &name[0], &size) > 0) {
        name.erase(std::find(name.begin(), name.end(), '\0'), name.end());
        return name;
    }
    return "";
}

#define WM_APP_TRAYMSG (WM_USER + 1)
NOTIFYICONDATAW nid = {};
HMENU hTrayMenu = nullptr;

LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_APP_TRAYMSG:
        if (lParam == WM_RBUTTONUP || lParam == WM_CONTEXTMENU) {
            POINT pt; GetCursorPos(&pt); SetForegroundWindow(hWnd); 
            TrackPopupMenu(hTrayMenu, TPM_BOTTOMALIGN | TPM_LEFTALIGN, pt.x, pt.y, 0, hWnd, NULL);
        }
        return 0;
    case WM_COMMAND: if (LOWORD(wParam) == 1002) DestroyWindow(hWnd); return 0;
    case WM_CLOSE: DestroyWindow(hWnd); return 0;
    case WM_DESTROY: Shell_NotifyIconW(NIM_DELETE, &nid); PostQuitMessage(0); return 0;
    
    case WM_APP_RELOAD_CONFIG:
        LoadConfig();
        WriteLog("Config File modified - Safe Reload Triggered in Main Thread.");
        return 0;

    case WM_APP_RELOAD_HOTKEY:
        UnregisterHotKey(hWnd, 1);
        RegisterHotKey(hWnd, 1, 0, cfgInterceptKey);
        WriteLog("HotKey re-registered to VK Code: " + std::to_string(cfgInterceptKey));
        return 0;

    case WM_HOTKEY:
        if (wParam == 1) ToggleInterceptMode(); 
        return 0;
        
    case WM_INPUT:
        {
            UINT sz; GetRawInputData((HRAWINPUT)lParam, RID_INPUT, NULL, &sz, sizeof(RAWINPUTHEADER));
            if (sz == 0) break;
            std::vector<BYTE> buf(sz);
            if (GetRawInputData((HRAWINPUT)lParam, RID_INPUT, buf.data(), &sz, sizeof(RAWINPUTHEADER)) == sz) {
                RAWINPUT* raw = (RAWINPUT*)buf.data();
                if (raw->header.dwType == RIM_TYPEMOUSE) {
                    auto& m = raw->data.mouse; HANDLE h = raw->header.hDevice; 
                    std::string currentID = GetDeviceHardwareID(h);
                    
                    if (!g_hMouseR && currentID == cfgDeviceR && !cfgDeviceR.empty()) g_hMouseR = h;
                    if (!g_hMouseL && currentID == cfgDeviceL && !cfgDeviceL.empty()) g_hMouseL = h;
                    
                    if (isIntercepting.load() && (h == g_hMouseR || h == g_hMouseL)) {

                        SetCursorPos(99999, 99999);
                        
                        bool isRight = (h == g_hMouseR);
                        MouseState& state = isRight ? mStateR : mStateL;
                        float sens = isRight ? cfgSensitivityR : cfgSensitivityL;
                        
                        UpdateVirtualMousePos(state, m.lLastX, m.lLastY, sens);
                        
                        if (m.usButtonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) state.left = true;
                        if (m.usButtonFlags & RI_MOUSE_LEFT_BUTTON_UP) state.left = false;
                        if (m.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) state.right = true;
                        if (m.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_UP) state.right = false;
                        if (m.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_DOWN) state.middle = true;
                        if (m.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_UP) state.middle = false;
                        if (m.usButtonFlags & RI_MOUSE_BUTTON_4_DOWN) state.x1 = true; 
                        if (m.usButtonFlags & RI_MOUSE_BUTTON_4_UP) state.x1 = false;
                        if (m.usButtonFlags & RI_MOUSE_BUTTON_5_DOWN) state.x2 = true; 
                        if (m.usButtonFlags & RI_MOUSE_BUTTON_5_UP) state.x2 = false;

                        MouseAction actionAoE = isRight ? cfgActionAoER : cfgActionAoEL;
                        MouseAction actionSingle = isRight ? cfgActionSingleR : cfgActionSingleL;
                        
                        if (state.IsDown(actionAoE)) {
                            state.activeMask = GetMasksWithAoE(state.x, state.y);
                        } else if (state.IsDown(actionSingle)) { 
                            state.activeMask = GetMasksWithSingle(state.x, state.y);
                        } else {
                            state.activeMask = 0;
                        }
                        
                        touchStateMask.store(mStateR.activeMask | mStateL.activeMask);
                    }
                }
            }
        }
        break;
    }
    return DefWindowProc(hWnd, msg, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow) {
    WriteLog("=== 2maiss Backend Started ===");
    timeBeginPeriod(1); 
    
    WNDCLASSEXW wc = { sizeof(wc), 0, WndProc, 0, 0, hInstance, NULL, NULL, NULL, NULL, L"2maissBackend", NULL };
    RegisterClassExW(&wc);
    g_hWnd = CreateWindowExW(0, wc.lpszClassName, L"2maiss Headless", 0, 0, 0, 0, 0, HWND_MESSAGE, NULL, hInstance, NULL);

    LoadConfig(); 
    InitUDPClient();
    InitSerialPort();

    lutData = stbi_load("lut.png", &lutWidth, &lutHeight, &lutChannels, 4);
    if (!lutData) WriteLog("WARNING: lut.png not found or failed to load!");

    memset(&nid, 0, sizeof(nid));
    nid.cbSize = sizeof(NOTIFYICONDATAW); nid.hWnd = g_hWnd; nid.uID = 1; nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_APP_TRAYMSG; nid.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_APP_ICON));
    lstrcpyW(nid.szTip, L"2maiss Backend");
    Shell_NotifyIconW(NIM_ADD, &nid);

    hTrayMenu = CreatePopupMenu();
    AppendMenuW(hTrayMenu, MF_STRING, 1002, L"Exit 2maiss Backend");

    RegisterHotKey(g_hWnd, 1, 0, cfgInterceptKey); 
    g_hMouseHook = SetWindowsHookEx(WH_MOUSE_LL, LLMouseHookProc, GetModuleHandle(nullptr), 0);
    
    RAWINPUTDEVICE Rid = { 0x01, 0x02, RIDEV_INPUTSINK, g_hWnd }; 
    RegisterRawInputDevices(&Rid, 1, sizeof(Rid));

    std::thread ioThread(IOThreadFunc);
    std::thread configThread(ConfigWatcherThread);

    MSG msg; 
    while (GetMessage(&msg, nullptr, 0, 0)) { 
        TranslateMessage(&msg); DispatchMessage(&msg); 

        struct UDPPacket { float pRx, pRy, pLx, pLy; } packet = { mStateR.x, mStateR.y, mStateL.x, mStateL.y };
        sendto(g_udpSocket, (const char*)&packet, sizeof(packet), 0, (sockaddr*)&g_destAddr, sizeof(g_destAddr));
    }

    g_isRunning.store(false); 
    if (g_hMouseHook) { UnhookWindowsHookEx(g_hMouseHook); g_hMouseHook = nullptr; }
    if (ioThread.joinable()) ioThread.join(); 
    if (configThread.joinable()) configThread.join();
    
    {
        std::lock_guard<std::recursive_mutex> lock(g_serialMutex);
        if (hSerial != INVALID_HANDLE_VALUE) { CloseHandle(hSerial); hSerial = INVALID_HANDLE_VALUE; }
    }
    
    ClipCursor(NULL);
    
    if (lutData) stbi_image_free(lutData);
    closesocket(g_udpSocket); WSACleanup(); timeEndPeriod(1); 
    WriteLog("=== 2maiss Backend Exited ===");
    return 0; 
}