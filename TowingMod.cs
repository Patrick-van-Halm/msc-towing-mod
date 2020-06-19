using MSCLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System.Collections;

namespace TowingMod
{
    public class TowingMod : Mod
    {
        public override string ID => "TowingMod"; //Your mod ID (unique)
        public override string Name => "Towing Mod"; //You mod name
        public override string Author => "Polyoxis"; //Your Username
        public override string Version => "1.0"; //Version

        // Set this to true if you will be load custom assets from Assets folder.
        // This will create subfolder in Assets folder for your mod.
        public override bool UseAssetsFolder => false;

        private Keybind towingMenuKey = new Keybind("OpenTowingMenu", "Open Towing Menu", KeyCode.F10);
        private Keybind towingAcceptKey = new Keybind("AcceptTowingSpot", "Accept towing spot", KeyCode.Return);
        private List<GameObject> cars = new List<GameObject>();
        private List<GameObject> proxCars = new List<GameObject>();
        private event Action<GameObject> TowVehicle;
        private string lastPlayerLocation = "";
        private GameObject player;
        private Camera playerCam;
        private GameObject sleepEyes;
        private Animation eyesAnimation;
        private GameObject towedVehicle;

        private string[] blacklistedCars = new string[]
        {
            "Bus",
            "FLATBED",
            "FITTAN"
        };

        private bool toggleTowingMenu = false;
        private bool toggleTowingError = false;
        private bool isLoaded = false;
        private bool isAcceptingSpot = false;
        private bool isAcceptingPayment = false;

        private string errorMessage = "";
        private static readonly int PriceTowPerMeter = 5;
        private int paymentPrice = 0;

        private Vector3 towingSpot = new Vector3();

        public override void OnGUI()
        {
            if (isAcceptingSpot)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 40, 300, 20), $"Currently towing: {towedVehicle.name.Substring(0, towedVehicle.name.IndexOf('('))} [{towingAcceptKey.Key.ToString()} TO ACCEPT PLACEMENT]", style);
            }

            if (toggleTowingMenu)
            {
                GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 150, 200, 300), "Towing menu");

                if (lastPlayerLocation != player.transform.position.ToString("F0"))
                {
                    proxCars.Clear();
                    lastPlayerLocation = player.transform.position.ToString("F0");
                    foreach (GameObject car in cars)
                    {
                        if (Vector3.Distance(car.transform.position, player.transform.position) < 10)
                        {
                            proxCars.Add(car);
                        }
                    }
                }

                foreach (GameObject car in proxCars)
                {
                    bool pressed = GUI.Button(new Rect(
                        Screen.width / 2 - 80,
                        Screen.height / 2 - 130 + ((220 / (proxCars.Count + 1)) + 30 * proxCars.IndexOf(car)),
                        160,
                        20
                    ),
                    $"Tow {car.name.Substring(0, car.name.IndexOf('('))}");

                    if (pressed)
                    {
                        toggleTowingMenu = false;
                        paymentPrice = 0;
                        UnlockMouse(false);
                        TowVehicle?.Invoke(car);
                    }
                }
            }

            if (toggleTowingError)
            {
                GUI.Box(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), "Towing Error");
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width / 2 - 130, Screen.height / 2 - 10, 260, 20), errorMessage, style);
                bool pressed = GUI.Button(new Rect(Screen.width / 2 - 130, Screen.height / 2 + 20, 260, 20), "Close");

                if (pressed)
                {
                    UnlockMouse(false);
                    ResetValues();
                    toggleTowingError = false;
                }
            }

            if (isAcceptingPayment)
            {
                GUI.Box(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), "Towing Payment");
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width / 2 - 130, Screen.height / 2 - 10, 260, 20), $"The payment for towing is: {paymentPrice} mk", style);
                bool pressedAccept = GUI.Button(new Rect(Screen.width / 2 - 130, Screen.height / 2 + 20, 120, 20), "Accept");
                bool pressedReject = GUI.Button(new Rect(Screen.width / 2 + 10, Screen.height / 2 + 20, 120, 20), "Reject");

                if (pressedAccept)
                {
                    FsmVariables.GlobalVariables.FindFsmFloat("PlayerMoney").Value = FsmVariables.GlobalVariables.FindFsmFloat("PlayerMoney").Value - paymentPrice;
                    UnlockMouse(false);
                    TowVehicleToPosition();
                    isAcceptingPayment = false;
                    ResetValues();
                }

                if (pressedReject)
                {
                    UnlockMouse(false);
                    isAcceptingPayment = false;
                    ResetValues();
                }
            }
        }

        private void ResetValues()
        {
            towedVehicle = null;
            paymentPrice = 0;
            towingSpot = new Vector3();
        }

        private void TowVehicleToPosition()
        {
            towedVehicle.transform.position = towingSpot;
            towedVehicle.transform.rotation = Quaternion.identity;
        }

        private void UnlockMouse(bool isUnlocked)
        {
            FsmVariables.GlobalVariables.FindFsmBool("PlayerInMenu").Value = isUnlocked;
        }

        public override void OnLoad()
        {
            Keybind.Add(this, towingMenuKey);
            Keybind.Add(this, towingAcceptKey);

            ModConsole.Print("Towing Mod has been loaded!");
            
            foreach(CarDynamics car in GameObject.FindObjectsOfType<CarDynamics>())
            {
                if (!blacklistedCars.Contains(car.name))
                {
                    cars.Add(car.gameObject);
                }
            }

            player = GameObject.Find("PLAYER");
            playerCam = player.transform.Find("Pivot/AnimPivot/Camera/FPSCamera/FPSCamera").gameObject.GetComponent<Camera>();
            sleepEyes = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera/FPSCamera/SleepEyes");
            eyesAnimation = this.sleepEyes.GetComponent<Animation>();
            TowVehicle += TowingMod_TowVehicle;
            isLoaded = true;
        }

        private void TowingMod_TowVehicle(GameObject car)
        {
            isAcceptingSpot = true;
            towedVehicle = car;
        }

        public override void Update()
        {
            if (!isLoaded) return;

            if (towingMenuKey.GetKeybindUp())
            {
                toggleTowingMenu = !toggleTowingMenu;
                UnlockMouse(toggleTowingMenu);
            }

            if (isAcceptingSpot)
            {
                Ray ray = playerCam.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if(lastPlayerLocation != player.transform.position.ToString("F0"))
                {
                    lastPlayerLocation = player.transform.position.ToString("F0");
                    if (Vector3.Distance(towedVehicle.transform.position, player.transform.position) > 10)
                    {
                        UnlockMouse(true);
                        errorMessage = "You went to far from the vehicle.";
                        isAcceptingSpot = false;
                        toggleTowingError = true;
                    }
                }

                if(Physics.Raycast(ray, out hit, 10f))
                {
                    if (towingAcceptKey.GetKeybindUp())
                    {
                        paymentPrice = PriceTowPerMeter * (int)Vector3.Distance(new Vector3(hit.point.x, hit.point.y + 2, hit.point.z), towedVehicle.transform.position);
                        towingSpot = new Vector3(hit.point.x, hit.point.y + 2, hit.point.z);

                        UnlockMouse(true);
                        isAcceptingPayment = true;
                        isAcceptingSpot = false;
                    }
                }
            }
        }
    }
}
