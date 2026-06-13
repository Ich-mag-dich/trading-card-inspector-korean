using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace TCIKorean
{
    [BepInPlugin("com.user.tci-korean", "TCI Korean", "1.0.0")]
    public class KoreanPlugin : BaseUnityPlugin
    {
        static readonly string[] TableNames = { "MainTable", "MemoTable", "CardTable" };
        static readonly string PluginFolder = Path.Combine(Paths.PluginPath, "TCIKorean");

        private bool isShowModal = false;
        private Rect modalRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 100, 300, 200);

        Dictionary<string, Dictionary<long, string>> _translations;
        TMP_FontAsset _koreanFont;

        void Update()
        {
            // F5를 누르면 모달 창 상태 반전
            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
            {
                isShowModal = !isShowModal;

                // 마우스 커서 보이게 처리 (게임 설정에 따라 다름)
                Cursor.visible = isShowModal;
                Cursor.lockState = isShowModal ? CursorLockMode.None : CursorLockMode.Locked;
            }
        }

        private void OnGUI()
        {
            // 모달 창이 켜져 있을 때만 OnGUI 실행
            if (isShowModal)
            {
                // 중앙에 창 그리기
                modalRect = GUI.Window(0, modalRect, DrawModalWindow, "주의! 모달 창");
            }
        }

        private void DrawModalWindow(int windowID)
        {
            GUILayout.Label("이 창이 열려있는 동안 게임 입력을 차단합니다.");
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("확인"))
            {
                Debug.Log("확인 버튼 클릭");
                isShowModal = false;
                Cursor.visible = false;
            }
            if (GUILayout.Button("취소"))
            {
                Debug.Log("취소 버튼 클릭");
                isShowModal = false;
                Cursor.visible = false;
            }
            GUILayout.EndHorizontal();

            // 창을 마우스로 드래그해서 움직일 수 있게 함
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void Awake()
        {
            var jsonPath = Path.Combine(PluginFolder, "strings_ko.json");
            if (!File.Exists(jsonPath)) { Logger.LogError($"strings_ko.json 없음: {jsonPath}"); return; }
            LoadTranslations(jsonPath);
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(InjectTranslations());
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_koreanFont != null) StartCoroutine(ApplyFontNextFrame());
        }

        IEnumerator ApplyFontNextFrame() { yield return null; ApplyFont(); }

        void ApplyFont()
        {
            if (_koreanFont == null) return;

            // 글로벌 fallback에 추가 (동적으로 생성되는 UI 포함)
            if (!TMP_Settings.fallbackFontAssets.Contains(_koreanFont))
                TMP_Settings.fallbackFontAssets.Add(_koreanFont);

            // 현재 씬의 모든 폰트 fallback에도 추가
            var seen = new HashSet<TMP_FontAsset>();
            int count = 0;
            foreach (var t in GameObject.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.font == null || seen.Contains(t.font)) continue;
                seen.Add(t.font);
                if (!t.font.fallbackFontAssetTable.Contains(_koreanFont))
                {
                    t.font.fallbackFontAssetTable.Add(_koreanFont);
                    count++;
                }
            }
            Logger.LogInfo($"폰트 fallback 적용: {count}개 폰트");
        }

        TMP_FontAsset LoadFontFromBundle(string bundlePath)
        {
            if (!File.Exists(bundlePath)) { Logger.LogError($"폰트 번들 없음: {bundlePath}"); return null; }
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null) { Logger.LogError("번들 로드 실패"); return null; }
            var assets = bundle.LoadAllAssets<TMP_FontAsset>();
            if (assets == null || assets.Length == 0) { Logger.LogError("번들에 FontAsset 없음"); return null; }
            Logger.LogInfo($"폰트 번들 로드 성공: {assets[0].name}");
            return assets[0];
        }

        void LoadTranslations(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var root = JObject.Parse(json);
            var tables = (JObject)root["tables"];
            _translations = new Dictionary<string, Dictionary<long, string>>();
            foreach (var table in tables)
            {
                var dict = new Dictionary<long, string>();
                foreach (var entry in (JObject)table.Value)
                {
                    if (!long.TryParse(entry.Key, out long keyId)) continue;
                    var ko = entry.Value["ko"]?.ToString() ?? "";
                    if (ko.Length > 0) dict[keyId] = ko;
                }
                _translations[table.Key] = dict;
                Logger.LogInfo($"로드: {table.Key} {dict.Count}개");
            }
        }

        IEnumerator InjectTranslations()
        {
            yield return LocalizationSettings.InitializationOperation;

            Locale enLocale = null;
            foreach (var l in LocalizationSettings.AvailableLocales.Locales)
            { if (l.Identifier.Code == "en") enLocale = l; }
            if (enLocale == null) { Logger.LogError("EN locale 없음"); yield break; }

            LocalizationSettings.SelectedLocale = enLocale;
            yield return null;

            foreach (var tableName in TableNames)
            {
                var op = LocalizationSettings.StringDatabase.GetTableAsync(tableName, enLocale);
                yield return op;
                var table = op.Result;
                if (table == null) { Logger.LogWarning($"{tableName}: null"); continue; }
                var jsonKey = tableName + "_en";
                if (!_translations.TryGetValue(jsonKey, out var dict)) continue;
                int count = 0;
                foreach (var kv in dict)
                { var entry = table.GetEntry(kv.Key); if (entry != null) { entry.Value = kv.Value; count++; } }
                Logger.LogInfo($"{tableName}: {count}개 교체");
            }

            LocalizationSettings.SelectedLocale = enLocale;
            Logger.LogInfo("번역 주입 완료");

            _koreanFont = LoadFontFromBundle(Path.Combine(PluginFolder, "koreanfont"));
            Logger.LogInfo($"폰트 로드: {(_koreanFont != null ? "성공" : "실패")}");
            ApplyFont();
        }
    }
}
