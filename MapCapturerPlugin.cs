using BepInEx;
using GlobalEnums;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MapCapturerMod
{
    [BepInPlugin("NaughtyCat.MapCapturerMod", "MapCapturerMod", "1.0.0")]
    public class MapCapturerPlugin : BaseUnityPlugin
    {
        public static MapCapturerPlugin Instance;
        private bool _showUI = false;
        private bool _waitingKey;
        private KeyCode _uiKey = KeyCode.Y;
        private Rect _uiRect = new Rect(100, 400, 350, 600);
        private int _blockFrame = -1;
        private Vector2 _scroll;
        private List<MapZone> _zones;
        private void Awake()
        {
            Instance = this;
            _zones = new List<MapZone>();
            Debug.Log("[MapCapturer] 已加载");
        }
        private void Update()
        {
            if(_waitingKey || Time.frameCount == _blockFrame) return;
            if(Input.GetKeyDown(_uiKey))
            {
                _showUI = !_showUI;
                if(_showUI && _zones.Count == 0)
                {
                    _zones.Clear();
                    foreach(MapZone zone in Enum.GetValues(typeof(MapZone)))
                    {
                        if(zone == MapZone.NONE) continue;
                        _zones.Add(zone);
                    }
                    _zones.Sort((a, b) => string.Compare(Utils.GetLocalized(a), Utils.GetLocalized(b)));
                    Debug.Log("[MapCapturer] 区域列表初始化完成");
                }
            }
        }
        private void OnGUI()
        {
            if(_showUI) _uiRect = GUI.Window(0, _uiRect, Draw, "MapCapturerMod");
        }
        private void Draw(int id)
        {
            var gm = GameManager.instance;
            MapZone currentZone = gm.GetCurrentMapZoneEnum();
            string currentScene = gm.GetSceneNameString();
            RebindRow();
            GUI.enabled = !Utils.IsProcessing;
            ExportZoneRow(currentZone);
            ExportSceneRow(currentZone, currentScene);
            GUI.enabled = true;
            ExportProcessRow();
            BrightnessRow();
            ListRow();
            PathRow();
            if(GUILayout.Button("关闭面板")) _showUI = false;
            GUI.DragWindow();
        }
        private void RebindRow()
        {
            var e = Event.current;
            GUILayout.BeginHorizontal();
            GUILayout.Label("面板快捷键：" + _uiKey);
            string btnText = _waitingKey ? "按下任意键…" : "重设";
            GUI.enabled = !_waitingKey;
            if(GUILayout.Button(btnText, GUILayout.Width(100))) _waitingKey = true;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            if(_waitingKey && e.isKey && e.type == EventType.KeyDown)
            {
                _uiKey = e.keyCode;
                _waitingKey = false;
                _blockFrame = Time.frameCount;
                e.Use();
            }
        }
        private void ExportZoneRow(MapZone curZone)
        {
            string curZoneLocal = Utils.GetLocalized(curZone);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前区域：【{curZoneLocal}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前区域", GUILayout.Width(100))) Utils.Export(curZone);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ExportSceneRow(MapZone curZone, string curScene)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前场景：【{curScene}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前场景", GUILayout.Width(100))) Utils.Export(curZone, curScene);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ExportProcessRow()
        {
            GUILayout.BeginHorizontal();
            if(Utils.IsProcessing)
            {
                GUILayout.Label($"当前导出进度：{Utils.CurrentIndex}/{Utils.TotalIndex}");
                if(GUILayout.Button("停止", GUILayout.Width(60))) Utils.IsCancel = true;
            }
            else if(Utils.TotalIndex > 0 && Utils.CurrentIndex == Utils.TotalIndex) GUILayout.Label("当前导出进度：已全部导出");
            else GUILayout.Label("当前导出进度：空闲");
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void BrightnessRow()
        {
            GUILayout.Label($"自定义场景亮度：{Utils.SceneBrightness:F1}");
            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            Utils.SceneBrightness = GUILayout.HorizontalSlider(Utils.SceneBrightness, 0.5f, 3f);
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ListRow()
        {
            GUI.enabled = !Utils.IsProcessing;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if(_zones != null)
            {
                foreach(MapZone zone in _zones)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Utils.GetLocalized(zone));
                    GUILayout.FlexibleSpace();
                    if(GUILayout.Button("导出", GUILayout.Width(80))) Utils.Export(zone);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
            GUILayout.Space(5f);
        }
        private void PathRow()
        {
            if(GUILayout.Button("打开文件夹"))
            {
                string path = Path.Combine(Application.dataPath, "../MapExport");
                if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                Application.OpenURL(Path.GetFullPath(path));
            }
            GUILayout.Space(5);
        }
    }
}