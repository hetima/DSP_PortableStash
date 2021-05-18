using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;


namespace DSPPortableStash
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.0")]
    public class PortableStash : BaseUnityPlugin
    {
        public const string __NAME__ = "PortableStash";
        public const string __GUID__ = "com.hetima.dsp." + __NAME__;
        internal static int _uiCount = 0;

        new internal static ManualLogSource Logger;
        void Awake()
        {
            Logger = base.Logger;
            //Logger.LogInfo("Awake");

            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }


        public static int StashIdOnPlanet(PlanetFactory pf)
        {
            if (pf == null) return 0;

            InserterComponent[] inserterPool = pf.factorySystem.inserterPool;
            List<int> candidates = new List<int>();
            for (int i = 1; i < pf.factorySystem.inserterCursor; i++)
            {
                InserterComponent ic=inserterPool[i];
                int pickEntityId = ic.pickTarget;
                int insEntityId = ic.insertTarget;
                int storageId = 0;
                if (ic.filter > 0) continue; //フィルター設定しているものは無視

                if (pickEntityId != 0 && insEntityId == 0)
                {
                    storageId = pf.entityPool[pickEntityId].storageId;

                }
                else if (pickEntityId == 0 && insEntityId != 0)
                {
                    storageId = pf.entityPool[insEntityId].storageId;
                }
                if (storageId > 0)
                {
                    if (candidates.Contains(storageId))
                    {
                        return storageId;
                    }
                    else
                    {
                        candidates.Add(storageId);
                    }
                }
            }
            return 0;
        }
        public static int StashEntityIdOnPlanet(PlanetFactory pf)
        {
            if (pf == null) return 0;

            InserterComponent[] inserterPool = pf.factorySystem.inserterPool;
            List<int> candidates = new List<int>();
            for (int i = 1; i < pf.factorySystem.inserterCursor; i++)
            {
                InserterComponent ic = inserterPool[i];
                int pickEntityId = ic.pickTarget;
                int insEntityId = ic.insertTarget;
                int storageId = 0;
                if (ic.filter > 0) continue; //フィルター設定しているものは無視

                if (pickEntityId != 0 && insEntityId == 0)
                {
                    if (pf.entityPool[pickEntityId].storageId > 0)
                    {
                        storageId = pickEntityId;
                    }
                }
                else if (pickEntityId == 0 && insEntityId != 0)
                {
                    if (pf.entityPool[insEntityId].storageId > 0)
                    {
                        storageId = insEntityId;
                    }
                }
                if (storageId > 0)
                {
                    if (candidates.Contains(storageId))
                    {
                        return storageId;
                    }
                    else
                    {
                        candidates.Add(storageId);
                    }
                }
            }
            return 0;
        }

        public static void OpenStorageEntityId(int entityId)
        {
            var player = GameMain.mainPlayer;
            if (entityId > 0 && player != null)
            {
                player.controller.actionInspect.SetInspectee(EObjectType.Entity, entityId);
            }
        }


        static class Patch
        {
            internal static bool _initialized = false;
            internal static IconButton _stashBtn = null;
            internal static IconButton _upBtn = null;
            internal static IconButton _downBtn = null;

            internal static List<int> _lastOpenedStashStack = null;
            internal static int _lastOpenedPlanetId = 0;


            [HarmonyPrefix, HarmonyPatch(typeof(GameMain), "Begin")]
            public static void GameMain_Begin_Prefix()
            {
                if (!_initialized)
                {
                    float posY = -72;
                    RectTransform parent = AccessTools.FieldRefAccess<UIStorageWindow, RectTransform>(UIRoot.instance.uiGame.storageWindow, "windowTrans");

                    _upBtn = IconButton.MakeIconButton(parent, IconButton.UpSprite(), 21, posY);
                    if (_upBtn == null) return;
                    _upBtn.uiButton.onClick += OnUpButtonClick;
                    _upBtn.uiButton.onRightClick += OnUpButtonRightClick;
                    posY -= 24;

                    _downBtn = IconButton.MakeIconButton(parent, IconButton.DownSprite(), 21, posY);
                    if (_downBtn == null) return;
                    _downBtn.uiButton.onClick += OnDownButtonClick;
                    _downBtn.uiButton.onRightClick += OnDownButtonRightClick;

                    parent = AccessTools.FieldRefAccess<UIStorageGrid, RectTransform>(UIRoot.instance.uiGame.inventory, "rectTrans");
                    _stashBtn = IconButton.MakeIconButton(parent, IconButton.StashSprite(), 132, 8);
                    if (_stashBtn == null) return;
                    _stashBtn.uiButton.onClick += OnStashButtonClick;

                    _initialized = true;
                }
            }


            [HarmonyPostfix, HarmonyPatch(typeof(PlayerAction_Inspect), "GetObjectSelectDistance"), HarmonyPriority(Priority.VeryLow)]
            public static void GetObjectSelectDistance_Patch_Postfix(ref float __result, EObjectType objType, int objid)
            {
                //UIStorageWindow が閉じないように距離をごまかす
                if (objType == EObjectType.Entity && GameMain.mainPlayer.factory.planetId == _lastOpenedPlanetId)
                {
                    if (_lastOpenedStashStack.Contains(objid))
                    {
                        __result = 9999f;
                    }
                }
            }


            [HarmonyPostfix, HarmonyPatch(typeof(UIStorageWindow), "_OnUpdate")]
            public static void UIStorageWindow__OnUpdate_Postfix(UIStorageWindow __instance)
            {
                if (!_initialized || __instance.factoryStorage == null) return;
                StorageComponent storageComponent = __instance.factoryStorage.storagePool[__instance.storageId];
                if (storageComponent == null || storageComponent.id != __instance.storageId)
                {
                    return;
                }

                _upBtn.SetEnabled(storageComponent.next > 0);
                _downBtn.SetEnabled(storageComponent.previous > 0);

            }

            public static void OnUpButtonClick(int obj)
            {
                UIStorageWindow win = UIRoot.instance.uiGame.storageWindow;
                StorageComponent storageComponent = win.factoryStorage.storagePool[win.storageId];
                if (storageComponent == null || storageComponent.id != win.storageId) return;
                if (storageComponent.next > 0)
                {
                    StorageComponent target = win.factoryStorage.storagePool[storageComponent.next];
                    OpenStorageEntityId(target.entityId);
                }
            }
            public static void OnUpButtonRightClick(int obj)
            {
                UIStorageWindow win = UIRoot.instance.uiGame.storageWindow;
                StorageComponent storageComponent = win.factoryStorage.storagePool[win.storageId];
                if (storageComponent == null || storageComponent.id != win.storageId) return;
                if (storageComponent.next > 0)
                {
                    StorageComponent target = storageComponent.topStorage;
                    OpenStorageEntityId(target.entityId);
                }
            }
            public static void OnDownButtonClick(int obj)
            {
                UIStorageWindow win = UIRoot.instance.uiGame.storageWindow;
                StorageComponent storageComponent = win.factoryStorage.storagePool[win.storageId];
                if (storageComponent == null || storageComponent.id != win.storageId) return;
                if (storageComponent.previous > 0)
                {
                    StorageComponent target = win.factoryStorage.storagePool[storageComponent.previous];
                    OpenStorageEntityId(target.entityId);
                }
            }
            public static void OnDownButtonRightClick(int obj)
            {
                UIStorageWindow win = UIRoot.instance.uiGame.storageWindow;
                StorageComponent storageComponent = win.factoryStorage.storagePool[win.storageId];
                if (storageComponent == null || storageComponent.id != win.storageId) return;
                if (storageComponent.previous > 0)
                {
                    StorageComponent target = storageComponent.bottomStorage;
                    OpenStorageEntityId(target.entityId);
                }
            }
            public static void OnStashButtonClick(int obj)
            {
                PlanetFactory pf = GameMain.mainPlayer.factory;

                int storageEntityId = StashEntityIdOnPlanet(pf);
                if (storageEntityId > 0)
                {
                    int storageId = pf.entityPool[storageEntityId].storageId;
                    if (storageId > 0)
                    {
                        List<int> stack = new List<int>();
                        StorageComponent target = pf.factoryStorage.storagePool[storageId];
                        StorageComponent sibling;
                        for (sibling = target.topStorage; sibling != null; sibling = sibling.previousStorage)
                        {
                            stack.Add(sibling.entityId);
                        }
                        _lastOpenedPlanetId = pf.planetId;
                        _lastOpenedStashStack = stack;
                        OpenStorageEntityId(storageEntityId);
                    }
                }
                else
                {
                    UIRealtimeTip.Popup("Stash not found", false, 0);
                }
            }

        }

        public class IconButton
        {

            public static IconButton MakeIconButton(Transform parent, Sprite sprite, float posX = 0, float posY = 0, bool right = false, bool bottom = false)
            {

                GameObject go = GameObject.Find("UI Root/Overlay Canvas/In Game/Research Queue/pause");
                if (go == null) return null;
                UIButton btn = MakeGameObject<UIButton>(parent, go, posX, posY, 0, 0, right, bottom);
                if (btn == null) return null;

                var icon = btn.gameObject.transform.Find("icon");
                if (sprite != null) icon.GetComponent<Image>().sprite = sprite;
                icon.localScale = new Vector3(1.6f, 1.6f, 1.6f);

                btn.gameObject.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                btn.tips.offset = new Vector2(0, -10);
                btn.tips.corner = 0;
                btn.tips.delay = 0.5f;
                btn.tips.tipText = "";
                btn.tips.tipTitle = "";

                IconButton result = new IconButton(btn);
                return result;
            }


            //LoadImage() 使うには UnityEngine.ImageConversionModule.dll を参照に追加する
            public static Sprite UpSprite()
            {
                Texture2D tex = new Texture2D(2, 2);
                byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0xFA, 0x37, 0xEA, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0B, 0x13, 0x00, 0x00, 0x0B, 0x13, 0x01, 0x00, 0x9A, 0x9C, 0x18, 0x00, 0x00, 0x00, 0x5E, 0x49, 0x44, 0x41, 0x54, 0x28, 0x91, 0x63, 0xFC, 0xCF, 0x80, 0x1F, 0x30, 0x11, 0x90, 0xA7, 0x81, 0x82, 0x7A, 0x86, 0x7A, 0x34, 0x91, 0xFF, 0xC8, 0xB0, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xD7, 0x23, 0x8B, 0xA1, 0x4B, 0xFF, 0xFE, 0xFF, 0x1B, 0x55, 0x09, 0xBA, 0x74, 0xF8, 0xFF, 0x70, 0x54, 0x25, 0xE8, 0xD2, 0x0C, 0xFF, 0x19, 0x50, 0x95, 0x20, 0x4B, 0x87, 0xC1, 0x4D, 0x0B, 0x43, 0x28, 0x61, 0x81, 0xBA, 0xF5, 0x0F, 0x43, 0x34, 0xC3, 0x2A, 0xB8, 0xCB, 0x57, 0x31, 0x30, 0x30, 0x2C, 0x45, 0xF5, 0x85, 0x16, 0x8A, 0x6F, 0x90, 0x44, 0x18, 0x07, 0x41, 0x5C, 0x00, 0x00, 0xCD, 0xB5, 0x7F, 0x21, 0x55, 0x9C, 0x60, 0xCD, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
                tex.LoadImage(pngBytes);
                return Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0f, 0f));
            }
            public static Sprite DownSprite()
            {
                Texture2D tex = new Texture2D(2, 2);
                byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0xFA, 0x37, 0xEA, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0B, 0x13, 0x00, 0x00, 0x0B, 0x13, 0x01, 0x00, 0x9A, 0x9C, 0x18, 0x00, 0x00, 0x00, 0x66, 0x49, 0x44, 0x41, 0x54, 0x28, 0x91, 0xCD, 0x91, 0x41, 0x0E, 0x80, 0x20, 0x0C, 0x04, 0x17, 0xC3, 0x6B, 0x7C, 0x10, 0x3E, 0x87, 0xF8, 0x18, 0x5E, 0xD9, 0xC3, 0x78, 0x40, 0xB0, 0x8D, 0x89, 0x5E, 0x3C, 0xB8, 0x24, 0x1C, 0x98, 0xE9, 0xA6, 0x09, 0x09, 0x3D, 0x67, 0x79, 0xE1, 0x1F, 0x0A, 0xEB, 0x8D, 0x8C, 0x17, 0x84, 0xA8, 0x18, 0x05, 0xB9, 0x53, 0x30, 0x2A, 0x62, 0x36, 0x64, 0x35, 0x95, 0x39, 0x5D, 0xD4, 0x94, 0x7D, 0x83, 0xA8, 0x80, 0xB1, 0x21, 0xC4, 0x86, 0x41, 0x9F, 0x47, 0x57, 0xE9, 0x50, 0x02, 0xF6, 0xC2, 0x50, 0x02, 0x8E, 0x42, 0x57, 0x02, 0x66, 0xAE, 0xD2, 0xB3, 0xBB, 0xFB, 0x4C, 0xFA, 0xC1, 0x5F, 0x1C, 0x29, 0xA0, 0x6B, 0xE7, 0xBA, 0x07, 0x97, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
                tex.LoadImage(pngBytes);
                return Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0f, 0f));
            }
            public static Sprite StashSprite()
            {
                Texture2D tex = new Texture2D(2, 2);
                byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0xF3, 0xFF, 0x61, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0B, 0x13, 0x00, 0x00, 0x0B, 0x13, 0x01, 0x00, 0x9A, 0x9C, 0x18, 0x00, 0x00, 0x00, 0xCF, 0x49, 0x44, 0x41, 0x54, 0x38, 0x8D, 0xDD, 0x93, 0xAD, 0x6E, 0x02, 0x41, 0x14, 0x85, 0xBF, 0x6D, 0x41, 0xAC, 0x21, 0xA9, 0xA8, 0x58, 0x81, 0xC1, 0xF0, 0x02, 0x6B, 0xEA, 0xD0, 0xA4, 0xAF, 0x50, 0x59, 0x82, 0xAD, 0xC6, 0xC2, 0xAB, 0x61, 0x78, 0x87, 0x2A, 0x14, 0x49, 0x25, 0x2C, 0x15, 0x9B, 0xAF, 0x62, 0x7F, 0x32, 0x6C, 0x77, 0x08, 0x29, 0xAE, 0x27, 0xB9, 0x39, 0x73, 0x67, 0xEE, 0xB9, 0x39, 0x99, 0x3B, 0x93, 0xA8, 0xDC, 0x83, 0x87, 0x4E, 0x3E, 0x04, 0x5E, 0x81, 0x1D, 0xF0, 0x15, 0x89, 0x3D, 0xB0, 0x6A, 0x15, 0x6A, 0x13, 0x23, 0xF5, 0x5D, 0xDD, 0xAA, 0x2F, 0xF5, 0xDE, 0x63, 0x0F, 0x67, 0xEA, 0xA9, 0xD1, 0x85, 0x0E, 0x16, 0xC0, 0x1C, 0xF8, 0x00, 0xA6, 0xC0, 0x18, 0x78, 0xEB, 0xE1, 0x23, 0x90, 0xF6, 0x39, 0x38, 0xA8, 0x13, 0x35, 0x57, 0xCF, 0xEA, 0x32, 0xC2, 0xB9, 0x15, 0x7E, 0x39, 0x78, 0x06, 0x3E, 0x81, 0xB2, 0x8E, 0xEF, 0x08, 0x97, 0x17, 0xB7, 0x16, 0x38, 0x68, 0xBA, 0xA6, 0xEA, 0x4C, 0x7D, 0x8A, 0x70, 0x1A, 0x3A, 0x48, 0x82, 0x31, 0x0A, 0x24, 0xF5, 0x3A, 0x09, 0xF2, 0x3E, 0x6E, 0x6B, 0xBB, 0x63, 0x0C, 0x9B, 0x5D, 0xE3, 0x16, 0xB1, 0x06, 0x37, 0xE3, 0x9F, 0x35, 0x28, 0x80, 0xEC, 0x06, 0x4D, 0x56, 0xD7, 0x02, 0x30, 0x08, 0x0E, 0x36, 0x54, 0x9F, 0x28, 0xED, 0x2A, 0x3A, 0x28, 0x80, 0x75, 0x93, 0x84, 0xEF, 0xE0, 0x4F, 0xF8, 0x01, 0x75, 0x24, 0xCA, 0x52, 0x10, 0xB6, 0xB1, 0xD6, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
                tex.LoadImage(pngBytes);
                return Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0f, 0f));
            }

            public readonly UIButton uiButton;
            private readonly Transform _icon;
            private readonly GameObject _col;
            private bool _enabled;

            public bool Enabled { get => _enabled; set => SetEnabled(value); }

            public IconButton(UIButton btn)
            {
                uiButton = btn;
                uiButton.gameObject.transform.Find("bg").gameObject.SetActive(false);
                uiButton.gameObject.transform.Find("sd").gameObject.SetActive(false);
                _icon = uiButton.gameObject.transform.Find("icon");
                _col = uiButton.gameObject.transform.Find("col").gameObject;

                uiButton.gameObject.SetActive(true);
                _enabled = false;
                SetEnabled(true);
            }

            public void SetEnabled(bool flag)
            {
                //この UIButton は button を持ってない
                if (_enabled == flag) return;
                _enabled = flag;

                _icon.GetComponent<Image>().color = new Color(0.94f, 0.74f, 0.24f, flag ? 0.8f : 0.07f);
                _col.SetActive(flag);
            }

        }

        public static T MakeGameObject<T>(Transform parent, GameObject src, float posX = 0, float posY = 0, float width = 0, float height = 0, bool right = false, bool bottom = false)
        {
            if (src == null) return default;
            var go = Instantiate(src);
            if (go == null)
            {
                Logger.LogInfo("Instantiate failed");
                return default;
            }
            go.name = __NAME__ + "-" + _uiCount++;

            var rect = (RectTransform)go.transform;
            if (rect != null)
            {
                float yAnchor = bottom ? 0 : 1;
                float xAnchor = right ? 1 : 0;
                rect.anchorMax = new Vector2(xAnchor, yAnchor);
                rect.anchorMin = new Vector2(xAnchor, yAnchor);
                rect.pivot = new Vector2(0, 0);
                if (width == -1) width = rect.sizeDelta.x;
                if (height == -1) height = rect.sizeDelta.y;
                if (width > 0 && height > 0)
                {
                    rect.sizeDelta = new Vector2(width, height);
                }
                rect.SetParent(parent, false);
                rect.anchoredPosition = new Vector2(posX, posY);
            }
            return go.GetComponent<T>();
        }


    }
}
