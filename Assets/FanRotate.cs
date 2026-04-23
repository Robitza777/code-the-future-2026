using UnityEngine;

public class FanRotate : MonoBehaviour
{
    public float speed = 1500f;

    void Update()
    {
        transform.Rotate(0, speed * Time.deltaTime, 0);
    }
}