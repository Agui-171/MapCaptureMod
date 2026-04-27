using GlobalEnums;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityStandardAssets.ImageEffects;
namespace AMapMod
{
    public class ExportManager
    {
        public bool IsProcessing { get; private set; }
        public int CurrentIndex { get; private set; }
        public int TotalIndex { get; private set; }
        public float PPU { get; set; } = 32f;
        private bool _cancelRequested = false;
        /// <summary>
        /// 
        /// </summary>
        private readonly MonoBehaviour _coroutineRunner;
        public ExportManager(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }
        /// <summary>
        /// 取消
        /// </summary>
        public void Cancel() => _cancelRequested = true;
        /// <summary>
        /// 单独导一张图
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="sceneName"></param>
        public void StartExportScene(MapZone zone, string sceneName)
        {
            if(IsProcessing) return;
            _coroutineRunner.StartCoroutine(ExportScene(zone.ToString(), sceneName));
        }
        /// <summary>
        /// 导出一个场景
        /// </summary>
        /// <param name="zone"></param>
        public void StartExportZone(MapZone zone)
        {
            if(IsProcessing) return;
            _coroutineRunner.StartCoroutine(ExportZone(zone));
        }
        private IEnumerator ExportScene(string zoneName, string sceneName)
        {
            IsProcessing = true;
            CurrentIndex = 1;
            TotalIndex = 1;
            Utils.Clean();
            yield return new WaitForEndOfFrame();
            Process(zoneName, sceneName);
            IsProcessing = false;
        }
        private IEnumerator ExportZone(MapZone zone)
        {
            var sceneList = Utils.Find(zone);
            IsProcessing = true;
            TotalIndex = sceneList.Count;
            CurrentIndex = 0;
            _cancelRequested = false;
            foreach(string sceneName in sceneList)
            {
                if(_cancelRequested) break;
                CurrentIndex++;
                yield return Utils.Teleport(sceneName);
                yield return new WaitForSeconds(0.5f);
                Utils.Clean();
                Process(zone.ToString(), sceneName);
            }
            _cancelRequested = false;
            IsProcessing = false;
        }
        /// <summary>
        /// 一些加载流程
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="sceneName"></param>
        private void Process(string zoneName, string sceneName)
        {
            try
            {
                Rect fullBounds = Utils.GetBounds();
                int targetW = Mathf.RoundToInt(fullBounds.width * PPU);
                int targetH = Mathf.RoundToInt(fullBounds.height * PPU);
                /// 限制最大尺寸防止报错
                int maxSize = SystemInfo.maxTextureSize;
                if(targetW > maxSize || targetH > maxSize)
                {
                    Debug.LogWarning($"[MapMod] 场景 {sceneName} 尺寸 ({targetW}x{targetH}) 超出限制 ({maxSize})，自动进行等比缩放");
                    float scale = Mathf.Min((float)maxSize / targetW, (float)maxSize / targetH);
                    targetW = Mathf.RoundToInt(targetW * scale);
                    targetH = Mathf.RoundToInt(targetH * scale);
                }
                Camera captureCam = Utils.Create(fullBounds);
                Texture2D photo = Utils.Render(captureCam, targetW, targetH);
                ExportPng(photo, zoneName, sceneName);
                Object.Destroy(photo);
                Object.Destroy(captureCam.gameObject);
                if(HeroController.instance) HeroController.instance.GetComponent<MeshRenderer>().enabled = true;
            }
            catch(System.Exception ex)
            {
                Debug.LogError($"[MapMod] 导出场景 {sceneName} 出错: {ex.Message}");
            }
            finally
            {
                if(HeroController.instance && HeroController.instance.GetComponent<MeshRenderer>())
                    HeroController.instance.GetComponent<MeshRenderer>().enabled = true;
            }
        }
        /// <summary>
        /// 导出png
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="zoneName"></param>
        /// <param name="sceneName"></param>
        private void ExportPng(Texture2D tex, string zoneName, string sceneName)
        {
            string folderPath = Path.Combine(Application.dataPath, "../MapExport", zoneName);
            if(!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            string pngPath = Path.Combine(folderPath, $"{sceneName}.png");
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);
            Debug.Log($"[MapMod] 成功导出: {pngPath}");
        }
    }
    public static class Utils
    {
        public static AccessTools.FieldRef<GameMap, object[]> ZoneInfoArrayRef = AccessTools.FieldRefAccess<GameMap, object[]>("mapZoneInfo");
        /// <summary>
        /// 获取地图边界
        /// </summary>
        /// <returns></returns>
        public static Rect GetBounds()
        {
            tk2dTileMap[] tilemaps = Object.FindObjectsOfType<tk2dTileMap>();
            if(tilemaps == null || tilemaps.Length == 0) return new Rect(0, 0, GameManager.instance.sceneWidth, GameManager.instance.sceneHeight);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach(var tm in tilemaps)
            {
                Vector3 pos = tm.transform.position;
                minX = Mathf.Min(minX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxX = Mathf.Max(maxX, pos.x + tm.width);
                maxY = Mathf.Max(maxY, pos.y + tm.height);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
        /// <summary>
        /// 寻找所有区域场景
        /// </summary>
        /// <param name="zone"></param>
        /// <returns></returns>
        public static List<string> Find(MapZone zone)
        {
            GameManager gm = GameManager.instance;
            List<string> scenes = new List<string>();
            object[] zoneInfoArray = ZoneInfoArrayRef(gm.gameMap);
            object zoneInfo = zoneInfoArray[(int)zone];
            var parents = Traverse.Create(zoneInfo).Field("Parents").GetValue<IEnumerable>();
            if(gm == null || gm.gameMap == null || zoneInfo == null || parents == null) return scenes;
            foreach(var parent in parents)
            {
                var maps = Traverse.Create(parent).Field("Maps").GetValue<IEnumerable>();
                if(maps == null) continue;
                foreach(var map in maps)
                {
                    var mapTraverse = Traverse.Create(map);
                    string sceneName = mapTraverse.Field("sceneName").GetValue<string>();
                    if(!mapTraverse.Field("hasGameMap").GetValue<bool>()) continue;
                    if(!string.IsNullOrEmpty(sceneName) && !scenes.Contains(sceneName))
                    {
                        scenes.Add(sceneName);
                    }
                }
            }
            return scenes;
        }
        /// <summary>
        /// 清理场景
        /// </summary>
        public static void Clean()
        {
            var gm = GameManager.instance;
            var hc = HeroController.instance;
            if(hc == null) return;
            if(hc.GetComponent<MeshRenderer>()) hc.GetComponent<MeshRenderer>().enabled = false;
            if(hc.vignette != null) hc.vignette.enabled = false;
            /// 去除遮罩，雾，暗色，蒙版，热浪
            string[] blockKey = { "vignette", "fog", "dark", "mask", "heatplane" };
            foreach(var go in Object.FindObjectsOfType<GameObject>(true))
            {
                string n = go.name.ToLowerInvariant();
                if(blockKey.Any(keyword => n.Contains(keyword)))
                {
                    go.SetActive(false);
                }
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * (ModCore.SceneBrightness / 1.3f);
            if(GameCameras.instance != null)
            {
                var be = GameCameras.instance.GetComponent<BrightnessEffect>();
                if(be != null)
                {
                    be.StopAllCoroutines();
                    be._Brightness = ModCore.SceneBrightness;
                    be._Contrast = 1f;
                }
                if(GameCameras.instance.sceneColorManager != null)
                {
                    GameCameras.instance.sceneColorManager.enabled = false;
                    var curves = GameCameras.instance.sceneColorManager.GetComponent<ColorCorrectionCurves>();
                    if(curves != null) curves.enabled = false;
                }
            }
        }
        /// <summary>
        /// 角色传送
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public static IEnumerator Teleport(string sceneName)
        {
            GameManager gm = GameManager.instance;
            string sceneKey = "Scenes/" + sceneName;
            var checkHandle = Addressables.LoadResourceLocationsAsync(sceneKey);
            yield return checkHandle;
            if(checkHandle.Status != AsyncOperationStatus.Succeeded || checkHandle.Result.Count == 0)
            {
                Addressables.Release(checkHandle);
                yield break;
            }
            Addressables.Release(checkHandle);
            GameManager.SceneLoadInfo loadInfo = new GameManager.SceneLoadInfo
            {
                SceneName = sceneName,
                EntryGateName = "right1",
                PreventCameraFadeOut = true,
                WaitForSceneTransitionCameraFade = false,
                AlwaysUnloadUnusedAssets = false
            };
            gm.BeginSceneTransition(loadInfo);
            float timeout = 0f;
            while(timeout < 10f)
            {
                bool isReadyState = gm.GameState == GameState.PLAYING || gm.GameState == GameState.CUTSCENE;
                if(!gm.IsInSceneTransition && isReadyState) break;
                timeout += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        /// <summary>
        /// 创建摄像机
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Camera Create(Rect bounds)
        {
            GameObject camObj = new GameObject("CaptureCam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(bounds.x + bounds.width / 2f, bounds.y + bounds.height / 2f, -10f);
            cam.orthographicSize = bounds.height / 2f;
            cam.aspect = bounds.width / bounds.height;
            int mask = Camera.main.cullingMask;
            mask &= ~(1 << LayerMask.NameToLayer("UI"));
            cam.cullingMask = mask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            return cam;
        }
        /// <summary>
        /// 渲染摄像机
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Texture2D Render(Camera cam, int width, int height)
        {
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
            return tex;
        }
    }
}