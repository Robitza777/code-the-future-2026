using UnityEngine;

public class FanRotate : MonoBehaviour
{
    public float speed = 500f;
    public bool isOn = false;

    void Update()
    {
        if (isOn)
        {
            transform.Rotate(0, speed * Time.deltaTime, 0);
        }
    }
}