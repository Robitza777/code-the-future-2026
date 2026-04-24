//using Assimp;
using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ServerRoomManager : MonoBehaviour
{
    [Header("Sensor Values")]
    public float temperature = 25f;
    public float humidity = 50f;
    public float gasLevel = 1000f;
    public float vibration = 0f;

    [Header("Scene References")]
    public FanRotate fanScript;      // drag FanPivot here
    public Transform relayCylinder;  // cilindrul din relay
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

    Vector3 relayStartPos;
    Vector3 relayTargetPos;

    bool warningMode = false;
    bool criticalMode = false;
    float beepTimer = 0f;

    const float BEEP_FREQ = 880f;
    const float BEEP_DUR = 0.12f;
    const int SAMPLE_RATE = 44100;
    AudioClip beepClip;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        relayStartPos = relayCylinder.localPosition;
        relayTargetPos = relayStartPos;
        relayCylinder.localPosition = relayStartPos;

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
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        TestKeys();
        RunLogic();
        MoveRelay();
        HandleBuzzer();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIMESTE DATE DIN FIREBASE REST
    // ─────────────────────────────────────────────────────────────────────────
    public void ReceiveSensorData(string json)
    {
        SensorPayload data = JsonUtility.FromJson<SensorPayload>(json);
        if (data == null) return;

        temperature = data.temp;
        humidity = data.humidity;
        gasLevel = data.gas_raw;
        vibration = data.vibration;

        Debug.Log("[Firebase] Date primite: " + json);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void TestKeys()
    {
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

    // ─────────────────────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────
    void MoveRelay()
    {
        relayCylinder.localPosition = Vector3.Lerp(
            relayCylinder.localPosition,
            relayTargetPos,
            relaySpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    void HandleBuzzer()
    {
        float interval;

        if (criticalMode)
            interval = 0.5f;
        else if (warningMode)
            interval = 1.5f;
        else
        {
            beepTimer = 0f;
            return;
        }

        beepTimer += Time.deltaTime;
        if (beepTimer >= interval)
        {
            beepTimer = 0f;
            if (!buzzerAudio.isPlaying)
                buzzerAudio.Play();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
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
            if (isOff)
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
            else
            {
                mat.SetColor("_EmissionColor", c * 2f);
                mat.EnableKeyword("_EMISSION");
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MODEL JSON DIN FIREBASE
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class SensorPayload
{
    public float temp;
    public float humidity;
    public float gas_raw;
    public float vibration;
    public string state;
}