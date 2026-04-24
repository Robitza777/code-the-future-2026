using UnityEngine;
using Firebase;
using Firebase.Database;

[RequireComponent(typeof(AudioSource))]
public class ServerRoomManager : MonoBehaviour
{
    [Header("Sensor Values")]
    public float temperature = 25f;
    public float humidity = 50f;
    public float gasLevel = 1000f;
    public float vibration = 0f;

    [Header("Scene References")]
    public FanRotate fanScript;        // drag FanPivot here
    public Transform relayCylinder;    // cilindrul din relay
    public Renderer ledverde;
    public Renderer ledgalben;
    public Renderer ledrosu;
    public AudioSource buzzerAudio;

    [Header("Relay Settings")]
    public float relayMove = 4f;
    public float relaySpeed = 6f;

    [Header("LED Colors")]
    public Color dimColor = new Color(0.15f, 0.15f, 0.15f);
    public Color greenOn = Color.green;
    public Color yellowOn = Color.yellow;
    public Color redOn = Color.red;

    // ── internal state ───────────────────────────────────────
    Vector3 relayStartPos;
    Vector3 relayTargetPos;

    bool warningMode = false;
    bool criticalMode = false;
    float beepTimer = 0f;

    const float BEEP_FREQ = 880f;
    const float BEEP_DUR = 0.12f;
    const int SAMPLE_RATE = 44100;
    AudioClip beepClip;

    // ── Firebase ──────────────────────────────────────────────
    DatabaseReference liveRef;
    DatabaseReference whatifRef;
    bool firebaseReady = false;

    // date primite din Firebase (actualizate pe main thread)
    float fb_temp = 25f;
    float fb_humidity = 50f;
    float fb_gas = 1000f;
    float fb_vibration = 0f;
    bool fb_whatifActive = false;

    // queue pentru main thread
    readonly System.Collections.Generic.Queue<System.Action> _mainQueue
        = new System.Collections.Generic.Queue<System.Action>();

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        relayStartPos = relayCylinder.localPosition;
        relayTargetPos = relayStartPos;

        // Genereaza beep clip
        int n = Mathf.RoundToInt(SAMPLE_RATE * BEEP_DUR);
        float[] samples = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = (i < n * 0.8f) ? 1f : 1f - (i - n * 0.8f) / (n * 0.2f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * BEEP_FREQ * t) * 0.6f * env;
        }
        beepClip = AudioClip.Create("Beep", n, 1, SAMPLE_RATE, false);
        beepClip.SetData(samples, 0);

        buzzerAudio.clip = beepClip;
        buzzerAudio.loop = false;
        buzzerAudio.playOnAwake = false;

        ApplyLeds(false, false);

        // Initializeaza Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                liveRef = FirebaseDatabase.DefaultInstance.GetReference("serverroom/live");
                whatifRef = FirebaseDatabase.DefaultInstance.GetReference("serverroom/whatif");

                liveRef.ValueChanged += OnLiveData;
                whatifRef.ValueChanged += OnWhatIfData;

                firebaseReady = true;
                Debug.Log("[Firebase] Conectat! Ascult date live...");
            }
            else
            {
                Debug.LogError("[Firebase] Eroare dependente: " + task.Result);
            }
        });
    }

    // ── Callback Firebase LIVE ────────────────────────────────
    void OnLiveData(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null)
        {
            Debug.LogError("[Firebase] " + e.DatabaseError.Message);
            return;
        }

        // Daca whatif e activ, ignoram datele live
        if (fb_whatifActive) return;

        float t = ParseFloat(e.Snapshot.Child("temp").Value, 25f);
        float h = ParseFloat(e.Snapshot.Child("humidity").Value, 50f);
        float g = ParseFloat(e.Snapshot.Child("gas_raw").Value, 1000f);
        float v = ParseFloat(e.Snapshot.Child("vibration").Value, 0f);

        // Actualizam pe main thread
        lock (_mainQueue)
        {
            _mainQueue.Enqueue(() =>
            {
                fb_temp = t;
                fb_humidity = h;
                fb_gas = g;
                fb_vibration = v;
                ApplySensorValues();
                Debug.Log($"[Live] T={t:F1} H={h:F1} G={g:F0} V={v:F3}");
            });
        }
    }

    // ── Callback Firebase WHAT IF ─────────────────────────────
    void OnWhatIfData(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null) return;

        bool active = ParseBool(e.Snapshot.Child("active").Value);
        float t = ParseFloat(e.Snapshot.Child("temp").Value, 25f);
        float h = ParseFloat(e.Snapshot.Child("humidity").Value, 50f);
        float g = ParseFloat(e.Snapshot.Child("gas_raw").Value, 1000f);
        float v = ParseFloat(e.Snapshot.Child("vibration").Value, 0f);

        lock (_mainQueue)
        {
            _mainQueue.Enqueue(() =>
            {
                fb_whatifActive = active;
                if (active)
                {
                    fb_temp = t;
                    fb_humidity = h;
                    fb_gas = g;
                    fb_vibration = v;
                    ApplySensorValues();
                    Debug.Log($"[WhatIf] T={t:F1} H={h:F1} G={g:F0} V={v:F3}");
                }
            });
        }
    }

    // ── Aplica valorile Firebase pe variabilele locale ────────
    void ApplySensorValues()
    {
        temperature = fb_temp;
        humidity = fb_humidity;
        gasLevel = fb_gas;
        vibration = fb_vibration;
    }

    // ─────────────────────────────────────────────────────────
    void Update()
    {
        // Proceseaza coada main thread
        lock (_mainQueue)
        {
            while (_mainQueue.Count > 0)
                _mainQueue.Dequeue()?.Invoke();
        }

        // Taste pentru test manual (functioneaza in continuare)
        TestKeys();
        RunLogic();
        MoveRelay();
        HandleBuzzer();
    }

    // ─────────────────────────────────────────────────────────
    void TestKeys()
    {
        // Taste test manual — utile cand Firebase nu e conectat
        if (Input.GetKeyDown(KeyCode.T)) temperature += 2f;
        if (Input.GetKeyDown(KeyCode.Y)) humidity += 5f;
        if (Input.GetKeyDown(KeyCode.G)) gasLevel += 500f;
        if (Input.GetKeyDown(KeyCode.V)) vibration += 0.2f;

        if (Input.GetKeyDown(KeyCode.Alpha1)) temperature = Mathf.Max(0f, temperature - 2f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) humidity = Mathf.Max(0f, humidity - 5f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) gasLevel = Mathf.Max(0f, gasLevel - 500f);
        if (Input.GetKeyDown(KeyCode.Alpha4)) vibration = Mathf.Max(0f, vibration - 0.2f);

        if (Input.GetKeyDown(KeyCode.R))
        {
            temperature = 25f;
            humidity = 50f;
            gasLevel = 1000f;
            vibration = 0f;
        }
    }

    // ─────────────────────────────────────────────────────────
    void RunLogic()
    {
        warningMode = false;
        criticalMode = false;

        bool fanOn = false;
        bool relayOn = false;

        bool gasCritical = gasLevel > 2800f;
        bool tempCritical = temperature >= 30f;
        bool vibCritical = vibration > 0.8f;
        bool tempWarning = temperature >= 28f;
        bool humWarning = humidity > 70f;

        if (tempCritical)
        {
            criticalMode = true;
            fanOn = true;
            relayOn = true;
        }
        else if (gasCritical || vibCritical)
        {
            criticalMode = true;
        }
        else if (tempWarning)
        {
            warningMode = true;
            fanOn = true;
            relayOn = true;
        }
        else if (humWarning)
        {
            warningMode = true;
        }

        ApplyLeds(warningMode, criticalMode);

        if (fanScript != null)
            fanScript.isOn = fanOn;

        relayTargetPos = relayOn
            ? relayStartPos + new Vector3(0f, 0f, relayMove)
            : relayStartPos;
    }

    // ─────────────────────────────────────────────────────────
    void ApplyLeds(bool warning, bool critical)
    {
        if (critical)
        {
            SetLed(ledrosu, redOn);
            SetLed(ledgalben, dimColor);
            SetLed(ledverde, dimColor);
        }
        else if (warning)
        {
            SetLed(ledgalben, yellowOn);
            SetLed(ledrosu, dimColor);
            SetLed(ledverde, dimColor);
        }
        else
        {
            SetLed(ledverde, greenOn);
            SetLed(ledgalben, dimColor);
            SetLed(ledrosu, dimColor);
        }
    }

    // ─────────────────────────────────────────────────────────
    void MoveRelay()
    {
        relayCylinder.localPosition = Vector3.Lerp(
            relayCylinder.localPosition,
            relayTargetPos,
            relaySpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────────────────
    void HandleBuzzer()
    {
        float interval;

        if (criticalMode) interval = 0.5f;
        else if (warningMode) interval = 1.5f;
        else { beepTimer = 0f; return; }

        beepTimer += Time.deltaTime;
        if (beepTimer >= interval)
        {
            beepTimer = 0f;
            if (!buzzerAudio.isPlaying)
                buzzerAudio.Play();
        }
    }

    // ─────────────────────────────────────────────────────────
    void SetLed(Renderer rend, Color c)
    {
        if (rend == null) return;
        UnityEngine.Material mat = rend.material;
        mat.color = c;

        bool isOff =
            Mathf.Approximately(c.r, dimColor.r) &&
            Mathf.Approximately(c.g, dimColor.g) &&
            Mathf.Approximately(c.b, dimColor.b);

        if (mat.HasProperty("_EmissionColor"))
        {
            if (isOff) { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }
            else { mat.SetColor("_EmissionColor", c * 2f); mat.EnableKeyword("_EMISSION"); }
        }
    }

    // ─────────────────────────────────────────────────────────
    void OnDestroy()
    {
        if (liveRef != null) liveRef.ValueChanged -= OnLiveData;
        if (whatifRef != null) whatifRef.ValueChanged -= OnWhatIfData;
    }

    // ── Helpers parse ─────────────────────────────────────────
    float ParseFloat(object val, float fallback)
    {
        if (val == null) return fallback;
        return float.TryParse(val.ToString(), out float r) ? r : fallback;
    }

    bool ParseBool(object val)
    {
        if (val == null) return false;
        return val.ToString().ToLower() == "true";
    }
}