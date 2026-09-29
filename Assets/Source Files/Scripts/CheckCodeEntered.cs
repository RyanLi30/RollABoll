using UnityEngine;
using TMPro; 
using System.Collections;

public class CheckCodeEntered : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_InputField codeInputField; 

    [Header("Effects & Audio")]
    public ParticleSystem correctEffect; 
    public AudioSource correctSound; 
    public AudioSource incorrectSound; 

    // --- State Variables ---
    private Coroutine clearCoroutine;
    private bool isUpdatingText = false; // <-- The missing variable!

    void Start()
    {
        if (codeInputField != null)
        {
            Debug.Log("Input Field successfully linked!");
            codeInputField.onValueChanged.AddListener(OnInputFieldChanged);
            
            // Force focus for New Input System on launch
            codeInputField.Select();
            codeInputField.ActivateInputField();
        }
    }

    private void OnInputFieldChanged(string text)
    {
        // If the script itself is changing the text, ignore the event to prevent loops
        if (isUpdatingText) return;

        if (text.Length == 4)
        {
            ValidateCode();
            
            // Safety check: Only start coroutine if the object is fully active/enabled
            if (isActiveAndEnabled)
            {
                if (clearCoroutine != null) StopCoroutine(clearCoroutine);
                clearCoroutine = StartCoroutine(ClearInputFieldAfterDelay(0.5f));
            }
            else
            {
                // Fallback if Unity is flickering the object's active state
                ClearInputFieldInstantly();
            }
        }
        else if (text.Length > 4)
        {
            // Truncate safely using the boolean flag
            isUpdatingText = true;
            codeInputField.text = text.Substring(0, 4);
            isUpdatingText = false;
        }
    }

    private IEnumerator ClearInputFieldAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        isUpdatingText = true;
        codeInputField.text = "";
        isUpdatingText = false;

        // Force New Input System UI to re-focus on the field
        codeInputField.ActivateInputField(); 
        codeInputField.Select(); 
    }

    private void ClearInputFieldInstantly()
    {
        isUpdatingText = true;
        codeInputField.text = "";
        isUpdatingText = false;
        
        codeInputField.ActivateInputField();
        codeInputField.Select();
    }

    public void ValidateCode()
    {
        if (codeInputField == null) return;

        string code = codeInputField.text;
        if (code == "2004")
        {
            correctEffect.Play();
            correctSound.Play();
        }
        else
        {
            incorrectSound.Play();
            StartCoroutine(ShakeInputField(0.5f, 0.1f));
        }
    }

    private IEnumerator ShakeInputField(float duration, float magnitude)
    {
        Vector3 originalPosition = codeInputField.transform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = originalPosition.x + Random.Range(-1f, 1f) * (magnitude * 100f);
            codeInputField.transform.localPosition = new Vector3(x, originalPosition.y, originalPosition.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        codeInputField.transform.localPosition = originalPosition;
    }
}