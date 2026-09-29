using UnityEngine;
//

public class CollectibleController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectParticlePrefab;
    void Start()
    {

    }
    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
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
