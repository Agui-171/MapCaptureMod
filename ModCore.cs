using BepInEx;
using GlobalEnums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace AMapMod
{
    [BepInPlugin("NaughtyCat.MapCapturer", "MapCapturerMod", "1.0.0")]
    public class ModCore : BaseUnityPlugin
    {
        private int _inputBlockedFrame = -1;
        private bool _showUI = false;
        private bool _waitingForKey = false;
        private List<MapZone> _sortedZones;
        private Rect _uiRect = new Rect(100, 300, 350, 500);
        private Vector2 _scrollPos;
        private KeyCode _toggleKey = KeyCode.Y;
        private ExportManager _exportManager;
        private void Awake()
        {
            Logger.LogInfo("[MapCapturer] 已加载");
            _sortedZones = Enum.GetValues(typeof(MapZone))
                .Cast<MapZone>()
                .OrderBy(z => z.ToString())
                .ToList();
            _exportManager = new ExportManager(this);
        }
        private void Update()
        {
            if(_waitingForKey || Time.frameCount == _inputBlockedFrame) return;
            if(Input.GetKeyDown(_toggleKey))
            {
                _showUI = !_showUI;
            }
        }
        private void OnGUI()
        {
            if(_showUI) _uiRect = GUI.Window(0, _uiRect, DrawUI, "MapCapturerMod");
        }
        private void DrawUI(int id)
        {
            var e = Event.current;
            var gm = GameManager.instance;
            MapZone currentZone = gm.GetCurrentMapZoneEnum();
            string currentScene = gm.GetSceneNameString();
            GUILayout.BeginHorizontal();
            /// 重设键
            GUILayout.Label("面板快捷键：" + _toggleKey);
            string btnText = _waitingForKey ? "按下任意键…" : "重设";
            GUI.enabled = !_waitingForKey;
            if(GUILayout.Button(btnText, GUILayout.Width(100))) _waitingForKey = true;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if(!_waitingForKey || e.type != EventType.KeyDown) return;
            _toggleKey = e.keyCode;
            _waitingForKey = false;
            _inputBlockedFrame = Time.frameCount;
            e.Use();
            GUILayout.Space(5);
            GUI.enabled = !_exportManager.IsProcessing;
            /// 导出区域
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前区域：【{currentZone}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前区域", GUILayout.Width(100))) _exportManager.StartExportZone(currentZone);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            /// 导出场景
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前场景：【{currentScene}】");
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("导出当前场景", GUILayout.Width(100))) _exportManager.StartExportScene(currentZone, currentScene);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUI.enabled = true;
            /// 导出进度
            GUILayout.BeginHorizontal();
            if(_exportManager.IsProcessing)
            {
                GUILayout.Label($"当前导出进度：{_exportManager.CurrentIndex}/{_exportManager.TotalIndex}");
                if(GUILayout.Button("停止", GUILayout.Width(60)))
                {
                    _exportManager.Cancel();
                }
            }
            else if(_exportManager.TotalIndex > 0 && _exportManager.CurrentIndex == _exportManager.TotalIndex)
            {
                GUILayout.Label("当前导出进度：已全部导出");
            }
            else
            {
                GUILayout.Label("当前导出进度：空闲");
            }
            GUILayout.EndHorizontal();
            /// 滑动条
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(270));
            if(_sortedZones != null)
            {
                foreach(MapZone zone in _sortedZones)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(zone.ToString());
                    GUILayout.FlexibleSpace();
                    if(GUILayout.Button("导出", GUILayout.Width(80)))
                        _exportManager.StartExportZone(zone);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            /// 开启文件夹
            if(GUILayout.Button("打开文件夹"))
            {
                string path = Path.Combine(Application.dataPath, "../MapExport");
                if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                Application.OpenURL(Path.GetFullPath(path));
            }
            GUILayout.Space(5);
            /// 关闭面板
            if(GUILayout.Button("关闭面板")) _showUI = false;
            GUI.DragWindow();
        }
    }
}