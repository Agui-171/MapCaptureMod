using GlobalEnums;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MapCapturerMod
{
    public static class Utils
    {
        public static int CurrentIndex;
        public static int TotalIndex;
        public static float PPU = 32f;
        public static float SceneBrightness = 1.5f;
        public static bool IsCancel = false;
        public static bool IsProcessing;
        public static Rect GetBound()
        {
            tk2dTileMap[] tilemaps = Object.FindObjectsOfType<tk2dTileMap>();
            if(tilemaps == null || tilemaps.Length == 0)
                return new Rect(0, 0, GameManager.instance.sceneWidth, GameManager.instance.sceneHeight);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach(var t in tilemaps)
            {
                Vector3 pos = t.transform.position;
                minX = Mathf.Min(minX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxX = Mathf.Max(maxX, pos.x + t.width);
                maxY = Mathf.Max(maxY, pos.y + t.height);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
        public static List<string> FindZone(MapZone zone)
        {
            GameManager gm = GameManager.instance;
            var scenes = new List<string>();
            var getZoneArray = AccessTools.FieldRefAccess<GameMap, object[]>("mapZoneInfo");
            var targetZone = getZoneArray(gm.gameMap)[(int)zone];
            var parents = Traverse.Create(targetZone).Field("Parents").GetValue<IEnumerable>();
            if(gm?.gameMap == null || parents == null) return scenes;
            foreach(var p in parents)
            {
                var maps = Traverse.Create(p).Field("Maps").GetValue<IEnumerable>();
                if(maps == null) continue;
                foreach(var m in maps)
                {
                    var mapNav = Traverse.Create(m);
                    if(!mapNav.Field("hasGameMap").GetValue<bool>()) continue;
                    string name = mapNav.Field("sceneName").GetValue<string>();
                    if(!string.IsNullOrEmpty(name) && !scenes.Contains(name))
                    {
                        scenes.Add(name);
                    }
                }
            }
            return scenes;
        }
        public static void CleanScene()
        {
            var gm = GameManager.instance;
            var hc = HeroController.instance;
            if(hc == null) return;
            if(hc.GetComponent<MeshRenderer>()) hc.GetComponent<MeshRenderer>().enabled = false;
            if(hc.vignette != null) hc.vignette.enabled = false;
            string[] blockKey = { "vignette", "fog", "dark", "mask", "heatplane" };
            foreach(var go in Object.FindObjectsOfType<GameObject>(true))
            {
                string n = go.name.ToLowerInvariant();
                if(blockKey.Any(keyword => n.Contains(keyword))) go.SetActive(false);
            }
            var gc = GameCameras.instance;
            if(gc == null) return;
            var be = gc.GetComponent<BrightnessEffect>();
            if(be != null)
            {
                be.StopAllCoroutines();
                be._Brightness = SceneBrightness;
                be._Contrast = 1f;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * (SceneBrightness / 1.3f);
        }
        public static void RestoreScene()
        {
            var hc = HeroController.instance;
            var gm = GameManager.instance;
            var gc = GameCameras.instance;
            if(hc == null || gm == null) return;
            var mr = hc.GetComponent<MeshRenderer>();
            if(mr) mr.enabled = true;
            if(hc.vignette != null) hc.vignette.enabled = true;
            var be = gc?.GetComponent<BrightnessEffect>();
            if(be != null) be.enabled = true;
        }
        public static IEnumerator TeleportScene(string sceneName)
        {
            string sceneKey = "Scenes/" + sceneName;
            var checkHandle = Addressables.LoadResourceLocationsAsync(sceneKey);
            yield return checkHandle;
            if(checkHandle.Status != AsyncOperationStatus.Succeeded || checkHandle.Result.Count == 0)
            {
                Addressables.Release(checkHandle);
                yield break;
            }
            Addressables.Release(checkHandle);
            var info = new GameManager.SceneLoadInfo
            {
                SceneName = sceneName,
                EntryGateName = "right1",
                PreventCameraFadeOut = true,
                WaitForSceneTransitionCameraFade = false,
                AlwaysUnloadUnusedAssets = false
            };
            GameManager gm = GameManager.instance;
            gm.BeginSceneTransition(info);
            for(float t = 0; t < 5f; t += Time.unscaledDeltaTime)
            {
                if(!gm.IsInSceneTransition && (gm.GameState == GameState.PLAYING || gm.GameState == GameState.CUTSCENE)) break;
                yield return null;
            }
        }
        public static Texture2D CaptureScene(Rect bounds, int width, int height)
        {
            var camObj = new GameObject("CaptureCam");
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(bounds.x + bounds.width / 2f, bounds.y + bounds.height / 2f, -10f);
            cam.orthographicSize = bounds.height / 2f;
            cam.aspect = (float)width / height;
            int mask = Camera.main != null ? Camera.main.cullingMask : -1;
            int uiLayer = LayerMask.NameToLayer("UI");
            if(uiLayer != -1) mask &= ~(1 << uiLayer);
            cam.cullingMask = mask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(camObj);
            return tex;
        }
        public static void Export(MapZone zone, string sceneName = null)
        {
            if(IsProcessing) return;
            MapCapturerPlugin.Instance.StartCoroutine(ExportRoutine(zone, sceneName));
        }
        private static void Save(string zoneName, string sceneName)
        {
            try
            {
                Rect bounds = GetBound();
                int targetW = Mathf.RoundToInt(bounds.width * PPU);
                int targetH = Mathf.RoundToInt(bounds.height * PPU);
                int maxSize = SystemInfo.maxTextureSize;
                if(targetW > maxSize || targetH > maxSize)
                {
                    float scale = Mathf.Min((float)maxSize / targetW, (float)maxSize / targetH);
                    targetW = Mathf.RoundToInt(targetW * scale);
                    targetH = Mathf.RoundToInt(targetH * scale);
                }
                Texture2D photo = CaptureScene(bounds, targetW, targetH);
                string dir = Path.Combine(Application.dataPath, "../MapExport", zoneName);
                if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, $"{sceneName}.png"), photo.EncodeToPNG());
                Object.Destroy(photo);
            }
            catch(System.Exception ex)
            {
                Debug.LogError($"[MapCapturer] 导出 {sceneName} 失败: {ex.Message}");
            }
        }
        private static IEnumerator ExportRoutine(MapZone zone, string scene)
        {
            IsProcessing = true;
            IsCancel = false;
            var list = string.IsNullOrEmpty(scene) ? FindZone(zone) : new List<string> { scene };
            TotalIndex = list.Count;
            CurrentIndex = 0;
            try
            {
                foreach(string s in list)
                {
                    if(IsCancel) break;
                    CurrentIndex++;
                    if(GameManager.instance.GetSceneNameString() != s)
                    {
                        yield return TeleportScene(s);
                    }
                    CleanScene();
                    Save(zone.ToString(), s);
                }
            }
            finally
            {
                RestoreScene();
                IsProcessing = false;
                IsCancel = false;
                Debug.Log("[MapCapturer] 所有导出任务已完成");
            }
        }
        public static string GetLocalized(MapZone zone)
        {
            string localized = Language.Get(zone.ToString(), "Map Zones");
            return string.IsNullOrEmpty(localized) ? zone.ToString() : localized;
        }
    }
}