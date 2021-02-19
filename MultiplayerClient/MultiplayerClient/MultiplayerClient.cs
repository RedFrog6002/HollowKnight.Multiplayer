using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Modding;
using MultiplayerClient.Canvas;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ModCommon.Util;

namespace MultiplayerClient
{
    public class MultiplayerClient : Mod<SaveSettings, GlobalSettings>
    {
        public static readonly Dictionary<string, GameObject> GameObjects = new Dictionary<string, GameObject>();
        internal static GlobalSettings settings;

        public static MultiplayerClient Instance;
        
        public static Dictionary<byte[], string> textureCache = new Dictionary<byte[], string>(new ByteArrayComparer());

        public static Dictionary<int, GameObject> Enemies = new Dictionary<int, GameObject>();

        public static string CustomKnightDir;
        
        public override string GetVersion()
        {
            return "0.1.7F";
        }
        
        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                ("GG_Hive_Knight", "Battle Scene/Globs/Hive Knight Glob"),
                ("GG_Hive_Knight", "Battle Scene/Hive Knight/Slash 1"),
            };
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            settings = GlobalSettings;

            // Initialize texture cache
            // This will allow us to easily send textures to the server when asked to.
            string cacheDir = Path.Combine(Application.dataPath, "SkinCache");
            Directory.CreateDirectory(cacheDir);
            string[] files = Directory.GetFiles(cacheDir);
            foreach(string filePath in files)
            {
                string filename = Path.GetFileName(filePath);
                byte[] hash = new byte[20];
                for (int i = 0; i < 40; i += 2)
                {
                    hash[i / 2] = Convert.ToByte(filename.Substring(i, 2), 16);
                }

                textureCache[hash] = filePath;
            }
            
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    CustomKnightDir = Path.GetFullPath(Application.dataPath + "/Resources/Data/Managed/Mods/CustomKnight");
                    break;
                default:
                    CustomKnightDir = Path.GetFullPath(Application.dataPath + "/Managed/Mods/CustomKnight");
                    break;
            }

            GameObjects.Add("Glob", preloadedObjects["GG_Hive_Knight"]["Battle Scene/Globs/Hive Knight Glob"]);
            GameObjects.Add("Slash", preloadedObjects["GG_Hive_Knight"]["Battle Scene/Hive Knight/Slash 1"]);

            Instance = this;
            
            GUIController.Instance.BuildMenus();

            GameManager.instance.gameObject.AddComponent<MPClient>();

            Unload();
            
            ModHooks.Instance.CharmUpdateHook += OnCharmUpdate;
            ModHooks.Instance.ApplicationQuitHook += OnApplicationQuit;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            On.HeroController.Start += HeroController_Start;
            //On.HeroController.TakeDamage += HeroController_TakeDamage;
            On.HeroController.Update += HeroController_Update;
            On.DesktopPlatform.WriteSaveSlot += DesktopPlatform_WriteSaveSlot;
            ModHooks.Instance.SetPlayerBoolHook += Instance_SetPlayerBoolHook;
            ModHooks.Instance.SetPlayerFloatHook += Instance_SetPlayerFloatHook;
            ModHooks.Instance.SetPlayerIntHook += Instance_SetPlayerIntHook;
            ModHooks.Instance.SetPlayerStringHook += Instance_SetPlayerStringHook;
            ModHooks.Instance.SetPlayerVariableHook += Instance_SetPlayerVariableHook;
            ModHooks.Instance.SetPlayerVector3Hook += Instance_SetPlayerVector3Hook;
            ModHooks.Instance.LanguageGetHook += Instance_LanguageGetHook;
            ClientPin.SetUpStaticHooks();
        }

        private string Instance_LanguageGetHook(string key, string sheetTitle)
        {
            if (key.Contains("Player_Name_"))
                return key.Substring(12, key.Length - 12);
            return Language.Language.GetInternal(key, sheetTitle);
        }

        private void Instance_SetPlayerVector3Hook(string vector3Name, Vector3 value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.Vector3, vector3Name, value);
            PlayerData.instance.SetVector3Internal(vector3Name, value);
        }

        private object Instance_SetPlayerVariableHook(Type type, string varName, object value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.Other, varName, Newtonsoft.Json.JsonConvert.SerializeObject(value));
            return value;
        }

        private void Instance_SetPlayerStringHook(string stringName, string value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.String, stringName, value);
            PlayerData.instance.SetStringInternal(stringName, value);
        }

        private void Instance_SetPlayerIntHook(string intName, int value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.Int, intName, value);
            PlayerData.instance.SetIntInternal(intName, value);
        }

        private void Instance_SetPlayerFloatHook(string floatName, float value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.Float, floatName, value);
            PlayerData.instance.SetFloatInternal(floatName, value);
        }

        private void Instance_SetPlayerBoolHook(string originalSet, bool value)
        {
            ClientSend.SendPDChange(PlayerDataTypes.Bool, originalSet, value);
            PlayerData.instance.SetBoolInternal(originalSet, value);
        }

        private void DesktopPlatform_WriteSaveSlot(On.DesktopPlatform.orig_WriteSaveSlot orig, DesktopPlatform self, int slotIndex, byte[] bytes, Action<bool> callback)
        {
            if ((!SessionManager.Instance.WDownloadClientEnabled && !SessionManager.Instance.WSyncClientEnabled) || SessionManager.Instance.OverwriteSave || Client.Instance.isHost)
            {
                orig(self, slotIndex, bytes, callback);
            }
            callback.Invoke(true);
        }

        private void HeroController_Update(On.HeroController.orig_Update orig, HeroController self)
        {
            orig(self);
            /*if (Input.GetKeyDown(KeyCode.H))
            {
                PlayerData.instance.SetInt("rancidEggs", 30);
            }*/
            try
            {
                if (PlayerManager.Instance.CurrentRoomSyncHost)
                {
                    GameObject[] enemies = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(go => go.layer == 11 || go.layer == 22 || go.layer == 15) as GameObject[];
                    if (enemies != null)
                    {
                        for (int i = 0; i <= enemies.Length; i++)
                        {
                            try
                            {
                                if (enemies[i] != null && MultiplayerClient.Enemies.Values.Contains(enemies[i]))
                                {
                                    var enemy = enemies[i];
                                    var e = enemy.AddComponent<EnemyTracker>();
                                    e.enemyId = i;
                                    MultiplayerClient.Enemies.Add(i, enemy);
                                }
                            }
                            catch (NullReferenceException e)
                            {
                                MultiplayerClient.Instance.Log("Object in enemies found null:  " + i + "    " + e);
                            }
                        }
                    }
                }
            }
            catch { }
            if (Input.GetKeyDown(KeyCode.G))
                ClientPin.CreatePin("Test Name", true, new Vector3(10, 10, SessionManager.Instance.pinPrefab.transform.position.z));
        }

        /*private void HeroController_TakeDamage(On.HeroController.orig_TakeDamage orig, HeroController self, GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            orig(self, go, damageSide, damageAmount, hazardType);
            if (SessionManager.Instance.TeamsEnabled)
            {
                switch (Client.Instance.team)
                {
                    case 1:
                        self.gameObject.GetComponent<global::tk2dSprite>().color = Color.red;
                        break;
                    case 2:
                        self.gameObject.GetComponent<global::tk2dSprite>().color = Color.blue;
                        break;
                    case 3:
                        self.gameObject.GetComponent<global::tk2dSprite>().color = Color.yellow;
                        break;
                    case 4:
                        self.gameObject.GetComponent<global::tk2dSprite>().color = Color.green;
                        break;
                }
            }
        }*/

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);
            GameObject nameObj = UnityEngine.Object.Instantiate(new GameObject("Username"), self.transform.position + Vector3.up * 1.25f,
                Quaternion.identity);
            nameObj.transform.SetParent(self.transform);
            nameObj.transform.SetScaleX(0.25f);
            nameObj.transform.SetScaleY(0.25f);
            TextMeshPro nameText = nameObj.AddComponent<TextMeshPro>();
            nameText.text = base.GlobalSettings.username;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 24;
            nameText.outlineColor = Color.black;
            nameText.outlineWidth = 0.1f;
            nameObj.AddComponent<KeepWorldScalePositive>();
            GameObject chatObj = UnityEngine.Object.Instantiate(new GameObject("Chattext"), self.transform.position + Vector3.up * 2f,
                 Quaternion.identity);
            chatObj.transform.SetParent(self.transform);
            chatObj.transform.SetScaleX(0.25f);
            chatObj.transform.SetScaleY(0.25f);
            TextMeshPro chatText = chatObj.AddComponent<TextMeshPro>();
            chatText.text = "";
            chatText.alignment = TextAlignmentOptions.Center;
            chatText.fontSize = 24;
            chatText.outlineColor = Color.black;
            chatText.outlineWidth = 0.1f;
            chatObj.AddComponent<KeepWorldScalePositive>();
            chatObj.AddComponent<HeroChatText>();
            heroname = nameText;
            herochat = chatText;
        }
        public class HeroChatText : MonoBehaviour
        {
            TMPro.TextMeshPro chattext;
            Coroutine routine;
            private void Start()
            {
                chattext = GetComponent<TMPro.TextMeshPro>();
            }

            public void SetChat(string text, TMPro.TextMeshPro textMesh = null)
            {
                if (textMesh != null)
                    chattext = textMesh;
                if (chattext != null)
                {
                    chattext.text = text;
                    if (routine != null)
                    {
                        StopCoroutine(routine);
                    }
                    routine = StartCoroutine(ChatRoutine());
                }
            }

            public string GetChat()
            {
                if (chattext != null)
                    return chattext.text;
                else
                    return "";
            }

            private IEnumerator ChatRoutine()
            {
                chattext.color = Color.white;
                yield return new WaitForSeconds(1.5f);
                for (int i = 9; i >= 0; i--)
                {
                    chattext.color = new Color(1, 1, 1, i / 10f);
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
        public TMPro.TextMeshPro heroname;
        public TMPro.TextMeshPro herochat;
        private void OnApplicationQuit()
        {
            SaveGlobalSettings();

            if (Client.Instance != null)
            {
                Client.Instance.Disconnect();
            }
        }

        private void OnCharmUpdate(PlayerData pd, HeroController hc)
        {
            if (Client.Instance != null && Client.Instance.isConnected && pd != null && hc != null)
            {
                ClientSend.CharmsUpdated(pd);
            }
        }

        private void Unload()
        {
            ModHooks.Instance.CharmUpdateHook -= OnCharmUpdate;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        public static List<string> NonGameplayScenes = new List<string>
        {
            "BetaEnd",
            "Cinematic_Stag_travel",
            "Cinematic_Ending_A",
            "Cinematic_Ending_B",
            "Cinematic_Ending_C",
            "Cinematic_Ending_D",
            "Cinematic_Ending_E",
            "Cinematic_MrMushroom",
            "Cutscene_Boss_Door",
            "End_Credits",
            "End_Game_Completion",
            "GG_Boss_Door_Entrance",
            "GG_End_Sequence",
            "GG_Entrance_Cutscene",
            "GG_Unlock",
            "Intro_Cutscene",
            "Intro_Cutscene_Prologue",
            "Knight Pickup",
            "Menu_Title",
            "Menu_Credits",
            "Opening_Sequence",
            "PermaDeath_Unlock",
            "Pre_Menu_Intro",
            "PermaDeath",
            "Prologue_Excerpt",
        };
        private void OnSceneChanged(Scene prevScene, Scene nextScene)
        {
            PlayerManager.Instance.activeScene = nextScene.name;
            if (Client.Instance.isConnected)
            {
                /*bool otherplayer = false;
                if (SessionManager.Instance.Players.Values != null)
                {
                    foreach (PlayerManager pm in SessionManager.Instance.Players.Values)
                    {
                        if (pm.activeScene == nextScene.name)
                            otherplayer = true;
                    }
                }*/
                PlayerManager.Instance.CurrentRoomSyncHost = false;
                if (!NonGameplayScenes.Contains(nextScene.name))
                {
                    ClientSend.SceneChanged(nextScene.name);
                }
                Enemies.Clear();
                /*if (!otherplayer)
                {
                    GameObject[] enemies = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(go => go.layer == 11 || go.layer == 22 || go.layer == 15) as GameObject[];
                    if (enemies != null)
                    {
                        for (int i = 0; i <= enemies.Length; i++)
                        {
                            try
                            {
                                if (enemies[i] != null)
                                {
                                    var enemy = enemies[i];
                                    var e = enemy.AddComponent<EnemyTracker>();
                                    e.enemyId = i;
                                    Enemies.Add(i, enemy);
                                }
                            }
                            catch (NullReferenceException e)
                            {
                                MultiplayerClient.Instance.Log("Object in enemies found null:  " + i + "    " + e);
                            }
                        }
                    }
                }*/
            }
            
            SessionManager.Instance.DestroyAllPlayers();
        }
    }
}