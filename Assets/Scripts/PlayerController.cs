using UnityEngine;
using UnityEngine.InputSystem;

public class Playercontroller : MonoBehaviour
{
    // Movement fields
    private float horizontalMovement;
    private float verticalMovement;
    private float force;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //horizontalMovement = 0.707f;
        //verticalMovement = 0.707f;
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
    // Gets the user key input and uses it to assign movement directions
    private void SetMoveDirection(Vector2 input)
    {
        horizontalMovement = -input.x;
        verticalMovement = -input.y;
    }
    // Listens for WASD and arrow key input then calls SetMoveDirection
    public void OnMoveInput(InputAction.CallbackContext ctx)
    {
        SetMoveDirection(ctx.ReadValue<Vector2>());  
    }
}
