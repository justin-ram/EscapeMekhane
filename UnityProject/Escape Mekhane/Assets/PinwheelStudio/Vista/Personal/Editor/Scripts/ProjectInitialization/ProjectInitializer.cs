using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    [InitializeOnLoad]
    public static class ProjectInitializer
    {
        public const string KW_VISTA = "VISTA";
        private const string FIRST_IMPORT_TIME_KEY = "Pinwheel.Vista.FirstImportTimeUtc";

        public static bool isSearcherInstalled { get; private set; }
        public static bool isEditorCoroutinesInstalled { get; private set; }
        public static bool isMathematicsInstalled { get; private set; }
        public static bool isVistaIndieInstalled { get; private set; }
        public static bool isVistaProInstalled { get; private set; }
        public static DateTime firstImportTimeUtc { get; private set; }

        /// <summary>
        /// Handlers to run once Vista finishes project setup and the editor becomes idle, not compiling
        /// and not importing assets. Private on purpose, subscribe through <see cref="RunWhenCompleted"/>
        /// so a subscription added after completion still runs instead of being silently missed.
        /// </summary>
        private static Action s_completed;

        /// <summary>True once completion has fired for the current domain load.</summary>
        public static bool isCompleted { get; private set; }

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            ResetPackageState();
            SetupDependencyPackages();
            SetupScriptingSymbols();

            EditorApplication.update += FireCompletedWhenIdle;
        }

        private static void FireCompletedWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= FireCompletedWhenIdle;

            EnsureFirstImportTime();

            // One shot. Mark completed and clear the handler list before invoking, so a handler that
            // calls RunWhenCompleted during the callback runs immediately instead of being dropped, and
            // so subscriber references are released right after firing.
            isCompleted = true;
            Action handlers = s_completed;
            s_completed = null;
            handlers?.Invoke();
        }

        /// <summary>
        /// Run an action once Vista is initialized and the editor is idle. If that already happened for
        /// this domain load the action runs immediately, otherwise it runs when completion fires.
        /// </summary>
        public static void RunWhenCompleted(Action action)
        {
            if (action == null)
                return;
            if (isCompleted)
                action();
            else
                s_completed += action;
        }

        /// <summary>
        /// Record the first successful Vista initialization for this project on this machine. The value
        /// survives editor restarts and domain reloads, but is not written into or shared with the project.
        /// </summary>
        private static void EnsureFirstImportTime()
        {
            string key = $"{FIRST_IMPORT_TIME_KEY}.{PlayerSettings.productGUID}";
            if (!EditorPrefs.HasKey(key))
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                EditorPrefs.SetString(key, nowTicks.ToString());
                firstImportTimeUtc = new DateTime(nowTicks, DateTimeKind.Utc);
                return;
            }

            string storedValue = EditorPrefs.GetString(key);
            if (long.TryParse(storedValue, out long storedTicks) &&
                storedTicks >= DateTime.MinValue.Ticks &&
                storedTicks <= DateTime.MaxValue.Ticks)
                firstImportTimeUtc = new DateTime(storedTicks, DateTimeKind.Utc);
        }

        private static void ResetPackageState()
        {
            isSearcherInstalled = false;
            isEditorCoroutinesInstalled = false;
            isMathematicsInstalled = false;

            isVistaIndieInstalled = false;
            isVistaProInstalled = false;
        }

        private static void SetupDependencyPackages()
        {
            List<Type> loadedTypes = GetAllLoadedTypes();
            foreach (Type t in loadedTypes)
            {
                if (!string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("UnityEditor.Searcher"))
                {
                    isSearcherInstalled = true;
                }
                if (!string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Unity.EditorCoroutines"))
                {
                    isEditorCoroutinesInstalled = true;
                }
                if (!string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Unity.Mathematics"))
                {
                    isMathematicsInstalled = true;
                }
                if (t.Name.Equals("VistaIndie"))
                {
                    isVistaIndieInstalled = true;
                }
                if (t.Name.Equals("VistaPro"))
                {
                    isVistaProInstalled = true;
                }
            }

            if (!isSearcherInstalled)
            {
                Debug.Log("VISTA: Installing dependency package [com.unity.searcher]");
                Client.Add("com.unity.searcher");
            }
            if (!isEditorCoroutinesInstalled)
            {
                Debug.Log("VISTA: Installing dependency package [com.unity.editorcoroutines]");
                Client.Add("com.unity.editorcoroutines");
            }
            if (!isMathematicsInstalled)
            {
                Debug.Log("VISTA: Installing dependency package [com.unity.mathematics]");
                Client.Add("com.unity.mathematics");
            }
        }

        private static void SetupScriptingSymbols()
        {
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            string scriptingSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));
            string newScriptingSymbols = scriptingSymbols;

            if (isSearcherInstalled)
            {
                newScriptingSymbols = AddKeywordToScriptingSymbols(KW_VISTA, newScriptingSymbols);
            }
            else
            {
                newScriptingSymbols = RemoveKeywordFromScriptingSymbols(KW_VISTA, newScriptingSymbols);
            }

            if (!string.IsNullOrEmpty(newScriptingSymbols) && !newScriptingSymbols.Equals(scriptingSymbols))
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), newScriptingSymbols);
            }
        }

        private static List<string> SplitScriptingSymbolsToList(string scriptingSymbols)
        {
            string[] splitSymbols = scriptingSymbols.Split(new string[] { ";" }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < splitSymbols.Length; ++i)
            {
                string s = splitSymbols[i].Replace(" ", "");
                splitSymbols[i] = s;
            }
            return new List<string>(splitSymbols);
        }

        private static string CombineSymbolsListToString(List<string> symbols)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < symbols.Count; ++i)
            {
                sb.Append(symbols[i]).Append(";");
            }
            return sb.ToString();
        }

        public static string AddKeywordToScriptingSymbols(string kw, string scriptingSymbols)
        {
            List<string> splitSymbols = SplitScriptingSymbolsToList(scriptingSymbols);
            if (!splitSymbols.Contains(kw))
            {
                splitSymbols.Add(kw);
            }
            return CombineSymbolsListToString(splitSymbols);
        }

        public static string RemoveKeywordFromScriptingSymbols(string kw, string scriptingSymbols)
        {
            List<string> splitSymbols = SplitScriptingSymbolsToList(scriptingSymbols);
            splitSymbols.RemoveAll(s => s.Equals(kw));
            return CombineSymbolsListToString(splitSymbols);
        }

        private static List<Type> GetAllLoadedTypes()
        {
            List<Type> loadedTypes = new List<Type>();
            List<string> typeName = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.IsVisible && !t.IsGenericType)
                    {
                        typeName.Add(t.Name);
                        loadedTypes.Add(t);
                    }
                }
            }
            return loadedTypes;
        }
    }
}
