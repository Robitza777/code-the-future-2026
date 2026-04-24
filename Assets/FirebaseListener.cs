using UnityEngine;
using Firebase;
using Firebase.Database;

public class FirebaseListener : MonoBehaviour
{
    DatabaseReference liveRef;
    DatabaseReference whatifRef;

    // ── conecteaza astea in Inspector la obiectele din scena ──
    public Light statusLight;
    public GameObject fanObject;      // obiectul ventilator din scena
    public GameObject smokeEffect;    // efect particule fum
    public GameObject serverRack;     // rack-ul de servere

    bool firebaseReady = false;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                liveRef = FirebaseDatabase.DefaultInstance.GetReference("serverroom/live");
                whatifRef = FirebaseDatabase.DefaultInstance.GetReference("serverroom/whatif");

                liveRef.ValueChanged += OnLiveData;
                whatifRef.ValueChanged += OnWhatIfData;

                firebaseReady = true;
                Debug.Log("[Firebase] Conectat si asculta date!");
            }
            else
            {
                Debug.LogError("[Firebase] Eroare: " + task.Result);
            }
        });
    }

    // ── Date LIVE de la ESP32 ──────────────────────────────────
    void OnLiveData(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null) return;

        float temp = float.Parse(e.Snapshot.Child("temp").Value?.ToString() ?? "0");
        float humidity = float.Parse(e.Snapshot.Child("humidity").Value?.ToString() ?? "0");
        int gas = int.Parse(e.Snapshot.Child("gas_raw").Value?.ToString() ?? "0");
        float vibration = float.Parse(e.Snapshot.Child("vibration").Value?.ToString() ?? "0");
        string state = e.Snapshot.Child("state").Value?.ToString() ?? "OK";

        // Firebase vine pe alt thread — folosim dispatcher
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
            ApplyToScene(temp, humidity, gas, vibration, state)
        );
    }

    // ── Date WHAT IF de pe dashboard ──────────────────────────
    void OnWhatIfData(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null) return;

        bool active = bool.Parse(e.Snapshot.Child("active").Value?.ToString() ?? "false");
        if (!active) return;

        float temp = float.Parse(e.Snapshot.Child("temp").Value?.ToString() ?? "0");
        float humidity = float.Parse(e.Snapshot.Child("humidity").Value?.ToString() ?? "0");
        int gas = int.Parse(e.Snapshot.Child("gas_raw").Value?.ToString() ?? "0");
        float vibration = float.Parse(e.Snapshot.Child("vibration").Value?.ToString() ?? "0");

        string state = "OK";
        if (temp > 32 || gas > 2800 || vibration > 0.8f) state = "CRITIC";
        else if (temp > 28 || humidity > 70) state = "WARN";

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
            ApplyToScene(temp, humidity, gas, vibration, state)
        );
    }

    // ── Aplica valorile pe scena 3D ───────────────────────────
    void ApplyToScene(float temp, float humidity, int gas, float vibration, string state)
    {
        // 1. Lumina de status
        if (statusLight != null)
        {
            statusLight.color = state == "CRITIC" ? Color.red
                              : state == "WARN" ? Color.yellow
                              : Color.green;
            statusLight.intensity = state == "CRITIC" ? 3f : 1.5f;
        }

        // 2. Ventilator — rotatie mai rapida la temp ridicata
        if (fanObject != null)
        {
            float speed = temp > 28 ? Mathf.Lerp(100f, 500f, (temp - 28f) / 10f) : 0f;
            fanObject.transform.Rotate(Vector3.forward, speed * Time.deltaTime);
        }

        // 3. Efect fum la gaz detectat
        if (smokeEffect != null)
            smokeEffect.SetActive(gas > 2800);

        // 4. Vibratie rack server
        if (serverRack != null && vibration > 0.8f)
        {
            serverRack.transform.localPosition += new Vector3(
                Random.Range(-0.005f, 0.005f), 0f,
                Random.Range(-0.005f, 0.005f)
            );
        }

        Debug.Log($"[Scene] Temp={temp} Hum={humidity} Gas={gas} Vib={vibration} State={state}");
    }

    void OnDestroy()
    {
        if (liveRef != null) liveRef.ValueChanged -= OnLiveData;
        if (whatifRef != null) whatifRef.ValueChanged -= OnWhatIfData;
    }
}