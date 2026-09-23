using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vertigo.Fortune.Presentation;

namespace Vertigo.Fortune.Editor
{
    /// <summary>Repeatable brief compliance checks and locally distributable player builds.</summary>
    [InitializeOnLoad]
    public static class FortuneVerification
    {
        private const string PendingBuildKey = "Fortune.Verification.PendingBuild";
        private const string RequestedAtKey = "Fortune.Verification.RequestedAt";
        private const string SwitchingKey = "Fortune.Verification.Switching";
        private const string AuditPath = "Builds/Reports/ui-audit.txt";
        private const string AndroidReportPath = "Builds/Reports/android-build.txt";
        private const string MacReportPath = "Builds/Reports/macos-build.txt";
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool isExecutingBuild;

        static FortuneVerification()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingBuildKey, string.Empty)))
                ScheduleBuild();
        }

        [MenuItem("Fortune/Validate UI")]
        public static void ValidateUI()
        {
            AuditLoadedScene();
        }

        /// <summary>Audits the actual loaded hierarchy, including inactive UI and runtime modals.</summary>
        public static bool AuditLoadedScene()
        {
            var report = new StringBuilder();
            var failures = new List<string>();
            var notes = new List<string>();
            var roots = LoadedRoots();
            var canvases = Components<Canvas>(roots).Where(c => c.isRootCanvas).ToArray();
            var graphics = Components<Graphic>(roots);
            var images = Components<Image>(roots);
            var buttons = Components<Button>(roots);
            var actions = Components<ActionButton>(roots);
            var screens = Components<FortuneScreen>(roots);
            var wheels = Components<WheelView>(roots);
            report.AppendLine("FORTUNE RUN — UI COMPLIANCE AUDIT");
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("Mode: " + (Application.isPlaying ? "Play Mode (live hierarchy)" : "Edit Mode (serialized hierarchy)"));
            report.AppendLine("Loaded scenes: " + string.Join(", ", roots.Select(r => r.scene.path).Distinct()));
            report.AppendLine();

            if (canvases.Length == 0) failures.Add("No root Canvas is loaded. Open the FortuneRun scene first.");
            if (screens.Length == 0) failures.Add("No FortuneScreen is loaded. Open the FortuneRun scene first.");
            foreach (var canvas in canvases)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize
                    || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                    failures.Add(PathOf(canvas) + ": requires Scale With Screen Size / Expand.");
            }

            foreach (var legacyText in Components<UnityEngine.UI.Text>(roots))
                failures.Add(PathOf(legacyText) + ": legacy UI.Text must be replaced with TextMeshPro.");

            var hitSurfaces = 0;
            foreach (var graphic in graphics)
            {
                var interactive = IsInputSurface(graphic, buttons);
                if (interactive) hitSurfaces++;
                if (!interactive && graphic.raycastTarget)
                    failures.Add(PathOf(graphic) + ": decorative Graphic has Raycast Target enabled.");
                var maskable = graphic as MaskableGraphic;
                if (maskable != null && maskable.maskable && !NeedsMask(maskable))
                    failures.Add(PathOf(graphic) + ": unnecessary Maskable is enabled.");
                if (!graphic.name.StartsWith("ui_", StringComparison.Ordinal))
                    notes.Add(PathOf(graphic) + ": consider a general-to-specific ui_ name.");
            }

            foreach (var image in images)
            {
                // Solid fills and transparent hit surfaces have no sprite to stretch.
                if (image.sprite == null) continue;
                if (image.sprite.border.sqrMagnitude > 0)
                {
                    if (image.type != Image.Type.Sliced)
                        failures.Add(PathOf(image) + ": bordered sprite requires Image.Type.Sliced.");
                }
                else if (image.type == Image.Type.Simple && !image.preserveAspect)
                    failures.Add(PathOf(image) + ": simple sprite requires Preserve Aspect.");
            }

            foreach (var button in buttons)
            {
                if (button.onClick.GetPersistentEventCount() != 0)
                    failures.Add(PathOf(button) + ": Inspector OnClick listeners are prohibited; bind in code.");
            }

            foreach (var action in actions) ValidateActionButton(action, failures);
            foreach (var screen in screens) ValidateMutableReferences(screen, failures);
            foreach (var wheel in wheels) ValidateMutableReferences(wheel, failures);
            foreach (var animator in Components<Animator>(roots))
            {
                if (animator.GetComponent<Canvas>() != null || animator.GetComponent<FortuneScreen>() != null
                    || animator.GetComponent<Button>() != null)
                    failures.Add(PathOf(animator) + ": move the UI Animator to a dedicated child motion transform.");
            }

            foreach (var transform in Components<Transform>(roots))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    failures.Add(PathOf(transform) + ": missing script reference.");
                var rect = transform as RectTransform;
                if (rect == null) continue;
                if (rect.anchorMin.x > rect.anchorMax.x || rect.anchorMin.y > rect.anchorMax.y)
                    failures.Add(PathOf(rect) + ": inverted anchors.");
                if (rect.pivot.x < 0 || rect.pivot.x > 1 || rect.pivot.y < 0 || rect.pivot.y > 1)
                    notes.Add(PathOf(rect) + ": pivot lies outside the normal 0..1 range; verify across aspects.");
            }

            try
            {
                FortuneContentBuilder.ValidateContent();
                report.AppendLine("Content: PASS — all 58 supplied sprites, catalogue and zone layouts validated.");
            }
            catch (Exception exception)
            {
                failures.Add("Content validation: " + exception.Message);
            }

            report.AppendLine("Root canvases: " + canvases.Length);
            report.AppendLine("Graphics: " + graphics.Length + " (" + hitSurfaces + " intentional input surfaces)");
            report.AppendLine("Sprite images: " + images.Count(i => i.sprite != null));
            report.AppendLine("Code-bound buttons: " + buttons.Length + "; ActionButton components: " + actions.Length);
            report.AppendLine("Mutable text and sprite references require _value; animation-only _motion visuals are excluded.");
            report.AppendLine("Maskable is allowed only under an actual Mask / RectMask2D, or on a Mask's own graphic.");
            report.AppendLine("Aspect fit and animation appearance additionally require the supplied 20:9, 16:9 and 4:3 captures.");
            report.AppendLine();
            report.AppendLine("RESULT: " + (failures.Count == 0 ? "PASS" : "FAIL") + " (" + failures.Count + " errors)");
            foreach (var failure in failures) report.AppendLine("ERROR: " + failure);
            foreach (var note in notes.Distinct()) report.AppendLine("NOTE: " + note);
            WriteReport(AuditPath, report.ToString());
            var marker = "FORTUNE_UI_AUDIT_" + (failures.Count == 0 ? "PASSED" : "FAILED")
                         + ": " + Path.GetFullPath(AuditPath) + " (" + failures.Count + " errors)";
            if (failures.Count == 0) Debug.Log(marker); else Debug.LogError(marker);
            return failures.Count == 0;
        }

        [MenuItem("Fortune/Build Android APK")]
        public static void BuildAndroidApk()
        {
            QueueBuild("Android");
        }

        [MenuItem("Fortune/Build macOS")]
        public static void BuildMacOS()
        {
            QueueBuild("macOS");
        }

        private static void QueueBuild(string platform)
        {
            if (isExecutingBuild || BuildPipeline.isBuildingPlayer
                || !string.IsNullOrEmpty(SessionState.GetString(PendingBuildKey, string.Empty)))
            {
                Debug.LogWarning("FORTUNE_BUILD_ALREADY_QUEUED: wait for the current build report.");
                return;
            }
            SessionState.SetString(PendingBuildKey, platform);
            SessionState.SetString(RequestedAtKey, DateTime.UtcNow.ToString("O"));
            SessionState.SetBool(SwitchingKey, false);
            WriteReport(ReportFor(platform), "STATUS: QUEUED\nPlatform: " + platform
                + "\nRequested UTC: " + SessionState.GetString(RequestedAtKey, string.Empty) + "\n");
            Debug.Log("FORTUNE_" + MarkerFor(platform) + "_BUILD_QUEUED: delayed until the Editor is idle.");
            // The menu invocation returns before compilation, target switching, or BuildPlayer starts.
            ScheduleBuild();
        }

        private static void ScheduleBuild()
        {
            // delayCall depends on an Inspector update and can stall in an unfocused Editor.
            // A one-shot Editor update continues to run for background Editor tasks.
            EditorApplication.delayCall -= ProcessQueuedBuild;
            EditorApplication.update -= ProcessQueuedBuild;
            EditorApplication.update += ProcessQueuedBuild;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void ProcessQueuedBuild()
        {
            // Detach before doing any work; busy states explicitly schedule their next attempt.
            EditorApplication.delayCall -= ProcessQueuedBuild;
            EditorApplication.update -= ProcessQueuedBuild;
            if (isExecutingBuild) return;
            var platform = SessionState.GetString(PendingBuildKey, string.Empty);
            if (string.IsNullOrEmpty(platform)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            {
                ScheduleBuild();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                ScheduleBuild();
                return;
            }

            var target = platform == "Android" ? BuildTarget.Android : BuildTarget.StandaloneOSX;
            var group = platform == "Android" ? BuildTargetGroup.Android : BuildTargetGroup.Standalone;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                FailBuild(platform, "The " + platform + " build support module is not installed for this Unity Editor.");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (!SessionState.GetBool(SwitchingKey, false))
                {
                    SessionState.SetBool(SwitchingKey, true);
                    WriteReport(ReportFor(platform), "STATUS: SWITCHING_TARGET\nPlatform: " + platform + "\n");
                    if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(group, target))
                    {
                        FailBuild(platform, "Unity could not switch to the requested build target.");
                        return;
                    }
                }
                ScheduleBuild();
                return;
            }

            SessionState.EraseString(PendingBuildKey);
            SessionState.SetBool(SwitchingKey, false);
            isExecutingBuild = true;
            try
            {
                ExecuteBuild(platform, target);
            }
            finally
            {
                isExecutingBuild = false;
            }
        }

        private static void ExecuteBuild(string platform, BuildTarget target)
        {
            var report = new StringBuilder();
            report.AppendLine("FORTUNE RUN — " + platform + " BUILD");
            report.AppendLine("Requested UTC: " + SessionState.GetString(RequestedAtKey, string.Empty));
            report.AppendLine("Started UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("STATUS: BUILDING");
            WriteReport(ReportFor(platform), report.ToString());
            Debug.Log("FORTUNE_" + MarkerFor(platform) + "_BUILD_STARTED");
            try
            {
                if (!File.Exists("Assets/Fortune/Scenes/FortuneRun.unity"))
                    throw new InvalidOperationException("Open and save Assets/Fortune/Scenes/FortuneRun.unity before building.");
                EditorSceneManager.SaveOpenScenes();
                FortuneContentBuilder.ValidateContent();
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.runInBackground = true;

                string outputPath;
                if (target == BuildTarget.Android)
                {
                    ConfigureAndroid(report);
                    outputPath = Path.GetFullPath("Builds/FortuneRun.apk");
                }
                else
                {
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                    PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2); // Universal Apple Silicon / Intel.
                    PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                    PlayerSettings.resizableWindow = true;
                    outputPath = Path.GetFullPath("Builds/FortuneRun.app");
                    report.AppendLine("Backend: Mono; architecture: universal macOS.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                AssetDatabase.SaveAssets();
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Fortune/Scenes/FortuneRun.unity" },
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.None
                });
                var summary = build.summary;
                var artifactExists = target == BuildTarget.Android
                    ? File.Exists(outputPath) && new FileInfo(outputPath).Length > 0
                    : Directory.Exists(outputPath)
                      && File.Exists(Path.Combine(outputPath, "Contents", "Info.plist"))
                      && Directory.Exists(Path.Combine(outputPath, "Contents", "MacOS"))
                      && Directory.GetFiles(Path.Combine(outputPath, "Contents", "MacOS")).Length > 0;
                var succeeded = summary.result == BuildResult.Succeeded && summary.totalErrors == 0 && artifactExists;
                report.AppendLine("Unity reported result: " + summary.result.ToString().ToUpperInvariant());
                report.AppendLine("STATUS: " + (succeeded ? "SUCCEEDED" : "FAILED"));
                report.AppendLine("Artifact: " + summary.outputPath);
                report.AppendLine("Artifact verification: " + (artifactExists ? "PASS" : "FAIL — missing or empty output"));
                report.AppendLine("Unity build data size bytes: " + summary.totalSize);
                if (target == BuildTarget.Android && artifactExists)
                    report.AppendLine("APK file size bytes: " + new FileInfo(outputPath).Length);
                report.AppendLine("Elapsed: " + summary.totalTime);
                report.AppendLine("Errors: " + summary.totalErrors + "; warnings: " + summary.totalWarnings);
                if (!artifactExists) report.AppendLine("ERROR: expected player artifact is missing or empty: " + outputPath);
                if (summary.totalErrors > 0) report.AppendLine("ERROR: build validation rejected Unity's result because build errors were reported.");
                foreach (var step in build.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                            report.AppendLine(message.type.ToString().ToUpperInvariant() + ": " + message.content);
                report.AppendLine("Finished UTC: " + DateTime.UtcNow.ToString("O"));
                WriteReport(ReportFor(platform), report.ToString());
                var marker = "FORTUNE_" + MarkerFor(platform) + "_BUILD_"
                    + (succeeded ? "SUCCEEDED" : "FAILED") + ": " + outputPath;
                if (succeeded) Debug.Log(marker); else Debug.LogError(marker);
            }
            catch (Exception exception)
            {
                report.AppendLine("STATUS: FAILED");
                report.AppendLine(exception.ToString());
                WriteReport(ReportFor(platform), report.ToString());
                Debug.LogError("FORTUNE_" + MarkerFor(platform) + "_BUILD_FAILED: " + exception.Message);
            }
        }

        private static void ConfigureAndroid(StringBuilder report)
        {
            var engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
            ConfigureEmbeddedTool("sdkRootPath", Path.Combine(engine, "SDK"), report);
            ConfigureEmbeddedTool("ndkRootPath", Path.Combine(engine, "NDK"), report);
            ConfigureEmbeddedTool("jdkRootPath", Path.Combine(engine, "OpenJDK"), report);
            // Enum values for removed SDK levels remain present with Obsolete attributes.
            // Filter those out instead of treating their numeric values as supported.
            var availableLevels = typeof(AndroidSdkVersions).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => !Attribute.IsDefined(field, typeof(ObsoleteAttribute)))
                .Select(field => (int)(AndroidSdkVersions)field.GetValue(null)).Where(value => value >= 23).ToArray();
            var minimum = availableLevels.Length > 0 ? availableLevels.Min() : 23;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minimum;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.vertigo.fortunerun");
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            var il2cppAvailable = Directory.Exists(Path.Combine(engine, "Variations", "il2cpp"))
                                  && Directory.Exists(Path.Combine(engine, "NDK"));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,
                il2cppAvailable ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = il2cppAvailable ? AndroidArchitecture.ARM64 : AndroidArchitecture.ARMv7;
            report.AppendLine("Android minimum SDK: " + minimum + "; target SDK: automatic (highest installed supported SDK).");
            if (minimum > 23) report.AppendLine("Compatibility note: this Unity Editor supports Android API "
                + minimum + " and later; the older API 23 baseline is unavailable in this Editor.");
            report.AppendLine("Backend: " + (il2cppAvailable ? "IL2CPP / ARM64" : "Mono / ARMv7 (IL2CPP module unavailable)"));
            report.AppendLine("Package: APK; development build: false; signing uses current project settings.");
        }

        private static void ConfigureEmbeddedTool(string propertyName, string directory, StringBuilder report)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Install Unity's embedded Android tools: missing " + directory);
            var settingsType = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            if (settingsType == null)
            {
                settingsType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings", false))
                    .FirstOrDefault(type => type != null);
            }
            var property = settingsType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite)
                throw new InvalidOperationException("Unity's Android tools are not initialized. Restart the Editor after installing Android Build Support. Missing setting: " + propertyName);
            property.SetValue(null, directory, null);
            report.AppendLine("Embedded " + propertyName + ": " + directory);
        }

        private static void ValidateActionButton(ActionButton action, List<string> failures)
        {
            var onValidate = action.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (onValidate == null) failures.Add(PathOf(action) + ": ActionButton requires OnValidate reference binding.");
            var serialized = new SerializedObject(action);
            var button = serialized.FindProperty("button");
            var label = serialized.FindProperty("label");
            if (button == null || button.objectReferenceValue != action.GetComponent<Button>())
                failures.Add(PathOf(action) + ": ActionButton's automatic Button reference is missing or incorrect.");
            var expectedLabel = action.GetComponentInChildren<TMP_Text>(true);
            if (expectedLabel != null && (label == null || label.objectReferenceValue != expectedLabel))
                failures.Add(PathOf(action) + ": ActionButton's automatic label reference is missing or incorrect.");
            if (expectedLabel != null && !expectedLabel.name.EndsWith("_value", StringComparison.Ordinal))
                failures.Add(PathOf(expectedLabel) + ": mutable button label requires the _value suffix.");
            if (action.Visual == null || action.Visual == action.transform || !action.Visual.IsChildOf(action.transform))
                failures.Add(PathOf(action) + ": button animation requires a dedicated child visual transform.");
        }

        private static void ValidateMutableReferences(Component view, List<string> failures)
        {
            foreach (var field in view.GetType().GetFields(InstanceFields))
            {
                if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
                var value = field.GetValue(view);
                if (value is Graphic) ValidateValueName((Graphic)value, view, field.Name, failures);
                else if (value is IEnumerable && !(value is string))
                    foreach (var item in (IEnumerable)value)
                        if (item is Graphic) ValidateValueName((Graphic)item, view, field.Name, failures);
            }
        }

        private static void ValidateValueName(Graphic graphic, Component owner, string field, List<string> failures)
        {
            if (graphic.name.EndsWith("_motion", StringComparison.Ordinal)) return;
            if (!graphic.name.EndsWith("_value", StringComparison.Ordinal))
                failures.Add(PathOf(graphic) + ": mutable " + owner.GetType().Name + "." + field + " requires the _value suffix.");
        }

        private static bool IsInputSurface(Graphic graphic, Button[] buttons)
        {
            if (buttons.Any(button => button.targetGraphic == graphic)) return true;
            var selectable = graphic.GetComponent<Selectable>();
            if (selectable != null && selectable.targetGraphic == graphic) return true;
            if (graphic.GetComponents<Component>().Any(component => component is IPointerClickHandler
                || component is IPointerDownHandler || component is IBeginDragHandler)) return true;
            return graphic.name.IndexOf("input_blocker", StringComparison.OrdinalIgnoreCase) >= 0
                   || graphic.name.IndexOf("modal_scrim", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool NeedsMask(MaskableGraphic graphic)
        {
            if (graphic.GetComponent<Mask>() != null) return true;
            var parent = graphic.transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<Mask>() != null || parent.GetComponent<RectMask2D>() != null) return true;
                parent = parent.parent;
            }
            return false;
        }

        private static GameObject[] LoadedRoots()
        {
            var roots = new List<GameObject>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            return roots.ToArray();
        }

        private static T[] Components<T>(IEnumerable<GameObject> roots) where T : Component
        {
            return roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static string PathOf(Component component)
        {
            var path = component.name;
            var parent = component.transform.parent;
            while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
            return path;
        }

        private static void FailBuild(string platform, string error)
        {
            SessionState.EraseString(PendingBuildKey);
            SessionState.SetBool(SwitchingKey, false);
            WriteReport(ReportFor(platform), "STATUS: FAILED\nPlatform: " + platform + "\nERROR: " + error + "\n");
            Debug.LogError("FORTUNE_" + MarkerFor(platform) + "_BUILD_FAILED: " + error);
        }

        private static string ReportFor(string platform) => platform == "Android" ? AndroidReportPath : MacReportPath;
        private static string MarkerFor(string platform) => platform == "Android" ? "ANDROID" : "MACOS";

        private static void WriteReport(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
