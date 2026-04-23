using UnityEngine;

public class ServerRoomManager : MonoBehaviour
{
    public float temperature = 25f;

    public GameObject Ledverde;
    public GameObject Ledgalben;
    public GameObject Ledrosu;

    public GameObject Fan;

    void Update()
    {
        CheckTemperature();

        if (Input.GetKeyDown(KeyCode.T))
        {
            temperature += 5f;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            temperature = 25f;
        }
    }

    void CheckTemperature()
    {
        Ledverde.SetActive(false);
        Ledgalben.SetActive(false);
        Ledrosu.SetActive(false);

        Fan.SetActive(false);

        if (temperature < 30)
        {
            Ledverde.SetActive(true);
        }
        else if (temperature < 40)
        {
            Ledgalben.SetActive(true);
            Fan.SetActive(true);
        }
        else
        {
            Ledrosu.SetActive(true);
            Fan.SetActive(true);
        }
    }
}