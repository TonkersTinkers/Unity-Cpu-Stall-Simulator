using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using UnityEngine;

public class CPUStallSimulator : MonoBehaviour
{
#if UNITY_EDITOR
    public static CPUStallSimulator Instance { get; private set; }

    [Header("Run Stuff")]
    [SerializeField] private bool autoKickoff = false;

    [SerializeField] private bool loopForevver = false;
    [SerializeField, Min(1)] private int runCount = 1;
    [SerializeField, Min(0f)] private float startDelay = 0.25f;
    [SerializeField, Min(0f)] private float waitBetweenRuns = 2f;

    [Header("Main Thread Mayhem")]
    [SerializeField] private bool freezeMainThread = true;

    [SerializeField, Min(0f)] private float freezeSeconds = 5f;
    [SerializeField] private bool chugFrames = false;
    [SerializeField, Min(0f)] private float chugSeconds = 10f;
    [SerializeField, Min(0f)] private float busyMsPerFrame = 30f;

    [Header("Backround Load")]
    [SerializeField] private bool runBackroundLoad = false;

    [SerializeField, Min(0)] private int workerCount = 0;
    [SerializeField, Range(0f, 1f)] private float workerLoad = 1f;

    [Header("Overlay Nonsence")]
    [SerializeField, TextArea(2, 3)] private string overlayText = "Simulating a slooow PC... please waite.";

    [SerializeField, Min(8)] private int overlayFontSize = 72;
    [SerializeField, Range(0f, 1f)] private float overlayBgAlpha = 0.6f;

    [Header("Env Tweeks")]
    [SerializeField] private bool forceVSyncOff = true;

    [SerializeField] private bool overrideFrameRate = true;
    [SerializeField] private int testFrameRate = -1;

    private Rect windowRect = new Rect(20f, 20f, 430f, 540f);
    private bool showUi = true;

    private bool isRunning;
    private bool showOverlay;
    private int oldVSync;
    private int oldFrameRate;
    private Coroutine runRoutine;

    private readonly List<Thread> workers = new List<Thread>();
    private volatile bool workersAlive;
    private static volatile int junkNumber = 1337;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        runCount = Mathf.Max(1, runCount);
        workerCount = Mathf.Max(0, workerCount);
    }

    private void Start()
    {
        if (autoKickoff)
        {
            StartRuns();
        }
    }

    private void OnDisable()
    {
        StopRuns();
        StopWorkers();
    }

    private void OnValidate()
    {
        if (runCount < 1)
        {
            runCount = 1;
        }

        if (startDelay < 0f)
        {
            startDelay = 0f;
        }

        if (waitBetweenRuns < 0f)
        {
            waitBetweenRuns = 0f;
        }

        if (freezeSeconds < 0f)
        {
            freezeSeconds = 0f;
        }

        if (chugSeconds < 0f)
        {
            chugSeconds = 0f;
        }

        if (busyMsPerFrame < 0f)
        {
            busyMsPerFrame = 0f;
        }

        if (workerCount < 0)
        {
            workerCount = 0;
        }

        if (overlayFontSize < 8)
        {
            overlayFontSize = 8;
        }
    }

    private void StartRuns()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        ApplyOverrides();

        if (runBackroundLoad)
        {
            StartWorkers();
        }

        runRoutine = StartCoroutine(RunLoop());
    }

    private void StartOneNow()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        ApplyOverrides();

        if (runBackroundLoad)
        {
            StartWorkers();
        }

        runRoutine = StartCoroutine(RunJustOne());
    }

    private void StopRuns()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;

        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }

        showOverlay = false;
        StopWorkers();
        RestoreOverrides();
    }

    private IEnumerator RunJustOne()
    {
        yield return RunOnePass();
        StopRuns();
    }

    private IEnumerator RunLoop()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        if (loopForevver)
        {
            while (isRunning)
            {
                yield return RunOnePass();

                if (!isRunning)
                {
                    break;
                }

                if (waitBetweenRuns > 0f)
                {
                    yield return new WaitForSecondsRealtime(waitBetweenRuns);
                }
            }
        }
        else
        {
            for (int i = 0; i < runCount && isRunning; i++)
            {
                yield return RunOnePass();

                if (!isRunning)
                {
                    break;
                }

                if (i < runCount - 1 && waitBetweenRuns > 0f)
                {
                    yield return new WaitForSecondsRealtime(waitBetweenRuns);
                }
            }
        }

        StopRuns();
    }

    private IEnumerator RunOnePass()
    {
        showOverlay = true;
        yield return null;
        yield return new WaitForEndOfFrame();

        if (freezeMainThread)
        {
            FreezeForAWhile(freezeSeconds);
        }

        if (chugFrames)
        {
            yield return ChugForAWhile(chugSeconds, busyMsPerFrame);
        }

        showOverlay = false;
        yield return null;
    }

    private void FreezeForAWhile(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        Stopwatch sw = Stopwatch.StartNew();

        while (sw.Elapsed.TotalSeconds < seconds)
        {
            for (int i = 0; i < 10000; i++)
            {
                junkNumber = unchecked(junkNumber * 1664525 + 1013904223);
            }

            Thread.SpinWait(1000);
        }
    }

    private IEnumerator ChugForAWhile(float seconds, float msPerFrame)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        if (msPerFrame <= 0f)
        {
            yield break;
        }

        double endTime = Time.realtimeSinceStartupAsDouble + seconds;

        while (isRunning && Time.realtimeSinceStartupAsDouble < endTime)
        {
            BurnMs(msPerFrame);
            yield return null;
        }
    }

    private void BurnMs(float ms)
    {
        if (ms <= 0f)
        {
            return;
        }

        double secs = ms / 1000d;
        Stopwatch sw = Stopwatch.StartNew();

        while (sw.Elapsed.TotalSeconds < secs)
        {
            for (int i = 0; i < 2000; i++)
            {
                junkNumber = unchecked(junkNumber * 1103515245 + 12345);
            }

            Thread.SpinWait(500);
        }
    }

    private void StartWorkers()
    {
        if (workersAlive)
        {
            return;
        }

        workersAlive = true;

        int count = workerCount;
        if (count <= 0)
        {
            count = Math.Max(1, Environment.ProcessorCount - 1);
        }

        for (int i = 0; i < count; i++)
        {
            Thread t = new Thread(WorkerLoop);
            t.IsBackground = true;
            t.Priority = System.Threading.ThreadPriority.Highest;
            workers.Add(t);
            t.Start();
        }
    }

    private void StopWorkers()
    {
        if (!workersAlive)
        {
            return;
        }

        workersAlive = false;

        foreach (Thread t in workers)
        {
            if (t == null)
            {
                continue;
            }

            try
            {
                if (t.IsAlive)
                {
                    t.Join(200);
                }
            }
            catch
            {
            }
        }

        workers.Clear();
    }

    private void WorkerLoop()
    {
        Stopwatch sw = new Stopwatch();

        while (workersAlive)
        {
            double cycleMs = 10d;
            double busyMs = Mathf.Clamp01(workerLoad) * (float)cycleMs;
            double restMs = cycleMs - busyMs;

            sw.Restart();

            while (workersAlive && sw.Elapsed.TotalMilliseconds < busyMs)
            {
                for (int i = 0; i < 4000; i++)
                {
                    junkNumber = unchecked(junkNumber * 22695477 + 1);
                }

                Thread.SpinWait(1000);
            }

            if (restMs > 0d)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(restMs));
            }
        }
    }

    private void ApplyOverrides()
    {
        if (forceVSyncOff)
        {
            oldVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
        }

        if (overrideFrameRate)
        {
            oldFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = testFrameRate;
        }
    }

    private void RestoreOverrides()
    {
        if (forceVSyncOff)
        {
            QualitySettings.vSyncCount = oldVSync;
        }

        if (overrideFrameRate)
        {
            Application.targetFrameRate = oldFrameRate;
        }
    }

    #region Editor Only Logic

    private void OnGUI()
    {
        if (showOverlay)
        {
            DrawOverlay();
        }

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "CPU Stall Simulater");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        showUi = GUILayout.Toggle(showUi, showUi ? "Hide Stuff" : "Show Stuff");

        if (showUi)
        {
            GUILayout.Label("Run Stuff");
            runCount = DrawIntThing(runCount, 1, 1000, "Run Count");
            startDelay = DrawFloatThing(startDelay, 0f, 60f, "Start Delay (s)");
            waitBetweenRuns = DrawFloatThing(waitBetweenRuns, 0f, 3600f, "Wait Between Runs (s)");
            loopForevver = GUILayout.Toggle(loopForevver, "Loop Forevver");

            GUILayout.Space(6f);
            GUILayout.Label("Main Thread Mayhem");
            freezeMainThread = GUILayout.Toggle(freezeMainThread, "Do Big Freeze");
            freezeSeconds = DrawFloatThing(freezeSeconds, 0f, 3600f, "Freeze Time (s)");
            chugFrames = GUILayout.Toggle(chugFrames, "Chug Frams");
            chugSeconds = DrawFloatThing(chugSeconds, 0f, 3600f, "Chug Time (s)");
            busyMsPerFrame = DrawFloatThing(busyMsPerFrame, 0f, 1000f, "Busy Per Frame (ms)");

            GUILayout.Space(6f);
            GUILayout.Label("Backround Load");
            runBackroundLoad = GUILayout.Toggle(runBackroundLoad, "Run Backround Load");
            workerCount = DrawIntThing(workerCount, 0, Environment.ProcessorCount * 8, "Workers (0 auto)");
            workerLoad = DrawFloatThing(workerLoad, 0f, 1f, "Worker Load");

            GUILayout.Space(6f);
            GUILayout.Label("Overlay Nonsence");
            overlayText = GUILayout.TextField(overlayText);
            overlayFontSize = DrawIntThing(overlayFontSize, 8, 256, "Font Size");
            overlayBgAlpha = DrawFloatThing(overlayBgAlpha, 0f, 1f, "BG Alpha");

            GUILayout.Space(6f);
            GUILayout.Label("Env Tweeks");
            forceVSyncOff = GUILayout.Toggle(forceVSyncOff, "Force VSync Off");
            overrideFrameRate = GUILayout.Toggle(overrideFrameRate, "Override Frame Rate");
            testFrameRate = DrawIntThing(testFrameRate, -1, 240, "Target FPS");
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();

        if (!isRunning)
        {
            if (GUILayout.Button("Kick It Off"))
            {
                StartRuns();
            }
        }
        else
        {
            if (GUILayout.Button("Stop It"))
            {
                StopRuns();
            }
        }

        if (GUILayout.Button("One Run Now"))
        {
            StartOneNow();
        }

        GUILayout.EndHorizontal();
        GUI.DragWindow();
        GUILayout.EndVertical();
    }

    private void DrawOverlay()
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, overlayBgAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = overlayFontSize;
        style.fontStyle = FontStyle.Bold;
        style.wordWrap = true;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), overlayText, style);
    }

    private float DrawFloatThing(float value, float min, float max, string label)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(180f));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180f));

        string text = GUILayout.TextField(value.ToString("0.##", CultureInfo.InvariantCulture), GUILayout.Width(60f));
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            value = parsed;
        }

        GUILayout.EndHorizontal();
        return Mathf.Clamp(value, min, max);
    }

    private int DrawIntThing(int value, int min, int max, string label)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(180f));
        value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180f)));

        string text = GUILayout.TextField(value.ToString(), GUILayout.Width(60f));
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            value = parsed;
        }

        GUILayout.EndHorizontal();
        return Mathf.Clamp(value, min, max);
    }

    #endregion Editor Only Logic

#endif
}
