using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimpleVRGoggleDisplay : MonoBehaviour
{
    [Header("Fixation Point")]
    public string fixationSymbol = "+";
    public float fixationSize = 0.05f;
    private GameObject fixationPoint;

    [Header("VR Goggles Settings")]
    public bool enableStereoView = true;
    public float eyeSeparation = 0.064f;
    public Vector3 fixedPosition = new Vector3(0, 1.6f, 0);

    [Header("Chart Display Settings")]
    public float distanceFromPlayer = 0.6f;
    public float peripheralFontSize = 0.04f;
    public Color letterColor = Color.black;    [Header("Letter Display")]
    public float displayInterval = 6f;  // Increased from 2f to give more time for speech recognition
    public float intervalBetweenLetters = 0.5f;
    public float minSize = 0.03f;
    public float maxSize = 0.08f;
    public bool randomizeSize = true;

    [Header("Voice Recognition")]
    public bool enableVoiceRecognition = true;
    public Color feedbackColor = Color.green;
    private AndroidVoiceInput voiceInput;
    private GameObject feedbackText;
    private string lastVoiceResult = "";

    [Header("Performance Display")]
    public bool showPerformanceStats = true;
    private GameObject statsDisplay;

    private Camera leftEyeCamera;
    private Camera rightEyeCamera;
    private List<GameObject> instantiatedLetters = new List<GameObject>();
    private List<PeripheralLetterData> peripheralLetters = new List<PeripheralLetterData>();
    private int currentIndex = 0;
    private bool isDisplaying = false;

    private Quaternion baseRotation = Quaternion.identity;
    private bool gyroEnabled = false;

    // Track if we should continue listening for the current letter
    private bool shouldContinueListening = false;
    private Coroutine currentLetterCoroutine = null;

    [System.Serializable]
    public class PeripheralLetterData
    {
        public string letter;
        public float eccentricity;
        public float meridian;
        public float fontSize = 0.04f;
        public bool isVisible = true;
    }

    void Start()
    {
        SetupStereoCameras();
        SetupPeripheralChart();
        CreateFixationPoint();
        CreateVoiceFeedbackUI();
        CreatePerformanceStatsDisplay();
        DisplayChart();
        StartCoroutine(InitializeAndStartSequence());

        // Enable gyro
        gyroEnabled = SystemInfo.supportsGyroscope;
        if (gyroEnabled)
        {
            Input.gyro.enabled = true;
            baseRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Initialize voice recognition
        if (enableVoiceRecognition)
        {
            voiceInput = FindObjectOfType<AndroidVoiceInput>();
            if (voiceInput == null)
            {
                GameObject voiceObj = new GameObject("AndroidVoiceInput");
                voiceInput = voiceObj.AddComponent<AndroidVoiceInput>();
            }
        }
    }

    void Update()
    {
        if (gyroEnabled)
        {
            Quaternion deviceRotation = Input.gyro.attitude;
            // Convert to Unity coordinate system
            Quaternion correctedRotation = baseRotation * new Quaternion(-deviceRotation.x, -deviceRotation.y, deviceRotation.z, deviceRotation.w);

            // Apply to cameras
            if (leftEyeCamera != null)
                leftEyeCamera.transform.rotation = correctedRotation;
            if (rightEyeCamera != null)
                rightEyeCamera.transform.rotation = correctedRotation;
        }        // Debug keys for testing voice recognition on device
#if !UNITY_EDITOR || UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.V))
        {
            ShowVoiceStatus();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            RestartVoiceRecognition();
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            // Test voice input with current letter
            if (voiceInput != null && instantiatedLetters.Count > 0)
            {
                string currentLetter = GetCurrentLetter();
                Debug.Log($"🧪 Testing voice input with letter: {currentLetter}");
                voiceInput.SimulateVoiceInput(currentLetter);
            }
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            // Test letter matching with various inputs
            if (voiceInput != null && instantiatedLetters.Count > 0)
            {
                string currentLetter = GetCurrentLetter();
                Debug.Log($"🧪 Testing letter matching for: {currentLetter}");
                
                // Test various inputs
                voiceInput.TestLetterMatching(currentLetter, currentLetter); // Exact
                voiceInput.TestLetterMatching(currentLetter.ToLower(), currentLetter); // Lowercase
                voiceInput.TestLetterMatching("ALPHA", "A"); // Phonetic
                voiceInput.TestLetterMatching("BEE", "B"); // Phonetic
                voiceInput.TestLetterMatching("SEE", "C"); // Phonetic
            }
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            // Test VoiceBridge connection
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing VoiceBridge connection...");
                voiceInput.TestVoiceBridge();
            }
        }        else if (Input.GetKeyDown(KeyCode.D))
        {
            // Debug entire voice pipeline
            if (voiceInput != null)
            {
                Debug.Log("🔍 Running voice pipeline debug...");
                voiceInput.DebugVoicePipeline();
            }
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            // Test speech result with current letter
            if (voiceInput != null && instantiatedLetters.Count > 0)
            {
                string currentLetter = GetCurrentLetter();
                Debug.Log($"🧪 Testing speech result with letter: {currentLetter}");
                voiceInput.TestSpeechResult(currentLetter);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            // Test speech recognizer status
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing speech recognizer status...");
                voiceInput.TestSpeechRecognizerStatus();
            }
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            // Test speech recognition capabilities
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing speech recognition capabilities...");
                voiceInput.TestCapabilities();
            }
        }
        else if (Input.GetKeyDown(KeyCode.U))
        {
            // Test ultra-aggressive recognition mode
            if (voiceInput != null)
            {
                Debug.Log("🚀 Testing ultra-aggressive recognition mode...");
                voiceInput.StartUltraAggressiveListening();
            }
        }
        else if (Input.GetKeyDown(KeyCode.I))
        {
            // Reinitialize speech recognizer (fix busy states)
            if (voiceInput != null)
            {
                Debug.Log("🔄 Reinitializing speech recognizer...");
                voiceInput.ReinitializeSpeechRecognizer();
            }
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            // Enable AudioRecord fallback mode
            if (voiceInput != null)
            {
                Debug.Log("🎤 Enabling AudioRecord fallback...");
                voiceInput.EnableAudioRecordFallback();
            }
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            // Test AudioRecord speech detection
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing AudioRecord speech detection...");
                voiceInput.TestAudioRecordSpeechDetection();
            }
        }        else if (Input.GetKeyDown(KeyCode.G))
        {
            // Test AudioRecord states sequence
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing AudioRecord states sequence...");
                voiceInput.TestAudioRecordStates();
            }
        }
        else if (Input.GetKeyDown(KeyCode.X))
        {
            // Test AudioRecord sensitivity adjustment
            if (voiceInput != null)
            {
                Debug.Log("🔧 Testing AudioRecord sensitivity adjustment...");
                voiceInput.TestAudioRecordSensitivity();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Z))
        {
            // Simulate AudioRecord detection for current letter
            if (voiceInput != null)
            {
                Debug.Log("🎯 Simulating AudioRecord detection for current letter...");
                voiceInput.SimulateAudioRecordDetection();
            }
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            // Lower AudioRecord threshold for better sensitivity
            if (voiceInput != null)
            {
                Debug.Log("🔧 Lowering AudioRecord threshold...");
                voiceInput.LowerAudioRecordThreshold();
            }
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            // Test AudioRecord detection manually
            if (voiceInput != null)
            {
                Debug.Log("🧪 Testing AudioRecord detection manually...");
                voiceInput.TestAudioRecordDetection();
            }
        }
#endif
    }


    void SetupStereoCameras()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCamera = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        if (enableStereoView)
        {
            GameObject leftEyeObj = new GameObject("Left Eye Camera");
            leftEyeCamera = leftEyeObj.AddComponent<Camera>();
            leftEyeCamera.CopyFrom(mainCamera);
            leftEyeCamera.rect = new Rect(0, 0, 0.5f, 1);
            leftEyeObj.transform.position = fixedPosition + Vector3.left * (eyeSeparation / 2);

            GameObject rightEyeObj = new GameObject("Right Eye Camera");
            rightEyeCamera = rightEyeObj.AddComponent<Camera>();
            rightEyeCamera.CopyFrom(mainCamera);
            rightEyeCamera.rect = new Rect(0.5f, 0, 0.5f, 1);
            rightEyeObj.transform.position = fixedPosition + Vector3.right * (eyeSeparation / 2);

            mainCamera.enabled = false;
        }
        else
        {
            leftEyeCamera = mainCamera;
            leftEyeCamera.transform.position = fixedPosition;
        }

        ConfigureCamera(leftEyeCamera);
        if (rightEyeCamera != null)
            ConfigureCamera(rightEyeCamera);
    }

    void ConfigureCamera(Camera cam)
    {
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 90f;
        cam.backgroundColor = Color.black;
    }

    void SetupPeripheralChart()
    {
        peripheralLetters.Clear();
        string[] chartLetters = { "E", "F", "P", "T", "O", "Z", "L", "D", "C" };
        CreatePeripheralRing(10f, 8, chartLetters);
        CreatePeripheralRing(15f, 12, chartLetters);
        CreatePeripheralRing(20f, 16, chartLetters);
    }

    void CreatePeripheralRing(float eccentricity, int letterCount, string[] letters)
    {
        float angleStep = 360f / letterCount;
        for (int i = 0; i < letterCount; i++)
        {
            float meridian = i * angleStep;
            string letter = letters[Random.Range(0, letters.Length)];

            PeripheralLetterData data = new PeripheralLetterData
            {
                letter = letter,
                eccentricity = eccentricity,
                meridian = meridian,
                fontSize = peripheralFontSize,
                isVisible = true
            };
            peripheralLetters.Add(data);
        }
    }

    void DisplayChart()
    {
        ClearExistingLetters();
        foreach (var letterData in peripheralLetters)
        {
            if (letterData.isVisible)
            {
                GameObject obj = CreatePeripheralLetterObject(letterData);
                instantiatedLetters.Add(obj);
            }
        }
    }

    GameObject CreatePeripheralLetterObject(PeripheralLetterData data)
    {
        GameObject obj = new GameObject($"Letter_{data.letter}_{data.eccentricity}deg");
        TextMeshPro textMesh = obj.AddComponent<TextMeshPro>();

        textMesh.text = data.letter;
        textMesh.fontSize = data.fontSize * 100;
        textMesh.color = letterColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;

        obj.transform.position = PolarToWorldPosition(data.eccentricity, data.meridian);
        obj.transform.LookAt(leftEyeCamera.transform);
        obj.transform.Rotate(0, 180, 0);

        if (randomizeSize)
        {
            float randomSize = Random.Range(minSize, maxSize);
            obj.transform.localScale = Vector3.one * randomSize;
        }

        return obj;
    }

    Vector3 PolarToWorldPosition(float eccentricity, float meridian)
    {
        float er = eccentricity * Mathf.Deg2Rad;
        float mr = meridian * Mathf.Deg2Rad;
        float x = distanceFromPlayer * Mathf.Sin(er) * Mathf.Cos(mr);
        float y = distanceFromPlayer * Mathf.Sin(er) * Mathf.Sin(mr);
        float z = distanceFromPlayer * Mathf.Cos(er);
        return fixedPosition + new Vector3(x, y, z);
    }

    IEnumerator InitializeAndStartSequence()
    {
        yield return new WaitForSeconds(0.5f);
        if (instantiatedLetters.Count == 0)
        {
            Debug.LogWarning("No letters found.");
            yield break;
        }
        StartCoroutine(ShowLettersOneByOne());
    }    IEnumerator ShowLettersOneByOne()
    {
        isDisplaying = true;
        
        // Initial notification that letters are ready (for permission setup)
        NotifyVoiceInputReady();
        
        while (isDisplaying)
        {
            // Hide all letters first
            foreach (var letter in instantiatedLetters)
                letter.SetActive(false);

            // Stop voice listening before changing letters
            shouldContinueListening = false;
            if (voiceInput != null)
            {
                voiceInput.StopListeningForLetter();
                UpdateVoiceListeningStatus(false);
            }            if (currentIndex < instantiatedLetters.Count)
            {
                // Show the new letter
                instantiatedLetters[currentIndex].SetActive(true);
                string currentLetter = GetCurrentLetter();
                Debug.Log($"🔠 Showing letter: {currentLetter}");
                
                // Wait a bit longer for letter to be visible, then start voice listening
                yield return new WaitForSeconds(0.5f);
                
                // Start voice listening for this specific letter
                shouldContinueListening = true;
                if (voiceInput != null)
                {
                    Debug.Log($"⏰ Starting voice listening for letter '{currentLetter}' - will listen for {displayInterval - 0.5f} seconds");
                    voiceInput.StartListeningForLetter();
                    UpdateVoiceListeningStatus(true);
                }
                
                // Listen for the main display duration (now 5.5 seconds instead of 1.8)
                float listeningDuration = displayInterval - 0.5f;
                Debug.Log($"⏱️ Listening for {listeningDuration} seconds...");
                yield return new WaitForSeconds(listeningDuration);
                
                // Stop listening before letter change
                shouldContinueListening = false;
                if (voiceInput != null)
                {
                    Debug.Log($"⏰ Stopping voice listening for letter '{currentLetter}' after {listeningDuration} seconds");
                    voiceInput.StopListeningForLetter();
                    UpdateVoiceListeningStatus(false);
                }
            }
            else
            {
                // No letter to show, just wait
                yield return new WaitForSeconds(displayInterval);
            }

            // Move to next letter and wait between letters
            currentIndex = (currentIndex + 1) % instantiatedLetters.Count;
            yield return new WaitForSeconds(intervalBetweenLetters);
        }
        
        // Stop listening when sequence ends
        shouldContinueListening = false;
        if (voiceInput != null)
        {
            voiceInput.StopListeningForLetter();
            UpdateVoiceListeningStatus(false);
        }
    }

    void ClearExistingLetters()
    {
        foreach (GameObject letter in instantiatedLetters)
            if (letter != null)
                Destroy(letter);
        instantiatedLetters.Clear();
    }

    public string GetCurrentLetter()
    {
        if (currentIndex < instantiatedLetters.Count)
        {
            string[] parts = instantiatedLetters[currentIndex].name.Split('_');
            if (parts.Length >= 2) return parts[1];
        }
        return "";
    }
    void CreateFixationPoint()
    {
        fixationPoint = new GameObject("FixationPoint");
        TextMeshPro textMesh = fixationPoint.AddComponent<TextMeshPro>();

        textMesh.text = fixationSymbol;
        textMesh.fontSize = fixationSize * 100;
        textMesh.color = letterColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.fontSize = 0.5f; // Scale down for better fit

        fixationPoint.transform.position = fixedPosition + new Vector3(0, 0, distanceFromPlayer);
        fixationPoint.transform.LookAt(leftEyeCamera.transform);
        fixationPoint.transform.Rotate(0, 180, 0);
    }

    void CreateVoiceFeedbackUI()
    {
        feedbackText = new GameObject("VoiceFeedback");
        TextMeshPro textMesh = feedbackText.AddComponent<TextMeshPro>();

        textMesh.text = ""; // Remove voice feedback text as requested
        textMesh.fontSize = 0.03f * 100;
        textMesh.color = feedbackColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.fontSize = 0.5f;

        // Position at the bottom of the view
        feedbackText.transform.position = fixedPosition + new Vector3(0, -0.3f, distanceFromPlayer);
        feedbackText.transform.LookAt(leftEyeCamera.transform);
        feedbackText.transform.Rotate(0, 180, 0);
    }

    void CreatePerformanceStatsDisplay()
    {
        if (!showPerformanceStats) return;

        statsDisplay = new GameObject("PerformanceStats");
        TextMeshPro textMesh = statsDisplay.AddComponent<TextMeshPro>();

        textMesh.text = "Accuracy: 0%";
        textMesh.fontSize = 0.025f * 100;
        textMesh.color = Color.cyan;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.fontSize = 0.5f;// Scale down for better fit

        // Position at the top of the view
        statsDisplay.transform.position = fixedPosition + new Vector3(0, 0.4f, distanceFromPlayer);
        statsDisplay.transform.LookAt(leftEyeCamera.transform);
        statsDisplay.transform.Rotate(0, 180, 0);
    }

    public void UpdatePerformanceStats(float accuracy, int correct, int total)
    {
        if (statsDisplay != null && showPerformanceStats)
        {
            TextMeshPro textMesh = statsDisplay.GetComponent<TextMeshPro>();
            textMesh.text = $"Accuracy: {accuracy:F1}% ({correct}/{total})";
        }
    }    public void UpdateVoiceFeedback(string message, bool isCorrect = false)
    {
        // Voice feedback UI disabled as requested - only keep debug logs
        Debug.Log($"Voice feedback: {message}");
    }

    void ClearVoiceFeedback()
    {
        if (feedbackText != null)
        {
            TextMeshPro textMesh = feedbackText.GetComponent<TextMeshPro>();
            textMesh.text = "🎤 Listening...";
            textMesh.color = feedbackColor;
        }
    }

    public void OnVoiceCommandRecognized(string command)
    {
        lastVoiceResult = command;
        Debug.Log($"🎤 Voice command recognized: {command}");

        // Show feedback
        feedbackText.SetActive(true);
        feedbackText.GetComponent<TextMeshPro>().text = command;

        // Hide after 2 seconds
        StartCoroutine(HideVoiceFeedback());
    }

    IEnumerator HideVoiceFeedback()
    {
        yield return new WaitForSeconds(2f);
        feedbackText.SetActive(false);
    }

    public bool IsDisplayingLetters()
    {
        return isDisplaying && instantiatedLetters.Count > 0;
    }

    // Method to notify voice input when letters start displaying
    public void NotifyVoiceInputReady()
    {
        if (voiceInput != null)
        {
            Debug.Log("📢 Notifying voice input that letters are ready");
            voiceInput.OnLettersReady();
        }
    }    // Method to show voice recognition debug status
    private void ShowVoiceStatus()
    {
        if (voiceInput != null)
        {
            string status = voiceInput.GetVoiceRecognitionStatus();
            Debug.Log($"🔍 Voice Status: {status}");
            UpdateVoiceFeedback($"Status: {status}", true);
            
            // Show AudioRecord fallback information
            bool audioRecordEnabled = voiceInput.useAudioRecordFallback;
            string fallbackInfo = audioRecordEnabled ? "AudioRecord fallback: ENABLED" : "AudioRecord fallback: DISABLED - Enable in Inspector if speech recognition fails";
            Debug.Log($"💡 {fallbackInfo}");
            UpdateVoiceFeedback($"💡 {fallbackInfo}", true);
        }
        else
        {
            Debug.Log("❌ Voice input component not found");
            UpdateVoiceFeedback("❌ Voice input not found", false);
        }
    }

    // Method to manually restart voice recognition
    public void RestartVoiceRecognition()
    {
        if (voiceInput != null)
        {
            UpdateVoiceFeedback("🔄 Restarting voice recognition...", false);
            voiceInput.RestartVoiceRecognitionSystem();
        }
    }

    // Method to check if voice input should continue listening
    public bool ShouldContinueListening()
    {
        return shouldContinueListening && isDisplaying;
    }

    // Method to update voice listening status in UI
    private void UpdateVoiceListeningStatus(bool isListening)
    {
        if (feedbackText != null)
        {
            TextMeshPro textMesh = feedbackText.GetComponent<TextMeshPro>();
            if (isListening)
            {
                textMesh.text = "🎤 Listening...";
                textMesh.color = Color.green;
                feedbackText.SetActive(true);
            }
            else
            {
                textMesh.text = "⏸️ Voice paused";
                textMesh.color = Color.yellow;
                feedbackText.SetActive(true);
                
                // Auto-hide after 1 second
                CancelInvoke("ClearVoiceFeedback");
                Invoke("ClearVoiceFeedback", 1f);
            }
        }
    }
}
