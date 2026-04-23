using UnityEngine;

public class AlertSystem : MonoBehaviour
{
    public GameObject greenLed;
    public GameObject yellowLed;
    public GameObject redLed;

    public AudioSource buzzer;

    public float temperatura;
    public float umiditate;
    public float vibratii;
    public float gaz;

    void Update()
    {
        bool warning = false;
        bool critical = false;

        // WARNING
        if (temperatura > 28f || umiditate > 70f)
            warning = true;

        // CRITICAL
        if (temperatura > 32f || vibratii > 0.8f || gaz > 2700f)
            critical = true;

        if (critical)
        {
            greenLed.SetActive(false);
            yellowLed.SetActive(false);
            redLed.SetActive(true);

            if (!buzzer.isPlaying)
                buzzer.Play();
        }
        else if (warning)
        {
            greenLed.SetActive(false);
            yellowLed.SetActive(true);
            redLed.SetActive(false);

            buzzer.Stop();
        }
        else
        {
            greenLed.SetActive(true);
            yellowLed.SetActive(false);
            redLed.SetActive(false);

            buzzer.Stop();
        }
    }
}