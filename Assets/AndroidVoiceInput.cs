using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public class AndroidVoiceInput : MonoBehaviour
{
    [Header("Voice Recognition Settings")]
    public float recognitionTimeout = 3f;
    public float confidenceThreshold = 0.5f;
    public bool logAllResults = true;
    
    private bool isListening = false;
    private string filePath;
    private AndroidJavaObject speechRecognizer;
    private AndroidJavaObject unityActivity;
    private AndroidJavaObject recognitionListener;
    private SimpleVRGoggleDisplay vrDisplay;
    private bool isInitialized = false;
    private bool permissionsGranted = false;
    private bool waitingForLetterDisplay = false;
    
    // Voice recognition results
    private string lastRecognizedText = "";
    private bool hasNewResult = false;
    
    // Performance tracking
    private int correctAnswers = 0;
    private int totalAttempts = 0;

    void Start()
    {
        // Create file path with current date
        string fileName = "VoiceRecognition_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        Debug.Log("Voice recognition file: " + filePath);
        
        // Find the VR display component
        vrDisplay = FindObjectOfType<SimpleVRGoggleDisplay>();        if (vrDisplay == null)
        {
            Debug.LogError("SimpleVRGoggleDisplay not found! Voice recognition needs this component.");
            return;
        }
        
        // Request microphone permissions first, then initialize
        RequestMicrophonePermission();
    }

    void Update()
    {
        // Process voice recognition results
        if (hasNewResult && !string.IsNullOrEmpty(lastRecognizedText))
        {
            ProcessVoiceResult(lastRecognizedText);
            hasNewResult = false;
            lastRecognizedText = "";
        }
    }    private void InitializeAndroidSpeechRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("🔧 Starting Android speech recognition initialization...");
            
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            if (unityActivity == null)
            {
                Debug.LogError("❌ Unity activity is null!");
                return;
            }
            
            Debug.Log("✅ Unity activity obtained successfully");
            
            // Check if speech recognition is available
            AndroidJavaClass speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            bool isAvailable = speechRecognizerClass.CallStatic<bool>("isRecognitionAvailable", unityActivity);
            
            Debug.Log($"🎙️ Speech recognition availability check: {isAvailable}");
            
            if (isAvailable)
            {
                Debug.Log("🎙️ Speech recognition is available, creating VoiceBridge...");
                
                // Create our VoiceBridge plugin
                speechRecognizer = new AndroidJavaObject("com.unity3d.player.VoiceBridge", unityActivity, gameObject.name);
                
                if (speechRecognizer != null)
                {
                    isInitialized = true;
                    Debug.Log("✅ Voice recognition initialized successfully with VoiceBridge");
                    
                    if (vrDisplay != null)
                    {
                        vrDisplay.UpdateVoiceFeedback("🎙️ Voice recognition ready", true);
                    }
                }
                else
                {
                    Debug.LogError("❌ Failed to create VoiceBridge object");
                }
            }
            else
            {
                Debug.LogError("❌ Speech recognition not available on this device");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔇 Speech recognition not supported on this device", false);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to initialize Android Speech Recognition: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback($"🔇 Voice initialization error: {e.Message}", false);
            }
        }
#else
        // In editor, mark as initialized
        isInitialized = true;
        Debug.Log("🖥️ Voice recognition initialized (Editor mode)");
#endif
    }

    private void CreateRecognitionListener()
    {
        // This method is no longer needed as we're using the Java plugin
    }

    public void StartContinuousListening()
    {
        if (!isInitialized || isListening) return;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        StartListening();
#else
        Debug.Log("Speech recognition only works on Android device");
#endif
    }    public void StartListening()
    {
        Debug.Log($"🎯 StartListening called - isListening: {isListening}, isInitialized: {isInitialized}, speechRecognizer null: {speechRecognizer == null}, permissionsGranted: {permissionsGranted}");
        
        if (!isListening && isInitialized && speechRecognizer != null && permissionsGranted)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Debug.Log("🎤 Attempting to start voice recognition...");
                speechRecognizer.Call("startListening");
                isListening = true;
                
                Debug.Log("✅ Voice recognition start command sent successfully");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🎤 Listening for speech...", true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to start speech recognition: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
                isListening = false;
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback($"🔇 Voice recognition error: {e.Message}", false);
                }
                
                // Retry after delay
                Debug.Log("🔄 Scheduling retry in 2 seconds...");
                Invoke("StartListening", 2f);
            }
#else
            Debug.Log("🖥️ Voice recognition simulated (Editor mode)");
            isListening = true;
            
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🎤 Voice recognition simulated", true);
            }
#endif
        }
        else
        {
            Debug.LogWarning($"⚠️ Cannot start listening - isListening: {isListening}, isInitialized: {isInitialized}, speechRecognizer: {speechRecognizer != null}, permissionsGranted: {permissionsGranted}");
            
            // Try to diagnose the issue
            if (!permissionsGranted)
            {
                Debug.LogWarning("❗ Microphone permission not granted");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔒 Microphone permission required", false);
                }
            }
            else if (!isInitialized)
            {
                Debug.LogWarning("❗ Voice recognition not initialized");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔧 Voice recognition not initialized", false);
                }
            }
            else if (speechRecognizer == null)
            {
                Debug.LogWarning("❗ Speech recognizer object is null");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔇 Speech recognizer error", false);
                }
            }
        }
    }    // This method will be called from Android native code or Unity messaging
    public void OnSpeechResult(string result)
    {
        if (!string.IsNullOrEmpty(result))
        {
            lastRecognizedText = result;
            hasNewResult = true;
            
            if (logAllResults)
            {
                SaveToFile(result);
                Debug.Log("Voice recognized: " + result);
            }
        }

        // Don't restart immediately - let the VR display control timing
        isListening = false;
        
        // Only restart if we're still supposed to be listening for this letter
        if (vrDisplay != null && vrDisplay.IsDisplayingLetters() && vrDisplay.ShouldContinueListening())
        {
            Invoke("StartListening", 0.5f); // Short delay before restarting
        }
    }
    
    // Alternative method that handles multiple results
    public void OnSpeechResults(string results)
    {
        if (!string.IsNullOrEmpty(results))
        {
            // Parse multiple results if available
            string[] resultArray = results.Split(',');            if (resultArray.Length > 0)
            {
                OnSpeechResult(resultArray[0].Trim()); // Use the first (most confident) result
            }
        }
    }

    public void OnSpeechError(string error)
    {
        Debug.LogWarning("Speech recognition error: " + error);
        
        // Update UI with error feedback
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback($"🔇 {error}", false);
        }
        
        isListening = false;
        
        // Restart after error, but with different delays based on error type
        float delay = 1f;
        if (error.Contains("No match") || error.Contains("No speech input"))
        {
            delay = 0.5f; // Quick restart for common errors
        }        else if (error.Contains("Network"))
        {
            delay = 3f; // Longer delay for network issues
        }
        
        Invoke("StartListening", delay);
    }

    private void ProcessVoiceResult(string recognizedText)
    {
        if (vrDisplay == null) return;
        
        string currentLetter = vrDisplay.GetCurrentLetter();
        if (string.IsNullOrEmpty(currentLetter)) return;
        
        // Clean and normalize the recognized text
        string cleanedText = recognizedText.Trim().ToUpper();
        string targetLetter = currentLetter.ToUpper();
        
        totalAttempts++;
        
        bool isCorrect = false;
        
        // Check if the recognized text contains the target letter
        if (cleanedText.Contains(targetLetter))
        {
            isCorrect = true;
            correctAnswers++;
        }
        // Also check for phonetic matches (optional)
        else if (IsPhoneticMatch(cleanedText, targetLetter))
        {
            isCorrect = true;
            correctAnswers++;
        }
          // Log the result
        string resultText = $"Target: {targetLetter}, Heard: '{cleanedText}', Correct: {isCorrect}";
        Debug.Log($"🎯 {resultText}");
        
        // Update UI feedback
        string feedbackMessage = isCorrect ? 
            $"✓ Correct! Said '{cleanedText}' for '{targetLetter}'" : 
            $"✗ Wrong: Said '{cleanedText}' for '{targetLetter}'";
        vrDisplay.UpdateVoiceFeedback(feedbackMessage, isCorrect);
        
        // Calculate accuracy
        float accuracy = totalAttempts > 0 ? (float)correctAnswers / totalAttempts * 100f : 0f;
        
        // Update performance stats display
        vrDisplay.UpdatePerformanceStats(accuracy, correctAnswers, totalAttempts);
        
        // Save detailed result to file
        SaveDetailedResult(targetLetter, cleanedText, isCorrect);
        
        Debug.Log($"📊 Accuracy: {correctAnswers}/{totalAttempts} ({accuracy:F1}%)");
    }
    
    private bool IsPhoneticMatch(string recognized, string target)
    {
        // Handle common phonetic variations for letters
        Dictionary<string, string[]> phoneticVariations = new Dictionary<string, string[]>
        {
            {"A", new[] {"AY", "EH"}},
            {"B", new[] {"BE", "BEE"}},
            {"C", new[] {"SEE", "SI"}},
            {"D", new[] {"DEE", "DI"}},
            {"E", new[] {"EE", "EH"}},
            {"F", new[] {"EF", "EFF"}},
            {"G", new[] {"JEE", "GEE"}},
            {"H", new[] {"AYCH", "HAYCH"}},
            {"I", new[] {"EYE", "AY"}},
            {"J", new[] {"JAY", "JEY"}},
            {"K", new[] {"KAY", "KEH"}},
            {"L", new[] {"EL", "ELL"}},
            {"M", new[] {"EM", "EMM"}},
            {"N", new[] {"EN", "ENN"}},
            {"O", new[] {"OH", "OW"}},
            {"P", new[] {"PEE", "PI"}},
            {"Q", new[] {"QUEUE", "CUE"}},
            {"R", new[] {"AR", "ARR"}},
            {"S", new[] {"ESS", "ES"}},
            {"T", new[] {"TEE", "TI"}},
            {"U", new[] {"YOU", "YOO"}},
            {"V", new[] {"VEE", "VI"}},
            {"W", new[] {"DOUBLE-U", "DOUBLE U"}},
            {"X", new[] {"EX", "EKS"}},
            {"Y", new[] {"WHY", "WYE"}},
            {"Z", new[] {"ZED", "ZEE"}}
        };
        
        if (phoneticVariations.ContainsKey(target))
        {
            foreach (string variation in phoneticVariations[target])
            {
                if (recognized.Contains(variation))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private void SaveToFile(string recognizedText)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] {recognizedText}\n";

            File.AppendAllText(filePath, entry);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save to file: " + e.Message);
        }
    }
    
    private void SaveDetailedResult(string targetLetter, string recognizedText, bool isCorrect)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] Target: {targetLetter} | Recognized: '{recognizedText}' | Correct: {isCorrect} | Accuracy: {correctAnswers}/{totalAttempts}\n";

            File.AppendAllText(filePath, entry);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save detailed result: " + e.Message);
        }
    }

    public void StopListening()
    {
        isListening = false;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("stopListening");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error stopping speech recognizer: " + e.Message);
            }
        }
#endif
        Debug.Log("Stopped listening.");
    }
    
    public void RestartListening()
    {
        StopListening();
        Invoke("StartListening", 0.5f);
    }
    
    // Public methods to get performance data
    public float GetAccuracy()
    {
        return totalAttempts > 0 ? (float)correctAnswers / totalAttempts * 100f : 0f;
    }
    
    public int GetCorrectAnswers()
    {
        return correctAnswers;
    }
    
    public int GetTotalAttempts()
    {
        return totalAttempts;
    }
    
    public void ResetStats()
    {
        correctAnswers = 0;
        totalAttempts = 0;
        Debug.Log("Voice recognition stats reset.");
    }

    private void OnDestroy()
    {
        StopListening();
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("destroy");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error destroying speech recognizer: " + e.Message);
            }
        }
#endif
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopListening();
        }
        else if (isInitialized)
        {
            Invoke("StartListening", 1f);
        }
    }    private void RequestMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // Use Unity's built-in permission system
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                Debug.Log("🎤 Microphone permission already granted");
                OnPermissionGranted();
            }
            else
            {
                Debug.Log("🎤 Requesting microphone permission...");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔒 Requesting microphone permission...", false);
                }
                
                // Request permission using Unity's system
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                
                // Start checking for permission result with a more robust approach
                StartCoroutine(CheckPermissionResultRobust());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to request microphone permission: " + e.Message);
            // Try to initialize anyway (older Android versions)
            OnPermissionGranted();
        }
#else
        // In editor, assume permission granted
        OnPermissionGranted();
#endif
    }

    private System.Collections.IEnumerator CheckPermissionResultRobust()
    {
        float timeout = 30f; // 30 second timeout
        float elapsed = 0f;
        
        Debug.Log("⏳ Starting permission check loop...");
        
        while (elapsed < timeout && !permissionsGranted)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            bool hasPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
            Debug.Log($"🔍 Permission check {elapsed:F1}s: {hasPermission}");
            
            if (hasPermission)
            {
                Debug.Log("✅ Microphone permission granted!");
                OnPermissionGranted();
                yield break;
            }
#endif
        }
        
        if (!permissionsGranted)
        {
            Debug.LogError("❌ Microphone permission not granted or timed out");
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🔇 Microphone permission required. Please restart app and grant permission.", false);
            }
        }
    }private System.Collections.IEnumerator CheckPermissionResult()
    {
        float timeout = 30f; // 30 second timeout
        float elapsed = 0f;
        
        while (elapsed < timeout && !permissionsGranted)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                Debug.Log("✅ Microphone permission granted!");
                OnPermissionGranted();
                yield break;
            }
#endif
        }
        
        if (!permissionsGranted)
        {
            Debug.LogError("❌ Microphone permission not granted or timed out");
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🔇 Microphone permission required. Please restart app and grant permission.", false);
            }
        }
    }private void OnPermissionGranted()
    {
        permissionsGranted = true;
        Debug.Log("🎤 Microphone permission granted, initializing voice recognition...");
        
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎤 Microphone access granted", true);
        }
        
        // Initialize Android Speech Recognition
        InitializeAndroidSpeechRecognition();
        
        // Wait a moment for initialization to complete, then check if we should start
        Invoke("CheckIfShouldStartListening", 1f);
    }    private void CheckIfShouldStartListening()
    {
        Debug.Log($"🔍 Voice recognition initialized and ready for synchronized listening - isInitialized: {isInitialized}, permissionsGranted: {permissionsGranted}");
        
        // Don't auto-start listening - wait for synchronized StartListeningForLetter() calls
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎙️ Voice ready - waiting for letter sync", true);
        }
        
        // If letters are already displaying, let the VR display know we're ready
        if (vrDisplay != null && vrDisplay.IsDisplayingLetters())
        {
            Debug.Log("🔤 Letters already displayed - voice system ready for sync");
        }
        else
        {
            Debug.Log("⏳ Voice system ready - waiting for letters to start");
            waitingForLetterDisplay = false; // We don't need to wait anymore, just be ready
        }
    }

    private System.Collections.IEnumerator WaitForLetterDisplay()
    {
        Debug.Log("⏳ Waiting for letters to be displayed...");
        
        while (waitingForLetterDisplay && vrDisplay != null)
        {
            if (vrDisplay.IsDisplayingLetters())
            {
                Debug.Log("🔤 Letters are now displayed, starting voice recognition...");
                waitingForLetterDisplay = false;
                StartListeningWhenReady();
                yield break;
            }
            
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void StartListeningWhenReady()
    {
        if (isInitialized && permissionsGranted)
        {
            Debug.Log("🎤 Starting voice recognition for letter detection...");
            StartContinuousListening();
            
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🎤 Voice recognition active", true);
            }
        }
    }    public void OnLettersReady()
    {
        Debug.Log("📧 Received notification that letters are ready for synchronized listening");
        waitingForLetterDisplay = false;
        
        // Don't start listening automatically - wait for StartListeningForLetter() calls
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎙️ Voice recognition ready for synchronized listening", true);
        }
    }

    public void OnSpeechListeningStarted(string message)
    {
        Debug.Log("🎤 " + message);
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎤 Listening for letters...", true);
        }
    }

    public void OnSpeechInitialized(string message)
    {
        Debug.Log("🎙️ " + message);
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎙️ Voice recognition initialized", true);
        }
    }

    // Test method for editor simulation
    [System.Obsolete("For testing only")]
    public void SimulateVoiceInput(string testLetter)
    {
#if UNITY_EDITOR
        Debug.Log($"🧪 Simulating voice input: {testLetter}");
        OnSpeechResult(testLetter);
#endif
    }

    // Public method to force restart voice recognition
    public void ForceRestartVoiceRecognition()
    {
        Debug.Log("🔄 Force restarting voice recognition...");
        StopListening();
        Invoke("StartListening", 1f);
    }

    // Public method to get current status for debugging
    public string GetVoiceRecognitionStatus()
    {
        return $"isInitialized: {isInitialized}, permissionsGranted: {permissionsGranted}, " +
               $"isListening: {isListening}, speechRecognizer: {speechRecognizer != null}, " +
               $"vrDisplay: {vrDisplay != null}, waitingForLetterDisplay: {waitingForLetterDisplay}";
    }

    // Method to force a complete restart of the voice recognition system
    public void RestartVoiceRecognitionSystem()
    {
        Debug.Log("🔄 Force restarting entire voice recognition system...");
        
        // Stop everything
        StopListening();
        isInitialized = false;
        permissionsGranted = false;
        waitingForLetterDisplay = false;
        
        // Clean up existing speechRecognizer
        if (speechRecognizer != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                speechRecognizer.Call("destroy");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error destroying speech recognizer during restart: " + e.Message);
            }
#endif
            speechRecognizer = null;
        }
        
        // Restart the entire process
        Invoke("RequestMicrophonePermission", 1f);
    }    // Method called by VR display to start listening for a specific letter
    public void StartListeningForLetter()
    {
        Debug.Log("🎯 Starting voice listening for new letter");
        CancelInvoke("StartListening"); // Cancel any pending restart
        
        if (vrDisplay != null)
        {
            string currentLetter = vrDisplay.GetCurrentLetter();
            Debug.Log($"🔤 Now listening for letter: {currentLetter}");
        }
        
        StartListening();
    }

    // Method called by VR display to stop listening (letter is changing)
    public void StopListeningForLetter()
    {
        Debug.Log("🛑 Stopping voice listening (letter changing)");
        CancelInvoke("StartListening"); // Cancel any pending restart
        StopListening();
    }

    // Method to check if we should continue listening (called after speech result)
    public bool ShouldContinueListening()
    {
        return vrDisplay != null && vrDisplay.ShouldContinueListening();
    }
}
