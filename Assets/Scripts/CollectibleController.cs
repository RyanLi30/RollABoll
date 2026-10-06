using UnityEngine;

/*****************************************************************
 * COMPONENT OF: Collectible
 * REQUIRED DEPENDENCIES: GameManager, AudioClip, GameObject
 * DESCRIPTION: Handles collectible pickup, sound, particle effects, updates the Game Manager, and rotates the collectible continuously.
 * AUTHOR: Ryan Li
 * VERSION 1.0: Initial Version
 * VERSION 1.1: Add a behavior so that the Collectible rotates slowly about the y-axis.
 *****************************************************************/
public class CollectibleController : MonoBehaviour
{
    // Speed at which the collectible rotates around the Y-axis in degrees per second.
    [SerializeField] private float rotationSpeed = 50f;

    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectParticlePrefab;

    private GameManager gameManager;

    private void Start()
    {
        // Search the scene for the GameManager instance.
        gameManager = FindAnyObjectByType<GameManager>();
    }

    private void Update()
    {
        // Delegate frame update responsibilities to specific helper methods.
        RotateCollectible();
    }

    /// <summary>
    /// Rotates the object around its Y-axis at a constant rate independent of frame rate.
    /// </summary>
    private void RotateCollectible()
    {
        // Rotate around the Y-axis (Vector3.up) based on rotationSpeed and delta time.
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            gameManager.UpdateRemaining();
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

            Instantiate(
                collectParticlePrefab,
                transform.position,
                Quaternion.identity
            );

            Destroy(gameObject);
        }
    }
}