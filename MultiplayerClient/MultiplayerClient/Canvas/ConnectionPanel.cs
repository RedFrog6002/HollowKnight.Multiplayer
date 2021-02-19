using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Modding;

namespace MultiplayerClient.Canvas
{
    public class ConnectionPanel
    {
        public static CanvasPanel Panel;
        public static CanvasButton ConnectButton;
        public static CanvasText ConnectionInfo;
        public static CanvasButton TeamButton;
        public static CanvasButton SendButton;

        private static CanvasInput _ipInput;
        private static CanvasInput _portInput;
        private static CanvasInput _usernameInput;
        private static CanvasInput _chatInput;

        public static CanvasPanel WPanel;
        public static CanvasButton WDownload;
        public static CanvasToggle WSync;
        public static CanvasToggle WOverwrite;

        public static CanvasPanel PPanel;
        public static CanvasToggle PSend;
        public static CanvasToggle PRecieve;

        public static bool enabled = false;

        public static void BuildMenu(GameObject canvas)
        {
            Texture2D buttonImg = GUIController.Instance.images["Button_BG"];
            Texture2D inputImg = GUIController.Instance.images["Input_BG"];
            Texture2D panelImg = GUIController.Instance.images["Panel_BG"];
            Texture2D teamImg = GUIController.Instance.images["Team_BG"];
            Texture2D chatImg = GUIController.Instance.images["Chat_BG"];
            Texture2D sendImg = GUIController.Instance.images["Send_BG"];
            Texture2D toggleImg = GUIController.Instance.images["Toggle_BG"];
            Texture2D checkImg = GUIController.Instance.images["Checkmark"];

            float x = Screen.width / 2.0f - inputImg.width / 2.0f - 30.0f;
            float x2 = x - inputImg.width + 10f;
            float x3 = x - inputImg.width + 30f;
            float y = 30.0f;
            float y2 = y;

            EventSystem eventSystem = null;
            if (!GameObject.Find("EventSystem"))
            {
                GameObject eventSystemObj = new GameObject("EventSystem");

                eventSystem = eventSystemObj.AddComponent<EventSystem>();
                eventSystem.sendNavigationEvents = true;
                eventSystem.pixelDragThreshold = 10;

                eventSystemObj.AddComponent<StandaloneInputModule>();

                Object.DontDestroyOnLoad(eventSystemObj);
            }

            Panel = new CanvasPanel(
                canvas,
                panelImg,
                new Vector2(x, y),
                Vector2.zero,
                new Rect(0, 0, panelImg.width, panelImg.height)
            );

            Panel.AddText(
                "Connection Text",
                "Connection",
                new Vector2(x, y),
                new Vector2(buttonImg.width, buttonImg.height),
                GUIController.Instance.trajanNormal,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );
            y += buttonImg.height + 10;

            _ipInput = Panel.AddInput(
                "IP Input",
                inputImg,
                new Vector2(x, y),
                Vector2.zero,
                new Rect(0, y, inputImg.width, inputImg.height),
                GUIController.Instance.trajanNormal,
                MultiplayerClient.settings.host, "Address",
                16
            );
            y += inputImg.height + 5;

            _portInput = Panel.AddInput(
                "Port Input",
                inputImg,
                new Vector2(x, y),
                Vector2.zero,
                new Rect(0, y, inputImg.width, inputImg.height),
                GUIController.Instance.trajanNormal,
                MultiplayerClient.settings.port.ToString(), "Port",
                16
            );
            y += inputImg.height + 5;

            _usernameInput = Panel.AddInput(
                "Username Input",
                inputImg,
                new Vector2(x, y),
                Vector2.zero,
                new Rect(0, y, inputImg.width, inputImg.height),
                GUIController.Instance.trajanNormal,
                MultiplayerClient.settings.username, "Username",
                16
            );
            y += inputImg.height + 5;

            /*Panel.AddText(
                "Team Text",
                "Teams",
                new Vector2(x, y),
                new Vector2(buttonImg.width, buttonImg.height),
                GUIController.Instance.trajanNormal,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );
            y += buttonImg.height + 10;

            TeamButton = Panel.AddButton(
                "Team Button",
                teamImg,
                new Vector2(x, y),
                Vector2.zero,
                ToggleTeam,
                new Rect(0, y, teamImg.width, teamImg.height),
                GUIController.Instance.trajanNormal,
                "None",
                16
            );
            y += teamImg.height + 5;*/

            ConnectButton = Panel.AddButton(
                "Connect Button",
                buttonImg,
                new Vector2(x, y),
                Vector2.zero,
                ToggleConnectToServer,
                new Rect(0, y, buttonImg.width, buttonImg.height),
                GUIController.Instance.trajanNormal,
                "Connect",
                16
            );
            y += buttonImg.height;

            _chatInput = Panel.AddInput(
                "Chat Input",
                chatImg,
                new Vector2(x - 25, y),
                Vector2.zero,
                new Rect(0, y, chatImg.width, chatImg.height),
                GUIController.Instance.trajanNormal,
                "", "Chat",
                16
            );

            SendButton = Panel.AddButton(
                "Send Button",
                sendImg,
                new Vector2(x + chatImg.width - 23, y),
                Vector2.zero,
                SendMessage,
                new Rect(0, y, sendImg.width, sendImg.height),
                GUIController.Instance.trajanNormal,
                "",
                16
            );
            y += chatImg.height + 5;

            ConnectionInfo = new CanvasText(
                canvas,
                new Vector2(Screen.width / 2 - 500, Screen.height - 70),
                new Vector2(1000.0f, 50.0f),
                GUIController.Instance.trajanBold, "Disconnected.",
                fontSize: 42, alignment: TextAnchor.UpperCenter
            );

            WPanel = new CanvasPanel(
                canvas,
                panelImg,
                new Vector2(x2, y2),
                Vector2.zero,
                new Rect(0, 0, panelImg.width, panelImg.height)
            );

            WPanel.AddText(
                "World Text",
                "World Options",
                new Vector2(x2, y2),
                new Vector2(buttonImg.width, buttonImg.height),
                GUIController.Instance.trajanNormal,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );
            y2 += buttonImg.height + 10;

            WOverwrite = WPanel.AddToggle(
                "Toggle Overwrite",
                toggleImg,
                checkImg,
                new Vector2(x2, y2),
                new Vector2(panelImg.width, 20),
                new Vector2(-60, 0),
                new Rect(0, 0, 150, 20),
                (bool b) => SessionManager.Instance.OverwriteSave = b,
                GUIController.Instance.trajanNormal,
                "Overwrite Save",
                16
            );
            y2 += buttonImg.height + 5;
            void Sync(bool b)
            {
                if (Client.Instance.isConnected)
                {
                    SessionManager.Instance.WSyncClientEnabled = b;
                    Log("WSync = " + SessionManager.Instance.WSyncClientEnabled);
                    //WSync.Remove();
                }
            }
            WSync = WPanel.AddToggle(
                "Toggle World Sync",
                toggleImg,
                checkImg,
                new Vector2(x2, y2),
                new Vector2(panelImg.width, 20),
                new Vector2(-60, 0),
                new Rect(0, 0, 150, 20),
                Sync,
                GUIController.Instance.trajanNormal,
                "World Sync",
                16
            );
            y2 += 30;

            void Download(string s)
            {
                if (Client.Instance.isConnected)
                {
                    ClientSend.DownloadSave();
                    SessionManager.Instance.WDownloadClientEnabled = true;
                    //WDownload.Remove();
                }
            }
            WDownload = WPanel.AddButton(
                "Download Button",
                teamImg,
                new Vector2(x2, y2),
                Vector2.zero,
                Download,
                new Rect(0, y2, teamImg.width, teamImg.height),
                GUIController.Instance.trajanNormal,
                "Download World",
                16
            );
            y2 += 40;


            PPanel = new CanvasPanel(
                canvas,
                panelImg,
                new Vector2(x3, y2),
                Vector2.zero,
                new Rect(0, 0, panelImg.width, panelImg.height)
            );

            PPanel.AddText(
                "Pins Text",
                "Pins",
                new Vector2(x3, y2),
                new Vector2(buttonImg.width, buttonImg.height),
                GUIController.Instance.trajanNormal,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );
            y2 += buttonImg.height + 10;

            PSend = PPanel.AddToggle(
                "Toggle Pin Send",
                toggleImg,
                checkImg,
                new Vector2(x3, y2),
                new Vector2(panelImg.width, 20),
                new Vector2(-60, 0),
                new Rect(0, 0, 150, 20),
                ClientSend.DoSyncPins,
                GUIController.Instance.trajanNormal,
                "Sync Pin",
                16
            );
            y2 += 30;

            PSend = PPanel.AddToggle(
                "Toggle Pin Recieve",
                toggleImg,
                checkImg,
                new Vector2(x3, y2),
                new Vector2(panelImg.width, 20),
                new Vector2(-60, 0),
                new Rect(0, 0, 150, 20),
                (bool b) => SessionManager.Instance.RecievePins = b,
                GUIController.Instance.trajanNormal,
                "Show Pins",
                16
            );

            if (eventSystem != null)
            {
                eventSystem.firstSelectedGameObject = _ipInput.InputObject;
            }

            ConnectionInfo.SetActive(false);
            Panel.SetActive(false, true);
            WPanel.SetActive(false, true);
            PPanel.SetActive(false, true);

            On.HeroController.Pause += OnPause;
            On.HeroController.UnPause += OnUnPause;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChange;
        }

        public static void UpdateWPanel(bool b)
        {
            if (enabled)
            {
                WPanel.SetActive(b, !b);
            }
        }

        private static void OnPause(On.HeroController.orig_Pause orig, HeroController hc)
        {
            Panel.SetActive(true, false);
            WPanel.SetActive(SessionManager.Instance.WHostEnabled, !SessionManager.Instance.WHostEnabled);
            PPanel.SetActive(true, false);
            enabled = true;

            orig(hc);
        }

        private static void OnUnPause(On.HeroController.orig_UnPause orig, HeroController hc)
        {
            Panel.SetActive(false, true);
            WPanel.SetActive(false, true);
            PPanel.SetActive(false, true);
            enabled = false;

            orig(hc);
        }

        private static void OnSceneChange(Scene prevScene, Scene nextScene)
        {
            if (nextScene.name == "Menu_Title")
            {
                Panel.SetActive(false, true);
                WPanel.SetActive(false, true);
                enabled = false;
            }
        }

        private static Coroutine _connectRoutine;

        private static void ToggleConnectToServer(string buttonName)
        {
            if (Client.Instance.isConnected)
            {
                DisconnectFromServer();
            }
            else
            {
                ConnectToServer();
            }
        }
        private static void SendMessage(string buttonName)
        {
            if (Client.Instance.isConnected)
            {
                Log(_chatInput.GetText());
                ClientSend.Chat(Client.Instance.myId, _chatInput.GetText());
            }
            MultiplayerClient.Instance.herochat.GetComponent<MultiplayerClient.HeroChatText>().SetChat(_chatInput.GetText());
        }
        private static void ToggleTeam(string buttonName)
        {
            if (SessionManager.Instance.TeamsEnabled)
            {
                Client.Instance.team++;
                if (Client.Instance.team != 1 && Client.Instance.team != 2 && Client.Instance.team != 3 && Client.Instance.team != 4)
                {
                    Log("Team Was not an known amount");
                    Client.Instance.team = 1;
                }
                Log("Button pressed");
                switch (Client.Instance.team)
                {
                    case 1:
                        HeroController.instance.gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.red;
                        TeamButton.UpdateText("Red");
                        Log("Red");
                        break;
                    case 2:
                        HeroController.instance.gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.blue;
                        TeamButton.UpdateText("Blue");
                        Log("blue");
                        break;
                    case 3:
                        HeroController.instance.gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.yellow;
                        TeamButton.UpdateText("Yellow");
                        Log("yellow");
                        break;
                    case 4:
                        HeroController.instance.gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.green;
                        TeamButton.UpdateText("Green");
                        Log("green");
                        break;
                }
            }
            if (Client.Instance.isConnected)
            {
                ClientSend.Team(Client.Instance.myId, Client.Instance.team);
            }
        }

        private static void ConnectToServer()
        {
            MultiplayerClient.Instance.herochat.text = _chatInput.GetText();
            if (!Client.Instance.isConnected)
            {
                Log("Connecting to Server...");
                ConnectionInfo.UpdateText("Connecting to server...");

                if (_ipInput.GetText() != "") MultiplayerClient.settings.host = _ipInput.GetText();
                if (_portInput.GetText() != "") MultiplayerClient.settings.port = int.Parse(_portInput.GetText());
                if (_usernameInput.GetText() != "") MultiplayerClient.settings.username = _usernameInput.GetText();

                PlayerManager.Instance.activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                _connectRoutine = Client.Instance.StartCoroutine(Connect());

                Log("Connected to Server!");
                ConnectionInfo.UpdateText("Connected to server.");
                ConnectButton.UpdateText("Disconnect");
            }
            else
            {
                Log("Already connected to the server!");
            }
        }

        private static IEnumerator Connect()
        {
            int waitTime = 2000;
            int time = DateTime.Now.Millisecond;
            // 5 connection attempts before giving up 
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                Log("Connection Attempt: " + attempt);
                if (!Client.Instance.isConnected)
                {
                    try
                    {
                        Client.Instance.ConnectToServer();
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                        continue;
                    }
                }
                else
                {
                    Log("Connected to Server!");
                    break;
                }

                yield return new WaitWhile(() => Client.Instance.isConnected && DateTime.Now.Millisecond - time <= waitTime);
                time = DateTime.Now.Millisecond;
            }
        }

        private static void DisconnectFromServer()
        {
            Log("Disconnecting from Server...");
            Client.Instance.StopCoroutine(_connectRoutine);
            Client.Instance.Disconnect();
        }

        private static void Log(object message) => Modding.Logger.Log("[Connection Panel] " + message);
    }
}