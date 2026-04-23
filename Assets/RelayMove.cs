using UnityEngine;

public class RelayMove : MonoBehaviour
{
    public GameObject fan;

    Vector3 startPos;
    Vector3 targetPos;

    public float distance = 0.15f;
    public float speed = 5f;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (fan.activeSelf)
        {
            targetPos = startPos + new Vector3(0, 0, distance);
        }
        else
        {
            targetPos = startPos + new Vector3(0, 0, -distance);
        }

        transform.localPosition =
            Vector3.Lerp(transform.localPosition, targetPos, speed * Time.deltaTime);
    }
}