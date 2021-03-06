﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using HutongGames.PlayMaker.Actions;
using ModCommon;
using ModCommon.Util;
using UnityEngine;
using MultiplayerClient.Canvas;
using System.Reflection;
using Newtonsoft.Json;
using Modding.Patches;

namespace MultiplayerClient
{
    public class ClientHandle : MonoBehaviour
    {
        public static void Welcome(Packet packet)
        {
            byte myId = packet.ReadByte();
            string msg = packet.ReadString();

            Log($"Message from server: {msg}");
            Client.Instance.myId = myId;

            // Ideally you would do this on skin change.
            // For now we only do it when joining the server.
            //var hashes = GetTextureHashes();
            
            ClientSend.WelcomeReceived(/*hashes, */Client.Instance.isHost);

            Client.Instance.udp.Connect(((IPEndPoint) Client.Instance.tcp.socket.Client.LocalEndPoint).Port);
        }

        public static void SpawnPlayer(Packet packet)
        {
            if (Client.Instance.isConnected)
            {
                byte id = packet.ReadByte();
                string username = packet.ReadString();
                Vector3 position = packet.ReadVector3();
                Vector3 scale = packet.ReadVector3();
                string animation = packet.ReadString();
                string scenename = packet.ReadString();
                List<bool> charmsData = new List<bool>();
                for (int charmNum = 1; charmNum <= 40; charmNum++)
                {    
                    charmsData.Add(packet.ReadBool());
                }
                int team = packet.ReadInt();

                string chat = packet.ReadString();
                bool pvpEnabled = packet.ReadBool();
                bool teamsEnabled = packet.ReadBool();
                bool Wsyncenabled = packet.ReadBool();
                SessionManager.Instance.EnablePvP(pvpEnabled);
                SessionManager.Instance.EnableTeams(teamsEnabled);
                SessionManager.Instance.WHostEnabled = Wsyncenabled;
                SessionManager.Instance.SpawnPlayer(id, username, position, scale, animation, charmsData, team, scenename, chat);
                    
                var player = SessionManager.Instance.Players[id];
                /*foreach (TextureType tt in Enum.GetValues(typeof(TextureType)))
                {
                    var hash = packet.ReadBytes(20);
                    Log("Hash null? " + (hash == null));
                    player.texHashes.Add(hash, tt);
                }*/

                SessionManager.Instance.ReloadPlayerTextures(player);
            }
        }

        #region CustomKnight Integration

        public static void LoadTexture(Packet packet)
        {
            byte[] hash = packet.ReadBytes(20);
            if (SessionManager.Instance.loadedTextures.ContainsKey(hash)) return;

            int texLen = packet.ReadInt();
            byte[] texBytes = packet.ReadBytes(texLen);

            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(texBytes);

            // Save reference to texture for easy reuse
            SessionManager.Instance.loadedTextures[hash] = texture;

            foreach (var player in SessionManager.Instance.Players.Values)
            {
                if(player.texHashes.ContainsKey(hash))
                {
                    TextureType tt = player.texHashes[hash];
                    player.textures[tt] = texture;
                }
            }
        }

        public static void HandleTextureRequest(Packet packet)
        {
            byte[] hash = packet.ReadBytes(20);

            Log("Received request for hash " + BitConverter.ToString(hash));
            if (MultiplayerClient.textureCache.ContainsKey(hash))
            {
                byte[] texture = File.ReadAllBytes(MultiplayerClient.textureCache[hash]);
                Log("Sending texture for hash " + BitConverter.ToString(hash));
                ClientSend.SendTexture(texture);
            }
        }

        /// Hash the textures and store them in the cache if needed.
        /// This method is slow - don't use it in the middle of gameplay.
        public static List<byte[]> GetTextureHashes()
        {
            var cacheDir = Path.Combine(Application.dataPath, "SkinCache");
            Directory.CreateDirectory(cacheDir);
            
            GameObject hc = HeroController.instance.gameObject;

            // SHA-1 hashes are 20 bytes, or 160 bits long.
            const int HASH_LENGTH = 20;
            byte[] baldurHash = new byte[HASH_LENGTH];
            byte[] flukeHash = new byte[HASH_LENGTH];
            byte[] grimmHash = new byte[HASH_LENGTH];
            byte[] hatchlingHash = new byte[HASH_LENGTH];
            byte[] knightHash = new byte[HASH_LENGTH];    
            byte[] shieldHash = new byte[HASH_LENGTH];
            byte[] sprintHash = new byte[HASH_LENGTH];
            byte[] unnHash = new byte[HASH_LENGTH];
            byte[] voidHash = new byte[HASH_LENGTH];
            byte[] vsHash = new byte[HASH_LENGTH];
            byte[] weaverHash = new byte[HASH_LENGTH];
            byte[] wraithsHash = new byte[HASH_LENGTH];

            var anim = hc.GetComponent<tk2dSpriteAnimator>();
            Texture2D knightTex = anim.GetClipByName("Idle").frames[0].spriteCollection.spriteDefinitions[0].material.mainTexture as Texture2D;
            Texture2D sprintTex = anim.GetClipByName("Sprint").frames[0].spriteCollection.spriteDefinitions[0].material.mainTexture as Texture2D;
            Texture2D unnTex = anim.GetClipByName("Slug Up").frames[0].spriteCollection.spriteDefinitions[0].material.mainTexture as Texture2D;

            knightHash = knightTex.Hash();
            sprintHash = sprintTex.Hash();
            unnHash = unnTex.Hash();
            
            foreach (Transform child in hc.transform)
            {
                if (child.name == "Spells")
                {
                    foreach (Transform spellsChild in child)
                    {
                        if (spellsChild.name == "Scr Heads")
                        {
                            Texture2D wraithsTex = spellsChild.gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                            wraithsHash = wraithsTex.Hash();
                        }
                        else if (spellsChild.name == "Scr Heads 2")
                        {
                            Texture2D voidTex = spellsChild.gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                            voidHash = voidTex.Hash();
                        }
                    }
                }
                else if (child.name == "Focus Effects")
                {
                    foreach (Transform focusChild in child)
                    {
                        if (focusChild.name == "Heal Anim")
                        {
                            Texture2D vsTex = focusChild.gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                            vsHash = vsTex.Hash();
                            break;
                        }
                    }
                }
                else if (child.name == "Charm Effects")
                {
                    foreach (Transform charmChild in child)
                    {
                        if (charmChild.name == "Blocker Shield")
                        {
                            GameObject shellAnim = charmChild.GetChild(0).gameObject;
                            Texture2D baldurTex = shellAnim.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                            baldurHash = baldurTex.Hash();
                            break;            
                        }
                    }
                    
                    PlayMakerFSM poolFlukes = child.gameObject.LocateMyFSM("Pool Flukes");
                    GameObject fluke = poolFlukes.GetAction<CreateGameObjectPool>("Pool Normal", 0).prefab.Value;
                    Texture2D flukeTex = fluke.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                    flukeHash = flukeTex.Hash();

                    PlayMakerFSM spawnGrimmchild = child.gameObject.LocateMyFSM("Spawn Grimmchild");
                    GameObject grimm = spawnGrimmchild.GetAction<SpawnObjectFromGlobalPool>("Spawn", 2).gameObject.Value;
                    Texture2D grimmTex = grimm.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                    grimmHash = grimmTex.Hash();

                    PlayMakerFSM hatchlingSpawn = child.gameObject.LocateMyFSM("Hatchling Spawn");
                    GameObject hatchling = hatchlingSpawn.GetAction<SpawnObjectFromGlobalPool>("Hatch", 2).gameObject.Value;
                    Texture2D hatchlingTex = hatchling.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                    hatchlingHash = hatchlingTex.Hash();

                    PlayMakerFSM spawnOrbitShield = child.gameObject.LocateMyFSM("Spawn Orbit Shield");
                    GameObject orbitShield = spawnOrbitShield.GetAction<SpawnObjectFromGlobalPool>("Spawn", 2).gameObject.Value;
                    GameObject shield = orbitShield.FindGameObjectInChildren("Shield");
                    Texture2D shieldTex = shield.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                    shieldHash = shieldTex.Hash();

                    PlayMakerFSM weaverlingControl = child.gameObject.LocateMyFSM("Weaverling Control");
                    GameObject weaver = weaverlingControl.GetAction<SpawnObjectFromGlobalPool>("Spawn", 0).gameObject.Value;
                    Texture2D weaverTex = weaver.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture as Texture2D;
                    weaverHash = weaverTex.Hash();
                }
            }

            // Ordered according to the TextureType enum
            var hashes = new List<byte[]>();
            hashes.Add(baldurHash);
            hashes.Add(flukeHash);
            hashes.Add(grimmHash);
            hashes.Add(hatchlingHash);
            hashes.Add(knightHash);
            hashes.Add(shieldHash);
            hashes.Add(sprintHash);
            hashes.Add(unnHash);
            hashes.Add(voidHash);
            hashes.Add(vsHash);
            hashes.Add(weaverHash);
            hashes.Add(wraithsHash);
            
            return hashes;
        }
        
        #endregion CustomKnight Integration
        
        public static void DestroyPlayer(Packet packet)
        {
            byte clientToDestroy = packet.ReadByte();
            bool amnewhost = packet.ReadBool();
            Log(amnewhost);
            SessionManager.Instance.DestroyPlayer(clientToDestroy);
            if (amnewhost && !PlayerManager.Instance.CurrentRoomSyncHost)
            {
                Log("clearing enemies");
                MultiplayerClient.Enemies.Clear();
                GameObject[] enemies = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(go => go.layer == 11 || go.layer == 17) as GameObject[];
                if (enemies != null)
                {
                    for (int i = 0; i <= enemies.Length; i++)
                    {
                        try
                        {
                            if (enemies[i] != null)
                            {
                                Log("adding enemy");
                                var enemy = enemies[i];
                                var e = enemy.GetOrAddComponent<EnemyTracker>();
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

        internal static void WorldEnabled(Packet packet)
        {
            bool enabled = packet.ReadBool();
            SessionManager.Instance.WHostEnabled = enabled;
            ConnectionPanel.UpdateWPanel(enabled);
            if (!enabled)
            {
                SessionManager.Instance.WDownloadClientEnabled = false;
                SessionManager.Instance.WSyncClientEnabled = false;
            }
        }

        public static void PvPEnabled(Packet packet)
        {
            bool enablePvP = packet.ReadBool();

            SessionManager.Instance.EnablePvP(enablePvP);
        }

        public static void TeamsEnabled(Packet packet)
        {
            bool enableTeams = packet.ReadBool();

            SessionManager.Instance.EnableTeams(enableTeams);
        }

        public static void PlayerPosition(Packet packet)
        {
            byte id = packet.ReadByte();
            Vector3 position = packet.ReadVector3();

            if (SessionManager.Instance.Players.ContainsKey(id))
            {
                SessionManager.Instance.Players[id].gameObject.transform.position = position;
            }
        }

        public static void PlayerScale(Packet packet)
        {
            byte id = packet.ReadByte();
            Vector3 scale = packet.ReadVector3();

            if (SessionManager.Instance.Players.ContainsKey(id))
            {
                SessionManager.Instance.Players[id].gameObject.transform.localScale = scale;
            }
        }
        
        public static void PlayerAnimation(Packet packet)
        {
            byte id = packet.ReadByte();
            string animation = packet.ReadString();

            if (SessionManager.Instance.Players.ContainsKey(id))
            {
                var anim = SessionManager.Instance.Players[id].gameObject.GetComponent<tk2dSpriteAnimator>();
                anim.Stop();
                anim.Play(animation);

                SessionManager.Instance.StartCoroutine(MPClient.Instance.PlayAnimation(id, animation));
            }
        }

        public static void HealthUpdated(Packet packet)
        {
            byte fromClient = packet.ReadByte();
            int health = packet.ReadInt();
            int maxHealth = packet.ReadInt();
            int healthBlue = packet.ReadInt();

            Log("Health Data from Server: " + health + " " + maxHealth + " " + healthBlue);
            
            SessionManager.Instance.Players[fromClient].health = health;
            SessionManager.Instance.Players[fromClient].maxHealth = maxHealth;
            SessionManager.Instance.Players[fromClient].healthBlue = healthBlue;
        }
        
        public static void CharmsUpdated(Packet packet)
        {
            byte fromClient = packet.ReadByte();
            for (int charmNum = 1; charmNum <= 40; charmNum++)
            {
                bool equippedCharm = packet.ReadBool();
                SessionManager.Instance.Players[fromClient].SetAttr("equippedCharm_" + charmNum, equippedCharm);
            }
            Log("Finished Modifying equippedCharm bools");
        }
        
        public static void PlayerDisconnected(Packet packet)
        {
            byte id = packet.ReadByte();
            Log($"Player {id} has disconnected from the server.");
            bool newhost = packet.ReadBool();
            SessionManager.Instance.DestroyPlayer(id);
            SessionManager.Instance.DestroyPin(id);
            if (newhost)
            {
                MultiplayerClient.Enemies.Clear();
                GameObject[] enemies = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(go => go.layer == 11 || go.layer == 17) as GameObject[];
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

        public static void DisconnectSelf(Packet packet)
        {
            Log("Disconnecting Self");
            Client.Instance.Disconnect();
        }

        public static void Team(Packet packet)
        {
            byte id = packet.ReadByte();
            int team = packet.ReadInt();
            Log($"Player {id} has teamed.");

            //SessionManager.Instance.TeamPlayer(id, team);
        }

        public static void Chat(Packet packet)
        {
            Log("suspicious teritory");
            Byte id = packet.ReadByte();
            Log("byte read");
            String message = packet.ReadString();
            Log($"Player {id} has chated.");
            Log(message);
            SessionManager.Instance.Chat(id, message);
        }

        public static void SyncEnemy(Packet packet)
        {
            string goName = packet.ReadString();
            int id = packet.ReadInt();
            if (!PlayerManager.Instance.CurrentRoomSyncHost)
            {
                GameObject[] enemies = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(go => go.layer == 11 || go.layer == 17) as GameObject[];
                if (enemies != null)
                {
                    try
                    {
                        if (enemies[id] != null)
                        {
                            var enemy = enemies[id];
                            if (enemy.name == goName)
                            {
                                //var e = enemy.AddComponent<EnemyTracker>();
                                //e.enemyId = i;
                                MultiplayerClient.Enemies.Add(id, enemy);
                            }
                        }
                    }
                    catch (NullReferenceException e)
                    {
                        MultiplayerClient.Instance.Log("Object in enemies found null:  " + id + "    " + e);
                    }
                }
            }
        }

        public static void EnemyPosition(Packet packet)
        {
            Vector3 position = packet.ReadVector3();
            int id = packet.ReadInt();

            MultiplayerClient.Enemies[id].transform.SetPosition3D(position.x, position.y, position.z);
        }

        public static void EnemyScale(Packet packet)
        {
            Vector3 scale = packet.ReadVector3();
            int id = packet.ReadInt();

            MultiplayerClient.Enemies[id].transform.SetScaleX(scale.x);
            MultiplayerClient.Enemies[id].transform.SetScaleY(scale.y);
            MultiplayerClient.Enemies[id].transform.SetScaleZ(scale.z);
        }

        public static void EnemyAnimation(Packet packet)
        {
            string animation = packet.ReadString();
            int id = packet.ReadInt();
            if (MultiplayerClient.Enemies[id].GetComponent<tk2dSpriteAnimator>())
            {
                MultiplayerClient.Enemies[id].GetComponent<tk2dSpriteAnimator>().Stop();
                MultiplayerClient.Enemies[id].GetComponent<tk2dSpriteAnimator>().Play(animation);
            }
        }
        public static void StartEnemySync(Packet packet)
        {
            MultiplayerClient.Enemies.Clear();
            PlayerManager.Instance.CurrentRoomSyncHost = true;
        }
        public static void PlayerDataSync(Packet packet)
        {
            if (PlayerData.instance != null && SessionManager.Instance.WSyncClientEnabled)
            {
                PlayerDataTypes pdtype = (PlayerDataTypes)packet.ReadInt();
                string variable = packet.ReadString();
                bool dosync = true;
                foreach((string s, ExclusionType exclusiontype, bool skip) in SAVEEXCLUDE)
                {
                    dosync = !(exclusiontype == ExclusionType.Contains ? variable.ToLower().Contains(s) : variable.ToLower() == s);
                    if (!dosync)
                    {
                        if (skip)
                            dosync = true;
                        break;
                    }
                }
                Log("PlayerData Sync " + dosync + " " + variable);
                if (dosync)
                {
                    switch (pdtype)
                    {
                        case PlayerDataTypes.Bool:
                            bool b = packet.ReadBool();
                            Modding.ReflectionHelper.SetAttrSafe<PlayerData, bool>(PlayerData.instance, variable, b);
                            break;
                        case PlayerDataTypes.Float:
                            float f = packet.ReadFloat();
                            Modding.ReflectionHelper.SetAttrSafe<PlayerData, float>(PlayerData.instance, variable, f);
                            break;
                        case PlayerDataTypes.Int:
                            int i = packet.ReadInt();
                            Modding.ReflectionHelper.SetAttrSafe<PlayerData, int>(PlayerData.instance, variable, i);
                            break;
                        case PlayerDataTypes.Other:
                            string json = packet.ReadString();
                            foreach (FieldInfo fi in typeof(PlayerData).GetFields())
                            {
                                if (fi.Name == variable)
                                {
                                    object obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                                    fi.SetValue(PlayerData.instance, obj);
                                }
                            }
                            break;
                        case PlayerDataTypes.String:
                            string s = packet.ReadString();
                            Modding.ReflectionHelper.SetAttrSafe<PlayerData, string>(PlayerData.instance, variable, s);
                            break;
                        case PlayerDataTypes.Vector3:
                            Vector3 v = packet.ReadVector3();
                            Modding.ReflectionHelper.SetAttrSafe<PlayerData, Vector3>(PlayerData.instance, variable, v);
                            break;
                    }
                }
            }
        }
        public static void UseHostPD(Packet packet)
        {
            Log("Downloading Host PD");
            string json = packet.ReadString();
            PlayerData hostdata = JsonConvert.DeserializeObject<PlayerData>(json, new JsonSerializerSettings()
            {
                ContractResolver = ShouldSerializeContractResolver.Instance,
                TypeNameHandling = TypeNameHandling.Auto
            });
            foreach (FieldInfo fi in typeof(PlayerData).GetFields())
            {
                if (!fi.IsStatic)
                {
                    bool dosync = true;
                    foreach ((string s, ExclusionType exclusiontype, bool skip) in SAVEEXCLUDE)
                    {
                        dosync = !(exclusiontype == ExclusionType.Contains ? fi.Name.ToLower().Contains(s) : fi.Name.ToLower() == s);
                        if (!dosync)
                        {
                            if (skip)
                                dosync = true;
                            break;
                        }
                    }
                    if (dosync)
                        fi.SetValue(PlayerData.instance, fi.GetValue(hostdata));
                }
            }
        }

        public static void SetPinPosition(Packet packet)
        {
            byte id = packet.ReadByte();
            Vector3 position = packet.ReadVector3();
            SessionManager.Instance.Pins[id].Position = position;
        }

        public static void SetPinEnabled(Packet packet)
        {
            byte id = packet.ReadByte();
            bool enabled = packet.ReadBool();
            SessionManager.Instance.Pins[id].isEnabled = enabled;
        }

        public static void CreatePin(Packet packet)
        {
            byte id = packet.ReadByte();
            string name = packet.ReadString();
            bool enabled = packet.ReadBool();
            Vector3 position = packet.ReadVector3();
            SessionManager.Instance.SpawnPin(id, name, enabled, position);
        }

        private static List<(string, ExclusionType, bool)> SAVEEXCLUDE = new List<(string, ExclusionType, bool)>()
        {
            ("gotCharm", ExclusionType.Equles, true),
            ("charm", ExclusionType.Contains, false),
            ("scene", ExclusionType.Contains, false),
            ("gmap", ExclusionType.Contains, false),
            ("pause", ExclusionType.Contains, false),
            ("invincible", ExclusionType.Contains, false),
            ("bench", ExclusionType.Contains, false),
            ("respawn", ExclusionType.Contains, false),
            ("mpcharge", ExclusionType.Equles, false),
            ("mpreserve", ExclusionType.Contains, false),
            ("fragment", ExclusionType.Contains, false),
            ("geo", ExclusionType.Contains, false),
            ("dark", ExclusionType.Contains, false),
        };

        enum ExclusionType
        {
            Contains,
            Equles,
        }
        private static void Log(object message) => Modding.Logger.Log("[Client Handle] " + message);
    }
}
