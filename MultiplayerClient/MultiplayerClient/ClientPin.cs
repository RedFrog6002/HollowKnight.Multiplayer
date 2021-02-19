using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;

namespace MultiplayerClient
{
    public class ClientPin : MonoBehaviour
    {
        private static bool MapOn = false;

        public static void SetUpStaticHooks()
        {
            On.GameMap.SetupMapMarkers += EnableMarkerStatic;
            On.GameMap.DisableMarkers += DisableMarkerStatic;
        }

        public static ClientPin CreatePin(string username, bool enabled, Vector3 position)
        {
            MultiplayerClient.Instance.Log("Pin " + (GameManager.instance.gameMap == null) + (SessionManager.Instance.pinPrefab == null));
            GameObject gameObject = Instantiate(
                SessionManager.Instance.pinPrefab,
                GameManager.instance.gameMap.transform);

            MultiplayerClient.Instance.Log("1");

            gameObject.transform.localPosition = position;
            gameObject.name += username;

            MultiplayerClient.Instance.Log("2");

            /*GameObject nameObj = UnityEngine.Object.Instantiate(
                new GameObject("Username" + gameObject.name),
                GameManager.instance.gameMap.transform);*/

            // copied and edited from https://github.com/SFGrenade/AdditionalMaps/blob/master/AdditionalMaps.cs
            var nameObj = GameObject.Instantiate(GameManager.instance.gameMap.GetComponent<GameMap>().areaCliffs.transform.GetChild(6).GetChild(0).gameObject, gameObject.transform);

            //nameObj.transform.localPosition = new Vector3(7f, -1.5f, nameObj.transform.localPosition.z);
            nameObj.transform.localPosition = new Vector3(position.x, position.y, nameObj.transform.localPosition.z);
            nameObj.GetComponent<SetTextMeshProGameText>().convName = "Player_Name_" + username;
            var rectT = nameObj.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(rectT.sizeDelta.x + 1, rectT.sizeDelta.y);

            MultiplayerClient.Instance.Log("3");

            GameObject.DontDestroyOnLoad(gameObject);
            GameObject.DontDestroyOnLoad(nameObj);
            //nameObj.transform.localPosition = position + Vector3.up;

            MultiplayerClient.Instance.Log("4");

            /*TextMeshPro nameText = nameObj.AddComponent<TextMeshPro>();
            nameText.text = username;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 10;
            nameText.color = Color.blue;*/

            MultiplayerClient.Instance.Log("5");
            MultiplayerClient.Instance.Log("enabled = " + enabled + MapOn + SessionManager.Instance.RecievePins + (enabled && MapOn && SessionManager.Instance.RecievePins));

            nameObj.SetActive(enabled && MapOn && SessionManager.Instance.RecievePins);
            gameObject.SetActive(enabled && MapOn && SessionManager.Instance.RecievePins);

            MultiplayerClient.Instance.Log("6");

            ClientPin pin = gameObject.AddComponent<ClientPin>();
            pin.isEnabled = enabled;
            pin.Position = position;
            pin.username = nameObj;

            MultiplayerClient.Instance.Log("7");

            On.GameMap.SetupMapMarkers += pin.EnableMarker;
            On.GameMap.DisableMarkers += pin.DisableMarker;

            MultiplayerClient.Instance.Log("8");

            return pin;
        }

        private void FixedUpdate()
        {
            if (Position != transform.localPosition)
            {
                transform.localPosition = Position;
                username.transform.localPosition = new Vector3(Position.x, Position.y, username.transform.localPosition.z);
            }
        }

        private void OnDestroy()
        {
            On.GameMap.SetupMapMarkers -= EnableMarker;
            On.GameMap.DisableMarkers -= DisableMarker;
        }

        private void DisableMarker(On.GameMap.orig_DisableMarkers orig, GameMap self)
        {
            orig(self);
            gameObject.SetActive(false);
            username.SetActive(false);
        }

        private void EnableMarker(On.GameMap.orig_SetupMapMarkers orig, GameMap self)
        {
            orig(self);
            if (isEnabled && SessionManager.Instance.RecievePins)
            {
                gameObject.SetActive(true);
                username.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
                username.SetActive(false);
            }
        }

        private static void DisableMarkerStatic(On.GameMap.orig_DisableMarkers orig, GameMap self)
        {
            orig(self);
            MapOn = false;
            MultiplayerClient.Instance.Log("map off");
        }

        private static void EnableMarkerStatic(On.GameMap.orig_SetupMapMarkers orig, GameMap self)
        {
            orig(self);
            MapOn = true;
            MultiplayerClient.Instance.Log("map on");
        }

        public bool isEnabled = false;
        public Vector3 Position = Vector3.zero;
        private GameObject username;
    }
    public class HeroPin : MonoBehaviour
    {
        private void FixedUpdate()
        {
            if (Position != transform.localPosition)
            {
                Position = transform.localPosition;
                if (SessionManager.Instance.SendPins)
                    ClientSend.SendPinPos(Position);
            }
        }
        public Vector3 Position = Vector3.zero;
        public static HeroPin instance;
    }
}
