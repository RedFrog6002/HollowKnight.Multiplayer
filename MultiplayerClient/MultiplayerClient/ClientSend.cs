using ModCommon.Util;
using UnityEngine;
using MultiplayerClient.Canvas;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MultiplayerClient
{
    public class ClientSend : MonoBehaviour
    {
        /// <summary>Sends a packet to the server via TCP.</summary>
        /// <param name="packet">The packet to send to the sever.</param>
        private static void SendTCPData(Packet packet)
        {
            packet.WriteLength();
            Client.Instance.tcp.SendData(packet);
        }

        /// <summary>Sends a packet to the server via UDP.</summary>
        /// <param name="packet">The packet to send to the sever.</param>
        private static void SendUDPData(Packet packet)
        {
            packet.WriteLength();
            Client.Instance.udp.SendData(packet);
        }

        #region Player Packets

        /// <summary>Lets the server know that the welcome message was received.</summary>
        public static void WelcomeReceived(/*List<byte[]> textureHashes, */bool isHost)
        {
            using (Packet packet = new Packet((int)ClientPackets.WelcomeReceived))
            {
                Transform heroTransform = HeroController.instance.gameObject.transform;

                packet.Write(Client.Instance.myId);
                packet.Write(MultiplayerClient.settings.username);
                packet.Write(isHost);
                packet.Write(HeroController.instance.GetComponent<tk2dSpriteAnimator>().CurrentClip.name);
                packet.Write(PlayerManager.Instance.activeScene);
                packet.Write(heroTransform.position);
                packet.Write(heroTransform.localScale);
                packet.Write(PlayerData.instance.health);
                packet.Write(PlayerData.instance.maxHealth);
                packet.Write(PlayerData.instance.healthBlue);

                for (int charmNum = 1; charmNum <= 40; charmNum++)
                {
                    packet.Write(PlayerData.instance.GetAttr<PlayerData, bool>("equippedCharm_" + charmNum));
                }

                // stolen from https://github.com/fifty-six/HollowKnight.Modding/blob/master/Assembly-CSharp/ModLoader.cs

                string path = string.Empty;
                if (SystemInfo.operatingSystem.Contains("Windows"))
                {
                    path = Application.dataPath + "\\Managed\\Mods";
                }
                else if (SystemInfo.operatingSystem.Contains("Mac"))
                {
                    path = Application.dataPath + "/Resources/Data/Managed/Mods/";
                }
                else if (SystemInfo.operatingSystem.Contains("Linux"))
                {
                    path = Application.dataPath + "/Managed/Mods";
                }

                string[] modPaths = Directory.GetFiles(path, "*.dll");

                packet.Write(modPaths.Length);
                foreach (string modPath in modPaths)
                {
                    string sendtext = Path.GetFileNameWithoutExtension(modPath);
                    try
                    {
                        using (FileStream fs = new FileStream(modPath, FileMode.Open))
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (SHA1Managed sha1 = new SHA1Managed())
                            {
                                byte[] hash = sha1.ComputeHash(bs);
                                StringBuilder formatted = new StringBuilder(2 * hash.Length);
                                foreach (byte b in hash)
                                {
                                    formatted.AppendFormat("{0:X2}", b);
                                }
                                sendtext += "   Sha1 hash: " + formatted;
                            }
                        }
                    }
                    catch
                    {
                        sendtext += "   Failed to get Sha1 hash";
                    }
                    packet.Write(sendtext);
                }
                packet.Write(Modding.ModHooks.Instance.ModVersion);

                packet.Write(Client.Instance.team);
                packet.Write(MultiplayerClient.Instance.herochat.text);
                packet.Write(SessionManager.Instance.SendPins);
                packet.Write(HeroPin.instance.Position);

                /*foreach (var hash in textureHashes)
                {
                    packet.Write(hash);
                }*/

                Log("Welcome Received Packet Length: " + packet.Length());

                SendTCPData(packet);
            }
        }

        public static void RequestTexture(byte[] hash)
        {
            using (Packet packet = new Packet((int)ClientPackets.TextureRequest))
            {
                packet.Write(hash);
                SendTCPData(packet);
            }
        }

        public static void SendTexture(byte[] texture)
        {
            using (Packet packet = new Packet((int) ClientPackets.TextureFragment))
            {
                // It's really that easy
                packet.Write(texture.Length);
                packet.Write(texture);
                SendTCPData(packet);
            }
        }
        
        public static void PlayerPosition(Vector3 position)
        {
            using (Packet packet = new Packet((int) ClientPackets.PlayerPosition))
            {
                packet.Write(position);
                
                SendUDPData(packet);
            }
        }
        
        public static void PlayerScale(Vector3 scale)
        {
            using (Packet packet = new Packet((int) ClientPackets.PlayerScale))
            {
                packet.Write(scale);
                
                SendUDPData(packet);
            }
        }

        public static void PlayerAnimation(string animation)
        {
            using (Packet packet = new Packet((int) ClientPackets.PlayerAnimation))
            {
                packet.Write(animation);
                
                SendUDPData(packet);
            }
        }

        public static void SceneChanged(string sceneName, bool otherplayer)
        {
            using (Packet packet = new Packet((int) ClientPackets.SceneChanged))
            {
                packet.Write(sceneName);
                packet.Write(otherplayer);

                SendTCPData(packet);
            }
        }

        public static void SceneChanged(string sceneName)
        {
            using (Packet packet = new Packet((int)ClientPackets.SceneChanged))
            {
                packet.Write(sceneName);
                packet.Write(false);

                SendTCPData(packet);
            }
        }

        public static void HealthUpdated(int currentHealth, int currentMaxHealth, int currentHealthBlue)
        {
            using (Packet packet = new Packet((int) ClientPackets.HealthUpdated))
            {
                packet.Write(currentHealth);
                packet.Write(currentMaxHealth);
                packet.Write(currentHealthBlue);

                SendTCPData(packet);
            }
        }
        
        public static void CharmsUpdated(PlayerData pd)
        {
            using (Packet packet = new Packet((int) ClientPackets.CharmsUpdated))
            {
                for (int i = 1; i <= 40; i++)
                {
                    packet.Write(pd.GetBool("equippedCharm_" + i));
                }

                SendTCPData(packet);
            }
        }
        
        public static void PlayerDisconnected(int id)
        {
            using (Packet packet = new Packet((int) ClientPackets.PlayerDisconnected))
            {
                packet.Write(id);

                SendTCPData(packet);
            }
        }
        public static void Team(byte id, int team)
        {
            using (Packet packet = new Packet((int)ClientPackets.Team))
            {
                packet.Write(id);
                packet.Write(team);

                SendTCPData(packet);
            }
        }
        public static void Chat(byte id, string message)
        {
            using (Packet packet = new Packet((int)ClientPackets.Chat))
            {
                packet.Write(id);
                packet.Write(message);

                SendTCPData(packet);
            }
        }
        public static void SendPDChange(PlayerDataTypes pdtype, string variable, object obj)
        {
            try
            {
                if (Client.Instance.isConnected && SessionManager.Instance.WSyncClientEnabled)
                {
                    using (Packet packet = new Packet((int)ClientPackets.PlayerDataChange))
                    {
                        packet.Write((int)pdtype);
                        packet.Write(variable);
                        switch (pdtype)
                        {
                            case PlayerDataTypes.Bool:
                                packet.Write((bool)obj);
                                break;
                            case PlayerDataTypes.Float:
                                packet.Write((float)obj);
                                break;
                            case PlayerDataTypes.Int:
                                packet.Write((int)obj);
                                break;
                            case PlayerDataTypes.Other:
                                packet.Write((string)obj);
                                break;
                            case PlayerDataTypes.String:
                                packet.Write((string)obj);
                                break;
                            case PlayerDataTypes.Vector3:
                                packet.Write((Vector3)obj);
                                break;
                        }
                        SendTCPData(packet);
                    }
                }
            }
            catch { }
        }
        public static void DownloadSave()
        {
            Log("Requesting Host PD");
            using (Packet packet = new Packet((int)ClientPackets.RequestWorldDownload))
            {
                SendTCPData(packet);
            }
        }

        public static void DoSyncPins(bool b)
        {
            Log("Enable Sync Pins " + b);
            SessionManager.Instance.SendPins = b;
            try
            {
                if (Client.Instance.isConnected)
                {
                    using (Packet packet = new Packet((int)ClientPackets.StartPinSync))
                    {
                        packet.Write(b);
                        SendTCPData(packet);
                    }
                }
            }
            catch { }
        }

        public static void SendPinPos(Vector3 pos)
        {
            try
            {
                if (Client.Instance.isConnected)
                {
                    using (Packet packet = new Packet((int)ClientPackets.PinPosition))
                    {
                        packet.Write(pos);
                        SendTCPData(packet);
                    }
                }
            }
            catch { }
        }

        #endregion Player Packets

        #region Enemy Packets

        public static void SyncEnemy(byte toClient, string goName, int id)
        {
            using (Packet packet = new Packet((int) ClientPackets.SyncEnemy))
            {
                packet.Write(toClient);
                packet.Write(goName);
                packet.Write(id);

                SendTCPData(packet);
            }
        }
        
        public static void EnemyPosition(byte toClient, Vector3 position, int id)
        {
            using (Packet packet = new Packet((int) ClientPackets.EnemyPosition))
            {
                packet.Write(toClient);
                packet.Write(position);
                packet.Write(id);

                SendUDPData(packet);
            }
        }
        
        public static void EnemyScale(byte toClient, Vector3 scale, int id)
        {
            using (Packet packet = new Packet((int) ClientPackets.EnemyScale))
            {
                packet.Write(toClient);
                packet.Write(scale);
                packet.Write(id);

                SendUDPData(packet);
            }
        }
        
        public static void EnemyAnimation(byte toClient, string clipName, int id)
        {
            using (Packet packet = new Packet((int) ClientPackets.EnemyAnimation))
            {
                packet.Write(toClient);
                packet.Write(clipName);
                
                SendUDPData(packet);
            }
        }
        
        # endregion Enemy Packets
        
        private static void Log(object message) => Modding.Logger.Log("[Client Send] " + message);
    }
}
