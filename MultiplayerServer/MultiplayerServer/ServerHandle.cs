using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using IL.HutongGames.PlayMaker.Actions;
using ModCommon.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MultiplayerServer
{
    public class ServerHandle
    {
        public static void WelcomeReceived(byte fromClient, Packet packet)
        {
            byte clientIdCheck = packet.ReadByte();
            string username = packet.ReadString();
            bool isHost = packet.ReadBool();
            string currentClip = packet.ReadString();
            string activeScene = packet.ReadString();
            Vector3 position = packet.ReadVector3();
            Vector3 scale = packet.ReadVector3();
            int health = packet.ReadInt();
            int maxHealth = packet.ReadInt();
            int healthBlue = packet.ReadInt();

            List<bool> charmsData = new List<bool>();
            for (int charmNum = 1; charmNum <= 40; charmNum++)
            {
                charmsData.Add(packet.ReadBool());
            }
            int modamount = packet.ReadInt();
            for (int modNum = 1; modNum <= modamount; modNum++)
            {
                Log(username + " Has mod " + packet.ReadString() + "  " + modNum + "/" + modamount);
            }
            Log(username + " Has modding api ver " + packet.ReadString());
            int team = packet.ReadInt();
            string chat = packet.ReadString();
            bool pinenabled = packet.ReadBool();
            Vector3 pinposition = packet.ReadVector3();

            if (isHost)
            {
                foreach (Client client in Server.clients.Values)
                {
                    if (client.player == null) continue;
                    if (client.player.isHost)
                    {
                        Log("Another player tried to connect with a server DLL active! Disconnecting that player.");
                        ServerSend.DisconnectPlayer(fromClient);

                        return;
                    }
                }
            }

            Server.clients[fromClient].SendIntoGame(username, position, scale, currentClip, health, maxHealth, healthBlue, charmsData, isHost, team, chat, pinenabled, pinposition);
            try{
                foreach (Client client in Server.clients.Values)
                {
                    ServerSend.CreatePin(client.id, fromClient);
                }
                ServerSend.CreatePin(fromClient);
            }
            catch (Exception e) {
                Log("Server could not create pin :" + e.Message + " Object: " + e.Source);            
            }

            /*for (int i = 0; i < Enum.GetNames(typeof(TextureType)).Length; i++)
            {
                byte[] hash = packet.ReadBytes(20);
                
                Player player = Server.clients[fromClient].player;
                if (!player.textureHashes.Contains(hash))
                {
                    player.textureHashes.Add(hash);
                }
                
                if (!MultiplayerServer.textureCache.ContainsKey(hash))
                {
                    ServerSend.RequestTexture(fromClient, hash);
                }
            }*/

            bool otherplayer = false;
            if (Server.clients.Count > 1)
            {
                foreach (Client c in Server.clients.Values)
                {
                    if (c.player == null) continue;
                    if (c.player.activeScene == activeScene)
                        otherplayer = true;
                }
            }
            SceneChanged(fromClient, activeScene, otherplayer);
            
            Log($"{username} connected successfully and is now player {fromClient}.");
            if (fromClient != clientIdCheck)
            {
                Log($"Player \"{username}\" (ID: {fromClient}) has assumed the wrong client ID ({clientIdCheck}.");
            }
        }

        public static void HandleTextureFragment(byte fromClient, Packet packet)
        {
            if (!ServerSettings.CustomKnightIntegration) return;

            int textureLength = packet.ReadInt();
            if(textureLength > 20_000_000)
            {
                Log("Over 20mb really ? That's going to be a 'no from me'.");
                return;
            }

            byte[] texture = packet.ReadBytes(textureLength);
            
            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                byte[] computedHash = sha1.ComputeHash(texture);
                string hashStr = BitConverter.ToString(computedHash).Replace("-", string.Empty);
                string cacheDir = Path.Combine(Application.dataPath, "SkinCache");
                string filePath = Path.Combine(cacheDir, hashStr);

                if (MultiplayerServer.textureCache.ContainsKey(computedHash)) return;

                File.WriteAllBytes(filePath, texture);
                MultiplayerServer.textureCache[computedHash] = filePath;
            }
        }

        public static void HandleTextureRequest(byte fromClient, Packet packet)
        {
            if (!ServerSettings.CustomKnightIntegration) return;

            byte[] hash = packet.ReadBytes(20);

            Player player = Server.clients[fromClient].player;
            if (!player.textureHashes.Contains(hash))
            {
                player.textureHashes.Add(hash);
            }    
            
            if (MultiplayerServer.textureCache.ContainsKey(hash))
            {
                byte[] texture = File.ReadAllBytes(MultiplayerServer.textureCache[hash]);
                ServerSend.SendTexture(fromClient, hash, texture);
            }
        }
        
        public static void PlayerPosition(byte fromClient, Packet packet)
        {
            Vector3 position = packet.ReadVector3();

            Server.clients[fromClient].player.SetPosition(position);
        }

        public static void PlayerScale(byte fromClient, Packet packet)
        {
            Vector3 scale = packet.ReadVector3();
            
            Server.clients[fromClient].player.SetScale(scale);
        }
        
        public static void PlayerAnimation(byte fromClient, Packet packet)
        {
            string animation = packet.ReadString();
            
            Server.clients[fromClient].player.SetAnimation(animation);
        }

        public static void SceneChanged(byte fromClient, Packet packet)
        {
            string sceneName = packet.ReadString();
            bool host = Server.clients[fromClient].player.CurrentRoomSyncHost;
            string oldscene = Server.clients[fromClient].player.activeScene;
            Server.clients[fromClient].player.activeScene = sceneName;
            bool first = true;
            bool otherplayer = false;
            for (byte i = 1; i <= Server.MaxPlayers; i++)    
            {
                if (Server.clients[i].player != null && i != fromClient)
                {
                    if (Server.clients[i].player.activeScene == sceneName)
                    {
                        Log("Same Scene, Spawning Players Subsequent Pass");
                        Player iPlayer = Server.clients[i].player;
                        Player fromPlayer = Server.clients[fromClient].player;
                        ServerSend.SpawnPlayer(fromClient, iPlayer);
                        ServerSend.SpawnPlayer(i, fromPlayer);
                        otherplayer = true;
                    }
                    else
                    {

                        if (first && Server.clients[i].player.activeScene == oldscene && host)
                        {
                            Log("Different Scene, Destroying Players And Changing room host");
                            ServerSend.DestroyPlayer(i, fromClient, true);
                            first = false;
                        }
                        else
                        {
                            Log("Different Scene, Destroying Players");
                            ServerSend.DestroyPlayer(i, fromClient, false);
                        }
                        //ServerSend.DestroyPlayer(fromClient, i);
                    }
                }
            }
            if (!otherplayer && !host)
            {
                ServerSend.StartEnemySync(fromClient, true);
            }
            Server.clients[fromClient].player.CurrentRoomSyncHost = !otherplayer;
        }

        /// <summary>Initial scene load when joining the server for the first time.</summary>
        /// <param name="fromClient">The ID of the client who joined the server</param>
        /// <param name="sceneName">The name of the client's active scene when joining the server</param>
        public static void SceneChanged(byte fromClient, string sceneName, bool other)
        {
            Server.clients[fromClient].player.activeScene = sceneName;
            Server.clients[fromClient].player.CurrentRoomSyncHost = !other;
            if (!other)
            {
                ServerSend.StartEnemySync(fromClient, true);
            }
            for (byte i = 1; i <= Server.MaxPlayers; i++)
            {
                if (Server.clients[i].player != null && i != fromClient)
                {
                    if (Server.clients[i].player.activeScene == sceneName)
                    {
                        Log("Same Scene, Spawning Players First Pass");
                        ServerSend.SpawnPlayer(fromClient, Server.clients[i].player);
                        ServerSend.SpawnPlayer(Server.clients[i].player.id, Server.clients[fromClient].player);
                        Player iPlayer = Server.clients[i].player;
                        Player fromPlayer = Server.clients[fromClient].player;
                        ServerSend.SpawnPlayer(fromClient, iPlayer);
                        ServerSend.SpawnPlayer(i, fromPlayer);
                    }
                }
            }
        }

        public static void HealthUpdated(byte fromClient, Packet packet)
        {
            int currentHealth = packet.ReadInt();
            int currentMaxHealth = packet.ReadInt();
            int currentHealthBlue = packet.ReadInt();

            Log("From Client: " + currentHealth + " " + currentMaxHealth + " " + currentHealthBlue);
            
            Server.clients[fromClient].player.health = currentHealth;
            Server.clients[fromClient].player.maxHealth = currentMaxHealth;
            Server.clients[fromClient].player.healthBlue = currentHealthBlue;

            ServerSend.HealthUpdated(fromClient, currentHealth, currentMaxHealth, currentHealthBlue);
        }
        
        public static void CharmsUpdated(byte fromClient, Packet packet)
        {
            Log("Logging equipped charms for " + Server.clients[fromClient].player.username);
            for (int charmNum = 1; charmNum <= 40; charmNum++)
            {
                bool equippedCharm = packet.ReadBool();
                Server.clients[fromClient].player.SetAttr("equippedCharm_" + charmNum, equippedCharm);
                Log(Server.clients[fromClient].player.username + " Equipped Charm " + charmNum + " " + equippedCharm);
            }
            
            ServerSend.CharmsUpdated(fromClient, Server.clients[fromClient].player);
        }
        
        public static void PlayerDisconnected(byte fromClient, Packet packet)
        {
            int id = packet.ReadInt();

            UObject.Destroy(Server.clients[id].player.gameObject);
            Server.clients[id].player = null;
            Server.clients[id].Disconnect();
        }

        public static void Team(byte fromClient, Packet packet)
        {
            byte id = packet.ReadByte();
            int team = packet.ReadInt();

            ServerSend.Team(id, team);
        }

        public static void Chat(byte fromClient, Packet packet)
        {
            byte id = packet.ReadByte();
            string message = packet.ReadString();

            Log("Got Chat " + message + " From " + Server.clients[id].player.username);

            Server.clients[id].player.chat = message;

            ServerSend.Chat(id, message);
        }
        public static void LoadServerScene(byte fromClient, Packet packet)
        {
            string sceneName = packet.ReadString();

            GameManager.instance.StartCoroutine(LoadSceneRoutine());

            IEnumerator LoadSceneRoutine()
            {
                Scene scene = USceneManager.GetSceneByName(sceneName);
                if (!scene.isLoaded)
                {
                    AsyncOperation operation = USceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                    yield return new WaitWhile(() => !operation.isDone);
                }

                Scene loadedScene = USceneManager.GetSceneByName(sceneName);
                GameObject[] rootGOs = loadedScene.GetRootGameObjects();
                if (rootGOs != null)
                {
                    List<GameObject> enemies = new List<GameObject>();
                    foreach (GameObject rootGO in rootGOs)
                    {
                        List<GameObject> childEnemies = rootGO.FindChildEnemies();

                        foreach (GameObject enemy in childEnemies)
                        {
                            enemies.Add(enemy);
                        }
                    }

                    foreach (GameObject enemy in enemies)
                    {
                        var tracker = enemy.GetComponent<EnemyTracker>();
                        if (tracker)
                        {
                            tracker.playerIds.Add(fromClient);
                        }
                        else
                        {
                            tracker = enemy.AddComponent<EnemyTracker>();
                            ServerSend.SyncEnemy(fromClient, enemy.name, tracker.enemyId);
                            tracker.playerIds.Add(fromClient);
                        }

                        bool foundUnusedKey = false;
                        for (int i = 0; i <= Server.Enemies.Count; i++)
                        {
                            if (!Server.Enemies.Keys.Contains(i))
                            {
                                tracker.enemyId = i;
                                Server.Enemies.Add(i, tracker);
                                foundUnusedKey = true;
                                break;
                            }
                        }

                        if (!foundUnusedKey)
                        {
                            for (int i = Server.Enemies.Count + 1; i <= 99999; i++)
                            {
                                if (!Server.Enemies.Keys.Contains(i))
                                {
                                    tracker.enemyId = i;
                                    Server.Enemies.Add(i, tracker);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }


        public static void SyncEnemy(byte fromClient, Packet packet)
        {
            //if (!Server.clients[fromClient].player.isHost) return;
            byte toClient = packet.ReadByte();
            if ((Server.clients[fromClient].player.activeScene == Server.clients[toClient].player.activeScene)) return;

            string goName = packet.ReadString();
            int id = packet.ReadInt();

            ServerSend.SyncEnemy(toClient, goName, id);
        }
        
        public static void EnemyPosition(byte fromClient, Packet packet)
        {
            //if (!Server.clients[fromClient].player.isHost) return;
            byte toClient = packet.ReadByte();
            if (Server.clients[fromClient].player.activeScene == Server.clients[toClient].player.activeScene) return;

            Vector3 position = packet.ReadVector3();
            int id = packet.ReadInt();

            ServerSend.EnemyPosition(toClient, position, id);
        }

        public static void EnemyScale(byte fromClient, Packet packet)
        {
            //if (!Server.clients[fromClient].player.isHost) return;
            byte toClient = packet.ReadByte();
            if (Server.clients[fromClient].player.activeScene != Server.clients[toClient].player.activeScene) return;

            Vector3 scale = packet.ReadVector3();
            int id = packet.ReadInt();

            ServerSend.EnemyScale(toClient, scale, id);
        }
        
        public static void EnemyAnimation(byte fromClient, Packet packet)
        {
            //if (!Server.clients[fromClient].player.isHost) return;
            byte toClient = packet.ReadByte();
            if (Server.clients[fromClient].player.activeScene != Server.clients[toClient].player.activeScene) return;

            string animation = packet.ReadString();
            int id = packet.ReadInt();

            ServerSend.EnemyAnimation(toClient, animation, id);
        }

        public static void PlayerDataChange(byte fromClient, Packet packet)
        {
            PlayerDataTypes pdtype = (PlayerDataTypes)packet.ReadInt();
            string variable = packet.ReadString();
            Log(Server.clients[fromClient].player.username + " " + pdtype.ToString() + " " + variable);
            object obj = null;
            switch (pdtype)
            {
                case PlayerDataTypes.Bool:
                    obj = packet.ReadBool();
                    break;
                case PlayerDataTypes.Float:
                    obj = packet.ReadFloat();
                    break;
                case PlayerDataTypes.Int:
                    obj = packet.ReadInt();
                    break;
                case PlayerDataTypes.Other:
                    obj = packet.ReadString();
                    break;
                case PlayerDataTypes.String:
                    obj = packet.ReadString();
                    break;
                case PlayerDataTypes.Vector3:
                    obj = packet.ReadVector3();
                    break;
            }

            ServerSend.UpdatePlayerData(fromClient, pdtype, variable, obj);
        }

        public static void SendPD(byte fromClient, Packet packet)
        {
            Log("Got PD Request From " + Server.clients[fromClient].player.username);
            ServerSend.SendPlayerData(fromClient);
        }

        public static void StartPinSync(byte fromClient, Packet packet)
        {
            bool enabled = packet.ReadBool();
            ServerSend.StartPinSync(fromClient, enabled);
        }

        public static void PinPosition(byte fromclient, Packet packet)
        {
            Vector3 position = packet.ReadVector3();
            ServerSend.PinPosition(fromclient, position);
        }

        private static void Log(object message) => Modding.Logger.Log("[Server Handle] " + message);
    }
}
