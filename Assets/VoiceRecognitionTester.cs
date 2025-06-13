using UnityEngine;

public class VoiceRecognitionTester : MonoBehaviour
{
    [Header("Testing Controls")]
    public KeyCode testKey = KeyCode.Space;
    public string[] testLetters = { "E", "F", "P", "T", "O", "Z", "L", "D", "C" };
    
    private AndroidVoiceInput voiceInput;
    private int currentTestIndex = 0;
    
    void Start()
    {
        voiceInput = FindObjectOfType<AndroidVoiceInput>();
        if (voiceInput == null)
        {
            Debug.LogWarning("VoiceRecognitionTester: AndroidVoiceInput not found!");
        }
    }
    
    void Update()
    {
        // Test voice recognition with keyboard input (Editor only)
        if (Input.GetKeyDown(testKey) && voiceInput != null)
        {
#if UNITY_EDITOR
            string testLetter = testLetters[currentTestIndex % testLetters.Length];
            Debug.Log($"🧪 Testing voice input with letter: {testLetter}");
            voiceInput.SimulateVoiceInput(testLetter);
            currentTestIndex++;
#endif
        }
        
        // Manual restart for testing
        if (Input.GetKeyDown(KeyCode.R) && voiceInput != null)
        {
            Debug.Log("🔄 Manual restart requested");
            voiceInput.ForceRestartVoiceRecognition();
        }
    }
    
    void OnGUI()
    {
#if UNITY_EDITOR
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("Voice Recognition Tester");
        GUILayout.Label($"Press {testKey} to simulate voice input");
        GUILayout.Label("Press R to restart voice recognition");
        
        if (voiceInput != null)
        {
            GUILayout.Label($"Accuracy: {voiceInput.GetAccuracy():F1}%");
            GUILayout.Label($"Correct: {voiceInput.GetCorrectAnswers()}");
            GUILayout.Label($"Total: {voiceInput.GetTotalAttempts()}");
        }
        
        GUILayout.EndArea();
#endif
    }
}
