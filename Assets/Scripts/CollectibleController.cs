using UnityEngine;

public class CollectibleController : MonoBehaviour
{
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectParticlePrefab;
    private GameManager gameManager;
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
    }

    private void Update()
    {

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