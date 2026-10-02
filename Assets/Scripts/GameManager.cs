using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/************************************************************
 * COMPONENT OF: Game Manager
 * REQUIRED DEPENDENCIES: TextMeshProUGUI
 * DESCRIPTION: Handles collectible tracking and win condition
 * for the game
 * AUTHOR: Your name
 * VERSION: 1.0
 ************************************************************/

public class GameManager : MonoBehaviour
{
    // UI text that shows collectibles remaining (assign in inspector)
    [SerializeField] private TextMeshProUGUI RemainingTextUI;
    [SerializeField] private AudioClip winClip;

    // Audio source for victory sound
    private AudioSource audioSource;

    // Tracks how many collectibles are left to collect
    private int numberOfCollectibles;

    // Initialize everything at start
    void Start()
    {
        // Assigns the AudioSource component to the audioSource field
        audioSource = GetComponent<AudioSource>();

        // Determines how many Collectibles are in this scene
        numberOfCollectibles =
            GameObject.FindGameObjectsWithTag("Collectible").Length;

        // Initialize the UI display
        UpdateRemainingUI();
    }

    // Called by the CollectibleController when a collectible is picked up
    // - decrements count
    public void UpdateRemaining()
    {
        numberOfCollectibles--;
        UpdateRemainingUI();
    }

    // Updates UI text - shows remaining count or "You Win" message
    private void UpdateRemainingUI()
    {
        if (RemainingTextUI != null && numberOfCollectibles > 0)
        {
            RemainingTextUI.text = "Collectibles Remaining: " +
                numberOfCollectibles;
        }
        else
        {
            StartCoroutine(EndGame());
        }
    }

    IEnumerator EndGame()
    {
        RemainingTextUI.text = "You Win";
        audioSource.clip = winClip;
        audioSource.Play();

        // Waits for the exact duration of the audio clip
        yield return new WaitForSeconds(audioSource.clip.length);

        // Reloads the currently active scene
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}