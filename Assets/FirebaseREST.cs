using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseREST : MonoBehaviour
{
    [Header("Firebase")]
    
    public string firebaseUrl = "https://server-room-digital-twin-default-rtdb.europe-west1.firebasedatabase.app/serverroom/live.json";

    [Header("Poll Interval (secunde)")]
    public float pollInterval = 2f;

    ServerRoomManager manager;

    void Start()
    {
        manager = GetComponent<ServerRoomManager>();
        StartCoroutine(PollFirebase());
    }

    IEnumerator PollFirebase()
    {
        while (true)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(firebaseUrl))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    manager.ReceiveSensorData(req.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning("[Firebase] Eroare: " + req.error);
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }
}
