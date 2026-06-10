using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace TCIKorean
{
    [BepInPlugin("com.user.tci-korean", "TCI Korean", "1.0.0")]
    public class KoreanPlugin : BaseUnityPlugin
    {
        static readonly string[] TableNames = { "MainTable", "MemoTable", "CardTable" };
        static readonly string PluginFolder = Path.Combine(Paths.PluginPath, "TCIKorean");

        Dictionary<string, Dictionary<long, string>> _translations;
        TMP_FontAsset _koreanFont;

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
            int count = 0;
            foreach (var t in GameObject.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { t.font = _koreanFont; t.ForceMeshUpdate(true); count++; }
            Logger.LogInfo($"폰트 적용: {count}개");
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
