using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModCommon.Util;
using MultiplayerClient.Canvas;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerClient
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance;
        public bool PvPEnabled;
        public bool TeamsEnabled;
        public bool SpectatorMode;

        public Dictionary<byte, PlayerManager> Players = new Dictionary<byte, PlayerManager>();

        // Loaded texture list, indexed by their hash. A texture can be shared by multiple players.
        public Dictionary<byte[], Texture2D> loadedTextures = new Dictionary<byte[], Texture2D>(new ByteArrayComparer());

        public byte MaxPlayers = 50;
        
        public GameObject playerPrefab;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != null)
            {
                Log("Instance already exists, destroying object.");
                Destroy(this);
            }
        }
        
        /// <summary>Spawns a player.</summary>
        /// <param name="id">The player's ID.</param>
        /// <param name="username">The player's username.</param>
        /// <param name="position">The player's starting position.</param>
        /// <param name="scale">The player's starting scale.</param>
        /// <param name="animation">The starting animation of the spawned player.</param>
        /// <param name="charmsData">List of bools containing charms equipped.</param>
        public void SpawnPlayer(byte id, string username, Vector3 position, Vector3 scale, string animation, List<bool> charmsData, int team)
        {
            Log("spawnpl");
            // Prevent duplication of same player, leaving one idle
            if (Players.ContainsKey(id))
            {
                Log("spawnpl2");
                DestroyPlayer(id);
            }

            Log("spawnpll2");
            GameObject player = Instantiate(playerPrefab);

            Log("spawnpl3");
            player.SetActive(true);
            player.SetActiveChildren(true);
            // This component needs to be enabled to run past Awake for whatever reason
            player.GetComponent<PlayerController>().enabled = true;

            Log("spawnpl4");

            player.transform.SetPosition2D(position);
            player.transform.localScale = scale;

            if (Instance.PvPEnabled)
            {
                Log("Enabling PvP Attributes");

                player.layer = 11;

                player.GetComponent<DamageHero>().enabled = true;
            }
            if (Instance.TeamsEnabled)
            {
                Log("Enabling Teams Attributes");
                if (Instance.PvPEnabled)
                {
                    if (team != Client.Instance.team)
                    {
                        player.layer = 11;

                        player.GetComponent<DamageHero>().enabled = true;
                    }
                    else
                    {
                        player.layer = 9;

                        player.GetComponent<DamageHero>().enabled = false;
                    }

                    switch (team)
                    {
                        case 1:
                            player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.red;
                            break;
                        case 2:
                            player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.blue;
                            break;
                        case 3:
                            player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.yellow;
                            break;
                        case 4:
                            player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.green;
                            break;
                        default:
                            player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.magenta;
                            break;
                    }
                }
            }

            player.GetComponent<tk2dSpriteAnimator>().Play(animation);
            
            GameObject nameObj = Instantiate(new GameObject("Username"), position + Vector3.up * 1.25f,
                Quaternion.identity);
            nameObj.transform.SetParent(player.transform);
            nameObj.transform.SetScaleX(0.25f);
            nameObj.transform.SetScaleY(0.25f);
            TextMeshPro nameText = nameObj.AddComponent<TextMeshPro>();
            nameText.text = username;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 24;
            nameText.outlineColor = Color.black;
            nameText.outlineWidth = 0.1f;
            nameObj.AddComponent<KeepWorldScalePositive>(); 
            GameObject chatObj = Instantiate(new GameObject("Chattext"), position + Vector3.up * 2f,
                 Quaternion.identity);
            chatObj.transform.SetParent(player.transform);
            chatObj.transform.SetScaleX(0.25f);
            chatObj.transform.SetScaleY(0.25f);
            TextMeshPro chatText = chatObj.AddComponent<TextMeshPro>();
            chatText.text = "Chat Text";
            chatText.alignment = TextAlignmentOptions.Center;
            chatText.fontSize = 24;
            chatText.outlineColor = Color.black;
            chatText.outlineWidth = 0.1f;
            chatObj.AddComponent<KeepWorldScalePositive>();

            DontDestroyOnLoad(player);

            PlayerManager playerManager = player.GetComponent<PlayerManager>();
            playerManager.id = id;
            playerManager.username = username;
            playerManager.chattext = chatText;
            for (int charmNum = 1; charmNum <= 40; charmNum++)
            {
                playerManager.SetAttr("equippedCharm_" + charmNum, charmsData[charmNum - 1]);
            }
            playerManager.team = team;
            Players.Add(id, playerManager);

            Log("Done Spawning Player " + id);
        }

        public void ReloadPlayerTextures(PlayerManager player)
        {
            foreach(var row in player.texHashes) {
                var hash = row.Key;
                var tt = row.Value;

                if(loadedTextures.ContainsKey(hash))
                {
                    // Texture already loaded : ezpz
                    player.textures[tt] = loadedTextures[hash];
                }
                else
                {
                    if(MultiplayerClient.textureCache.ContainsKey(hash))
                    {
                        // Texture not loaded but on disk : also ezpz
                        byte[] texBytes = File.ReadAllBytes(MultiplayerClient.textureCache[hash]);
                        Texture2D texture = new Texture2D(2, 2);
                        texture.LoadImage(texBytes);

                        loadedTextures[hash] = texture;
                        player.textures[tt] = texture;
                    }
                    else
                    {
                        // Ask the server for the texture and load it later...
                        ClientSend.RequestTexture(hash);
                    }
                }
            }

            if (player.textures.ContainsKey(TextureType.Knight))
            {
                Log("Knight tex length: " + player.textures[TextureType.Knight].EncodeToPNG().Length);
                var materialPropertyBlock = new MaterialPropertyBlock();
                player.GetComponent<MeshRenderer>().GetPropertyBlock(materialPropertyBlock);
                materialPropertyBlock.SetTexture("_MainTex", player.textures[TextureType.Knight]);
                player.GetComponent<MeshRenderer>().SetPropertyBlock(materialPropertyBlock); ;
            }
        }
        
        public void EnablePvP(bool enable)
        {
            Instance.PvPEnabled = enable;
            foreach (PlayerManager player in Players.Values)
            {
                player.gameObject.layer = enable ? 11 : 9;
                player.gameObject.GetComponent<DamageHero>().enabled = enable;
            }
            if (TeamsEnabled && enable == true)
            {
                foreach (PlayerManager player in Players.Values)
                {
                    if (player.team != Client.Instance.team)
                    {
                        player.gameObject.layer = 11;
                        player.gameObject.GetComponent<DamageHero>().enabled = true;
                    }
                    else
                    {
                        player.gameObject.layer = 9;
                        player.gameObject.GetComponent<DamageHero>().enabled = false;
                    }
                }
            }
        }

        public void EnableTeams(bool enable)
        {
            if (enable == true)
            {
                if (Client.Instance.team != 1 && Client.Instance.team != 2 && Client.Instance.team != 3 && Client.Instance.team != 4)
                {
                    Log("Team Was not an knowen amount");
                    Client.Instance.team = 1;
                }
                        HeroController.instance.gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.red;
                        ConnectionPanel.TeamButton.UpdateText("Red");
            }
            Instance.TeamsEnabled = enable;
            foreach (PlayerManager player in Players.Values)
            {
            if (enable == true)
            {
                    
            if (PvPEnabled && enable == true)
            {
                if (player.team != Client.Instance.team)
                {
                    player.gameObject.layer = 11;
                    player.gameObject.GetComponent<DamageHero>().enabled = true;
                }
                else
                {
                    player.gameObject.layer = 9;
                    player.gameObject.GetComponent<DamageHero>().enabled = false;
                }
            }
                    if (player.team != 1 && player.team != 2 && player.team != 3 && player.team != 4)
                    {
                        Log("Team Was not an knowen amount");
                        player.team = 1;
                    }
                    switch (player.team)
                {
                    case 1:
                        player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.red;
                        break;
                    case 2:
                        player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.blue;
                        break;
                    case 3:
                       player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.yellow;
                        break;
                    case 4:
                       player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.green;
                        break;
                    default:
                        player.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.magenta;
                        break;
                    }
                }

                if (PvPEnabled && enable == true)
                {
                    if (player.team != Client.Instance.team)
                    {
                        player.gameObject.layer = 11;
                        player.gameObject.GetComponent<DamageHero>().enabled = true;
                    }
                    else
                    {
                        player.gameObject.layer = 9;
                        player.gameObject.GetComponent<DamageHero>().enabled = false;
                    }
                }
            }
        }

        public void DestroyPlayer(byte playerId)
        {
            if(Players.ContainsKey(playerId))
            {
                Log("Destroying Player " + playerId);
                Destroy(Players[playerId].gameObject);
                Players.Remove(playerId);
            }
            else
            {
                Log("Was asked to destroy player " + playerId + " even though we don't have it. Ignoring.");
            }
        }

        public void DestroyAllPlayers()
        {
            List<byte> playerIds = new List<byte>(Players.Keys);
            foreach (byte playerId in playerIds)
            {
                DestroyPlayer(playerId);
            }
        }

        public void TeamPlayer(byte playerId, int team)
        {
            if (Players.ContainsKey(playerId))
            {
                Players[playerId].team = team;
                Log("Teaming Player " + playerId);
                if (Instance.TeamsEnabled)
                {
                    switch (team)
                    {
                        case 1:
                            Players[playerId].gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.red;
                            break;
                        case 2:
                            Players[playerId].gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.blue;
                            break;
                        case 3:
                            Players[playerId].gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.yellow;
                            break;
                        case 4:
                            Players[playerId].gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.green;
                            break;
                        default:
                            Players[playerId].gameObject.GetComponent<tk2dSpriteAnimator>().Sprite.color = Color.magenta;
                            break;
                    }
                }
            }
            else
            {
                Log("Was asked to team player " + playerId + " even though we don't have it. Ignoring.");
            }
        }
        public void Chat(byte playerId, string message)
        {
            if (Players.ContainsKey(playerId))
            {
                Players[playerId].chattext.text = message.ToString();
                Log("Chat from " + playerId + ":  " + message);
            }
            else
            {
                Log("Was asked to chat player " + playerId + " even though we don't have it. Ignoring.");
            }
        }

        private static void Log(object message) => Modding.Logger.Log("[Session Manager] " + message);
    }
}
