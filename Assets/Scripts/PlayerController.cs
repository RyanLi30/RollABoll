using UnityEngine;

public class Playercontroller : MonoBehaviour
{
    // Movement fields
    private float horizontalMovement;
    private float verticalMovement;
    private float force;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        horizontalMovement = 0.707f;
        verticalMovement = 0.707f;
        force = 4.75f;

    }

    // Update is called once per frame
    void Update()
    {
       MovePlayer();//GetComponent<Rigidbody>().AddForce(Vector3.forward * 10); 
    }
    // Moves the player
    private void MovePlayer()
    {
        Vector3 direction = new Vector3(horizontalMovement, 0, verticalMovement);
        GetComponent<Rigidbody>().AddForce(direction * force);
    }
}
