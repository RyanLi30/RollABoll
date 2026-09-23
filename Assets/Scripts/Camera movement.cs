using UnityEngine;
//attached to the main camera
//control the camera
//Ryan Li
//Sep23rd,2026
//version6
public class Cameramovement : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Vector3 offset;
    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = playerTransform.position + offset;
    }
}
