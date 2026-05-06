using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Project_2maiss.MaissMod), "2maiss Frontend", "0.9.0", "bkmile522")]
[assembly: MelonGame(null, null)] 

namespace Project_2maiss
{
    public class MaissMod : MelonMod
    {
        private Socket udpSocket;
        private Thread listenThread;
        private volatile bool isRunning = true; 
        private static float p1X = 960, p1Y = 540, p2X = 960, p2Y = 540; 

        private Texture2D rightCursorTexture; 
        private Texture2D leftCursorTexture;  
        private Texture2D lutTexture; 
        private const float CURSOR_SIZE_X = 38f;
        private const float CURSOR_SIZE_Y = 53f;
        private readonly Color mintGreen = new Color(0.6f, 0.95f, 0.75f);
        private readonly Color pastelBlue = new Color(0.424f, 0.812f, 0.965f);

        private bool isCalibrationMode = false;
        private float offsetX = 0f;
        private float offsetY = 0f;
        private float scaleMult = 1f;

        private readonly float[] moveSteps = { 0.1f, 0.5f, 1.0f, 5.0f, 10.0f };
        private readonly float[] scaleSteps = { 0.0001f, 0.0005f, 0.001f, 0.005f, 0.01f };
        private int currentStepIndex = 2; 

        private string DisplayConfigPath => Path.Combine(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "2maiss"), "display.ini");

        public override void OnInitializeMelon()
        {
            LoadDisplayConfig();
            LoadTextures();

            listenThread = new Thread(ListenForUDP) { IsBackground = true };
            listenThread.Start();
        }

        private void LoadDisplayConfig()
        {
            if (File.Exists(DisplayConfigPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(DisplayConfigPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();
                        if (key == "OffsetX") float.TryParse(val, out offsetX);
                        if (key == "OffsetY") float.TryParse(val, out offsetY);
                        if (key == "ScaleMult") float.TryParse(val, out scaleMult);
                    }
                }
                catch (Exception) { }
            }
        }

        private void SaveDisplayConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(DisplayConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string content = "OffsetX=" + offsetX.ToString("0.00") + "\n" +
                                 "OffsetY=" + offsetY.ToString("0.00") + "\n" +
                                 "ScaleMult=" + scaleMult.ToString("0.0000");
                File.WriteAllText(DisplayConfigPath, content);
            }
            catch (Exception) { }
        }

        private void LoadTextures()
        {
            string folderPath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "2maiss");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            
            string rightPath = Path.Combine(folderPath, "right.png");
            string leftPath = Path.Combine(folderPath, "left.png");
            string lutPath = Path.Combine(folderPath, "lut.png"); 

            if (File.Exists(rightPath)) {
                rightCursorTexture = new Texture2D(2, 2);
                ImageConversion.LoadImage(rightCursorTexture, File.ReadAllBytes(rightPath));
            }
            if (File.Exists(leftPath)) {
                leftCursorTexture = new Texture2D(2, 2);
                ImageConversion.LoadImage(leftCursorTexture, File.ReadAllBytes(leftPath));
            }
            if (File.Exists(lutPath)) {
                lutTexture = new Texture2D(2, 2);
                ImageConversion.LoadImage(lutTexture, File.ReadAllBytes(lutPath));
            }
        }

        private void ListenForUDP()
        {
            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try {
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, 52222));
                byte[] buffer = new byte[16];
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (isRunning) {
                    if (udpSocket.ReceiveFrom(buffer, ref remoteEP) >= 16) {
                        p1X = BitConverter.ToSingle(buffer, 0);
                        p1Y = BitConverter.ToSingle(buffer, 4);
                        p2X = BitConverter.ToSingle(buffer, 8);
                        p2Y = BitConverter.ToSingle(buffer, 12);
                    }
                }
            } catch { }
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F11)) {
                isCalibrationMode = !isCalibrationMode;
                if (!isCalibrationMode) SaveDisplayConfig();
            }

            if (isCalibrationMode) {
                if (Input.GetKeyDown(KeyCode.R)) {
                    offsetX = 0f;
                    offsetY = 0f;
                    scaleMult = 1f;
                    currentStepIndex = 2;
                }

                if (Input.GetKeyDown(KeyCode.LeftBracket)) {
                    currentStepIndex = Mathf.Max(0, currentStepIndex - 1);
                }
                if (Input.GetKeyDown(KeyCode.RightBracket)) {
                    currentStepIndex = Mathf.Min(moveSteps.Length - 1, currentStepIndex + 1);
                }

                float cMoveStep = moveSteps[currentStepIndex];
                float cScaleStep = scaleSteps[currentStepIndex];

                if (Input.GetKeyDown(KeyCode.UpArrow)) offsetY -= cMoveStep;
                if (Input.GetKeyDown(KeyCode.DownArrow)) offsetY += cMoveStep;
                if (Input.GetKeyDown(KeyCode.LeftArrow)) offsetX -= cMoveStep;
                if (Input.GetKeyDown(KeyCode.RightArrow)) offsetX += cMoveStep;

                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) scaleMult -= cScaleStep;
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) scaleMult += cScaleStep;
                
                scaleMult = Mathf.Max(0.1f, scaleMult);
            }
        }

        public override void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float logicalWidth = 1920f;
            float logicalHeight = 1080f;
            float screenW = Screen.width;
            float screenH = Screen.height;
            float minEdge = Mathf.Min(screenW, screenH);
            
            float scale = (minEdge / logicalHeight) * scaleMult;
            float centerX = (screenW / 2f) + offsetX;
            float centerY = (screenH / 2f) + offsetY;

            float f1X = centerX + (p1X - (logicalWidth / 2f)) * scale;
            float f1Y = centerY + (p1Y - (logicalHeight / 2f)) * scale;
            float f2X = centerX + (p2X - (logicalWidth / 2f)) * scale;
            float f2Y = centerY + (p2Y - (logicalHeight / 2f)) * scale;

            if (isCalibrationMode) {
                if (lutTexture != null) {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    float lutW = logicalWidth * scale;
                    float lutH = logicalHeight * scale;
                    GUI.DrawTexture(new Rect(centerX - lutW / 2f, centerY - lutH / 2f, lutW, lutH), lutTexture);
                }

                GUI.color = Color.white;
                string lutStatus = lutTexture != null ? "<color=#55FF55>Loaded</color>" : "<color=#FF5555>Not Found</color>";
                
                string info = $"<color=yellow><b>[ Calibration Mode ]</b></color>\n\n" +
                              $"<color=#AAAAAA><b>Data:</b></color>\n" +
                              $"X: {offsetX:F1}  |  Y: {offsetY:F1}  |  Scale: {scaleMult:F4}\n" +
                              $"Move Step: {moveSteps[currentStepIndex]:F1}  |  Scale Step: {scaleSteps[currentStepIndex]:F4}\n" +
                              $"LUT Texture: {lutStatus}\n\n" +
                              $"<color=#AAAAAA><b>Controls:</b></color>\n" +
                              $"<b>[Arrows]</b> Move X/Y\n" +
                              $"<b>[- / =]</b> Adjust Scale\n" +
                              $"<b>[[ / ]]</b> Change Steps\n" +
                              $"<b>[R]</b> Reset to Default\n" +
                              $"<b>[F11]</b> Save & Exit";

                GUI.Label(new Rect(25, 25, 400, 300), info, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 18 });

                GUI.color = new Color(1, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(centerX - 40, centerY - 1, 80, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX - 1, centerY - 40, 2, 80), Texture2D.whiteTexture);
            }

            GUI.color = mintGreen;
            GUI.DrawTexture(new Rect(f1X, f1Y, CURSOR_SIZE_X, CURSOR_SIZE_Y), rightCursorTexture ?? Texture2D.whiteTexture);
            GUI.color = pastelBlue;
            GUI.DrawTexture(new Rect(f2X - CURSOR_SIZE_X, f2Y, CURSOR_SIZE_X, CURSOR_SIZE_Y), leftCursorTexture ?? Texture2D.whiteTexture);
        }

        public override void OnApplicationQuit() { isRunning = false; udpSocket?.Close(); }
    }
}