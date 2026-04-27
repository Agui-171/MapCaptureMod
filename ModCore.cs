using BepInEx;
using GlobalEnums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamCherry.Localization;
using UnityEngine;
namespace AMapMod
{
    [BepInPlugin("NaughtyCat.MapCapturer", "MapCapturerMod", "1.0.1")]
    public class ModCore : BaseUnityPlugin
    {
        private int _blockFrame = -1;
        private bool _showUI = false;
        private bool _waitingKey = false;
        private Rect _uiRect = new Rect(100, 400, 350, 600);
        private List<MapZone>? _sortedZones;
        private Vector2 _scrollPos;
        private KeyCode _toggleKey = KeyCode.Y;
        private ExportManager? _exportManager;
        public static float SceneBrightness = 1.3f;
        private void Awake()
        {
            Logger.LogInfo("[MapCapturer] 已加载");
            _exportManager = new ExportManager(this);
        }
        private string GetLocalized(MapZone zone)
        {
            string localized = Language.Get(zone.ToString(), "Map Zones");
            return string.IsNullOrEmpty(localized) ? zone.ToString() : localized;
        }
        private void Update()
        {
            if(_waitingKey || Time.frameCount == _blockFrame) return;
            if(Input.GetKeyDown(_toggleKey))
            {
                _showUI = !_showUI;
                if(_showUI)
                {
                    _sortedZones = Enum.GetValues(typeof(MapZone))
                        .Cast<MapZone>()
                        .OrderBy(z => GetLocalized(z))
                        .ToList();
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
            GUI.enabled = !_exportManager.IsProcessing;
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
            GUILayout.Label("面板快捷键：" + _toggleKey);
            string btnText = _waitingKey ? "按下任意键…" : "重设";
            GUI.enabled = !_waitingKey;
            if(GUILayout.Button(btnText, GUILayout.Width(100))) _waitingKey = true;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            if(_waitingKey && e.isKey && e.type == EventType.KeyDown)
            {
                _toggleKey = e.keyCode;
                _waitingKey = false;
                _blockFrame = Time.frameCount;
                e.Use();
            }
        }
        private void ExportZoneRow(MapZone curZone)
        {
            string curZoneLocal = GetLocalized(curZone);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前区域：【{curZoneLocal}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前区域", GUILayout.Width(100))) _exportManager.StartExportZone(curZone);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ExportSceneRow(MapZone curZone, string curScene)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前场景：【{curScene}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前场景", GUILayout.Width(100))) _exportManager.StartExportScene(curZone, curScene);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ExportProcessRow()
        {
            GUILayout.BeginHorizontal();
            if(_exportManager.IsProcessing)
            {
                GUILayout.Label($"当前导出进度：{_exportManager.CurrentIndex}/{_exportManager.TotalIndex}");
                if(GUILayout.Button("停止", GUILayout.Width(60))) _exportManager.Cancel();
            }
            else if(_exportManager.TotalIndex > 0 && _exportManager.CurrentIndex == _exportManager.TotalIndex) GUILayout.Label("当前导出进度：已全部导出");
            else GUILayout.Label("当前导出进度：空闲");
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void BrightnessRow()
        {
            GUILayout.Label($"自定义场景亮度：{SceneBrightness:F1}");
            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            SceneBrightness = GUILayout.HorizontalSlider(SceneBrightness, 0.5f, 3f);
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void ListRow()
        {
            GUI.enabled = !_exportManager.IsProcessing;
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            if(_sortedZones != null)
            {
                foreach(MapZone zone in _sortedZones)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetLocalized(zone));
                    GUILayout.FlexibleSpace();
                    if(GUILayout.Button("导出", GUILayout.Width(80)))
                        _exportManager.StartExportZone(zone);
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