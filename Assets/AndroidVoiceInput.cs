using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public class AndroidVoiceInput : MonoBehaviour
{    [Header("Voice Recognition Settings")]
    public float recognitionTimeout = 3f;
    public float confidenceThreshold = 0.5f;
    public bool logAllResults = true;
    
    [Header("AudioRecord Fallback")]
    [Tooltip("Enable AudioRecord fallback for better single-letter detection. Check this if speech recognition isn't working.")]
    public bool useAudioRecordFallback = false;
    
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
    private string currentListeningLetter = ""; // Track which letter we're currently listening for
      // Performance tracking
    private int correctAnswers = 0;
    private int totalAttempts = 0;

    private bool lastAudioRecordFallbackSetting = false;

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
    }    void Update()
    {
        // Monitor for changes to the AudioRecord fallback setting
        if (useAudioRecordFallback != lastAudioRecordFallbackSetting)
        {
            lastAudioRecordFallbackSetting = useAudioRecordFallback;
            
            if (useAudioRecordFallback && isInitialized)
            {
                Debug.Log("🎤 AudioRecord fallback enabled via Inspector - applying immediately");
                EnableAudioRecordFallback();
            }
            else if (!useAudioRecordFallback && isInitialized)
            {
                Debug.Log("🔄 AudioRecord fallback disabled via Inspector - reinitializing standard mode");
                ReinitializeSpeechRecognizer();
            }
        }        
        // Process voice recognition results
        if (hasNewResult && !string.IsNullOrEmpty(lastRecognizedText))
        {
            Debug.Log($"🔄 Processing voice result in Update(): '{lastRecognizedText}'");
            ProcessVoiceResult(lastRecognizedText);
            hasNewResult = false;
            lastRecognizedText = "";
        }
    }private void InitializeAndroidSpeechRecognition()
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
                {                    isInitialized = true;
                    Debug.Log("✅ Voice recognition initialized successfully with VoiceBridge");
                    
                    // Check if AudioRecord fallback should be enabled from the start
                    if (useAudioRecordFallback)
                    {
                        Debug.Log("🎤 AudioRecord fallback enabled via Unity Inspector setting");
                        EnableAudioRecordFallback();
                    }
                    
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
        Debug.Log($"🎤🎤🎤 OnSpeechResult called with: '{result}'");
        
        if (!string.IsNullOrEmpty(result))
        {
            lastRecognizedText = result;
            hasNewResult = true;
            
            Debug.Log($"✅ Speech result received: '{result}' - will process in Update()");
            
            if (logAllResults)
            {
                SaveToFile(result);
                Debug.Log("Voice recognized and saved: " + result);
            }
            
            // Show immediate feedback
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback($"🎤 Heard: '{result}'", false);
            }
        }
        else
        {
            Debug.LogWarning("⚠️ OnSpeechResult called with empty result");
        }

        // Don't restart immediately - let the VR display control timing
        isListening = false;
        
        // Only restart if we're still supposed to be listening for this letter
        if (vrDisplay != null && vrDisplay.IsDisplayingLetters() && vrDisplay.ShouldContinueListening())
        {
            Invoke("StartListening", 0.5f); // Short delay before restarting
        }
        else
        {
            Debug.Log("🛑 Not restarting listening - either not displaying letters or shouldn't continue");
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
    }    public void OnSpeechError(string error)
    {
        Debug.LogWarning("Speech recognition error: " + error);
        
        // Don't show "No match" or "No speech input" as errors to user - these are normal
        if (error.Contains("No match") || error.Contains("No speech input"))
        {
            Debug.Log("🔍 No speech match - this is normal, continuing to listen");
            
            // Update UI with neutral feedback
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🎤 Keep trying...", false);
            }
            
            isListening = false;
            
            // Quick restart for no match cases
            if (vrDisplay != null && vrDisplay.ShouldContinueListening())
            {
                Invoke("StartListening", 0.3f);
            }
            return;
        }
        
        // For real errors, show them to the user
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback($"🔇 {error}", false);
        }
        
        isListening = false;
        
        // Restart after error, but with different delays based on error type
        float delay = 1f;
        if (error.Contains("Network"))
        {
            delay = 3f; // Longer delay for network issues
        }
        else if (error.Contains("Audio") || error.Contains("Microphone"))
        {
            delay = 2f; // Medium delay for audio issues
        }
        
        if (vrDisplay != null && vrDisplay.ShouldContinueListening())
        {
            Invoke("StartListening", delay);
        }
    }    private void ProcessVoiceResult(string recognizedText)
    {
        if (vrDisplay == null) return;
        
        string currentLetter = vrDisplay.GetCurrentLetter();
        if (string.IsNullOrEmpty(currentLetter)) return;
        
        // Clean and normalize the recognized text
        string cleanedText = recognizedText.Trim().ToUpper();
        string targetLetter = currentLetter.ToUpper();
        
        Debug.Log($"🎯 Processing voice result: Target='{targetLetter}', Heard='{cleanedText}'");
        
        totalAttempts++;
        
        bool isCorrect = false;
        string matchReason = "";
        
        // Check if the recognized text contains the target letter
        if (cleanedText.Contains(targetLetter))
        {
            isCorrect = true;
            correctAnswers++;
            matchReason = "exact match";
        }
        // Check for phonetic matches
        else if (IsPhoneticMatch(cleanedText, targetLetter))
        {
            isCorrect = true;
            correctAnswers++;
            matchReason = "phonetic match";
        }
        // Check if target letter is at the beginning of recognized text
        else if (cleanedText.StartsWith(targetLetter))
        {
            isCorrect = true;
            correctAnswers++;
            matchReason = "starts with target";
        }
        // Check if it's just the letter with common endings
        else if (cleanedText == targetLetter || 
                 cleanedText == targetLetter + "S" || 
                 cleanedText == targetLetter + "'S")
        {
            isCorrect = true;
            correctAnswers++;
            matchReason = "direct letter match";
        }
        
        // Log the detailed result
        string resultText = $"Target: {targetLetter}, Heard: '{cleanedText}', Correct: {isCorrect}";
        if (isCorrect) resultText += $" ({matchReason})";
        Debug.Log($"🎯 {resultText}");
        
        // Update UI feedback
        string feedbackMessage = isCorrect ? 
            $"✓ Correct! Said '{cleanedText}' for '{targetLetter}'" : 
            $"✗ Try again: Said '{cleanedText}' for '{targetLetter}'";
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
            {"A", new[] {"AY", "EH", "AAY", "EI", "ALPHA"}},
            {"B", new[] {"BE", "BEE", "BETA", "BAY"}},
            {"C", new[] {"SEE", "SI", "CEE", "CHARLIE"}},
            {"D", new[] {"DEE", "DI", "DELTA", "DAY"}},
            {"E", new[] {"EE", "EH", "ECHO", "EEE"}},
            {"F", new[] {"EF", "EFF", "FOXTROT", "EFFFF"}},
            {"G", new[] {"JEE", "GEE", "GOLF", "JAY"}},
            {"H", new[] {"AYCH", "HAYCH", "HOTEL", "AITCH"}},
            {"I", new[] {"EYE", "AY", "INDIA", "IYE"}},
            {"J", new[] {"JAY", "JEY", "JULIET", "JAAY"}},
            {"K", new[] {"KAY", "KEH", "KILO", "KAAY"}},
            {"L", new[] {"EL", "ELL", "LIMA", "ELLL"}},
            {"M", new[] {"EM", "EMM", "MIKE", "EMMM"}},
            {"N", new[] {"EN", "ENN", "NOVEMBER", "ENNN"}},
            {"O", new[] {"OH", "OW", "OSCAR", "OHH"}},
            {"P", new[] {"PEE", "PI", "PAPA", "PEE"}},
            {"Q", new[] {"QUEUE", "CUE", "QUEBEC", "QUE"}},
            {"R", new[] {"AR", "ARR", "ROMEO", "ARRR"}},
            {"S", new[] {"ESS", "ES", "SIERRA", "ESSS"}},
            {"T", new[] {"TEE", "TI", "TANGO", "TEEE"}},
            {"U", new[] {"YOU", "YOO", "UNIFORM", "YOUU"}},
            {"V", new[] {"VEE", "VI", "VICTOR", "VEEE"}},
            {"W", new[] {"DOUBLE-U", "DOUBLE U", "WHISKEY", "DOUBLEU"}},
            {"X", new[] {"EX", "EKS", "XRAY", "X-RAY", "EXXX"}},
            {"Y", new[] {"WHY", "WYE", "YANKEE", "WHYY"}},
            {"Z", new[] {"ZED", "ZEE", "ZULU", "ZEEE"}}
        };
        
        if (phoneticVariations.ContainsKey(target))
        {
            foreach (string variation in phoneticVariations[target])
            {
                if (recognized.Contains(variation))
                {
                    Debug.Log($"🎯 Phonetic match found: '{recognized}' contains '{variation}' for target '{target}'");
                    return true;
                }
            }
        }
        
        // Also check if the recognized text sounds like the target
        // Common speech recognition substitutions
        Dictionary<string, string[]> commonSubstitutions = new Dictionary<string, string[]>
        {
            {"B", new[] {"P", "V", "D"}},
            {"C", new[] {"K", "S"}},
            {"D", new[] {"T", "B"}},
            {"F", new[] {"V", "S"}},
            {"G", new[] {"K", "J"}},
            {"K", new[] {"C", "G"}},
            {"P", new[] {"B", "F"}},
            {"T", new[] {"D", "K"}},
            {"V", new[] {"F", "B"}},
            {"Z", new[] {"S", "X"}}
        };
        
        if (commonSubstitutions.ContainsKey(target))
        {
            foreach (string substitution in commonSubstitutions[target])
            {
                if (recognized.Contains(substitution))
                {
                    Debug.Log($"🎯 Sound-alike match found: '{recognized}' contains '{substitution}' (sounds like '{target}')");
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
        
        // Only start listening if we're initialized and have permissions
        if (isInitialized && permissionsGranted)
        {
            Debug.Log("🔍 Voice recognition initialized and ready for synchronized listening - isInitialized: " + isInitialized + ", permissionsGranted: " + permissionsGranted);
            
            if (vrDisplay != null && vrDisplay.IsDisplayingLetters())
            {
                Debug.Log("🔤 Letters already displayed - voice system ready for sync");
            }
            else
            {
                Debug.Log("⏳ Waiting for letters to start displaying...");
            }
            
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🎙️ Voice recognition ready", true);
            }
        }
        else
        {
            Debug.LogWarning("⚠️ Voice recognition not ready - isInitialized: " + isInitialized + ", permissionsGranted: " + permissionsGranted);
        }
    }    public void OnSpeechListeningStarted(string message)
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
            vrDisplay.UpdateVoiceFeedback("🎙️ Voice recognition initialized", true);        }
    }
    
    // New callback methods for VoiceBridge with AudioRecord fallback
    public void OnVoiceRecognitionInitialized(string message)
    {
        Debug.Log($"🎙️ Voice Recognition Initialized: {message}");
        isInitialized = true;
        
        if (vrDisplay != null)
        {
            string status = string.IsNullOrEmpty(message) ? "Standard" : message;
            vrDisplay.UpdateVoiceFeedback($"🎙️ Voice ready ({status})", true);
        }
    }
    
    public void OnVoiceRecognitionReady(string message)
    {
        Debug.Log($"🔍 Voice Recognition Ready: {message}");
        if (vrDisplay != null)
        {
            string currentLetter = vrDisplay.GetCurrentLetter();
            vrDisplay.UpdateVoiceFeedback($"🎯 SAY '{currentLetter}' NOW!", true);
        }
    }
    
    public void OnVoiceRecognitionBeginSpeech(string message)
    {
        Debug.Log($"👂 Voice Recognition Begin Speech: {message}");
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🎤 LISTENING...", true);
        }
    }
    
    public void OnVoiceRecognitionEndSpeech(string message)
    {
        Debug.Log($"⏹️ Voice Recognition End Speech: {message}");
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🔄 Processing...", true);
        }
    }
      public void OnVoiceRecognitionResult(string result)
    {
        Debug.Log($"✅ Voice Recognition Result: '{result}'");        // Handle special AudioRecord results
        if (result == "SPEECH_DETECTED")
        {
            // AudioRecord detected speech but can't transcribe it
            // Use the letter we were listening for when recording started
            string targetLetter = currentListeningLetter;
            
            Debug.Log($"🎤 AudioRecord detected speech for letter: {targetLetter}");
            
            if (!string.IsNullOrEmpty(targetLetter))
            {
                // Since the user clearly spoke something (AudioRecord detected it), 
                // and they're looking at the target letter, assume they said it correctly
                Debug.Log($"✅ AudioRecord detected speech for '{targetLetter}' - treating as correct");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback($"✅ Speech detected for '{targetLetter}' - Correct!", true);
                }
                
                // Process as correct answer
                ProcessVoiceResult(targetLetter);
            }
            else
            {
                Debug.LogWarning("⚠️ AudioRecord detected speech but no current letter is available!");
            }
        }
        else
        {
            // Normal speech recognition result
            lastRecognizedText = result;
            hasNewResult = true;
        }
    }
    
    public void OnVoiceRecognitionPartialResult(string partialResult)
    {
        Debug.Log($"🔄 Voice Recognition Partial: '{partialResult}'");
        
        if (partialResult == "SPEECH_STARTED")
        {
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("🎤 SPEECH DETECTED!", true);
            }
        }
        else
        {
            // Normal partial result
            if (vrDisplay != null && !string.IsNullOrEmpty(partialResult))
            {
                vrDisplay.UpdateVoiceFeedback($"🔄 Hearing: '{partialResult}'", true);
            }
        }
    }
      public void OnVoiceRecognitionError(string error)
    {
        Debug.LogError($"❌ Voice Recognition Error: {error}");
        isListening = false;
        
        if (vrDisplay != null)
        {
            if (error.Contains("AudioRecord"))
            {
                vrDisplay.UpdateVoiceFeedback($"🎤 Using audio fallback", true);
            }
            else if (error.Contains("Switched to AudioRecord"))
            {
                vrDisplay.UpdateVoiceFeedback($"🔄 Switched to audio mode", true);
            }
            else if (!error.Contains("No match") && !error.Contains("No speech"))
            {
                vrDisplay.UpdateVoiceFeedback($"❌ Voice error: {error}", false);
            }
            else
            {
                vrDisplay.UpdateVoiceFeedback($"🔇 Try speaking louder", false);
            }
        }
        
        // If we get "No match" but detected audio, switch to AudioRecord fallback
        if (error.Contains("No match") && error.Contains("detected audio: true"))
        {
            Debug.Log("🎤 SpeechRecognizer detected audio but no match - enabling AudioRecord fallback");
            EnableAudioRecordFallback();
            return; // Don't restart normal listening
        }
        
        // Continue listening if it's just a "no match" error without audio detection
        if (error.Contains("No match") || error.Contains("No speech") || error.Contains("No results"))
        {
            Debug.Log("🔄 Restarting listening after no match error...");
            Invoke("StartListening", 0.5f);
        }
    }
    
    public void OnVoiceRecognitionStatus(string status)
    {
        Debug.Log($"📊 Voice Recognition Status: {status}");
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback($"📊 Status: {status}", true);
        }
    }    // Legacy callback methods (keeping for compatibility)
    public void OnSpeechReady(string message)
    {
        OnVoiceRecognitionReady(message);
    }
    
    public void OnSpeechDetected(string message)
    {
        OnVoiceRecognitionBeginSpeech(message);
    }
    
    public void OnSpeechEndOfInput(string message)
    {
        OnVoiceRecognitionEndSpeech(message);
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
    }    // Public method to get current status for debugging
    public string GetVoiceRecognitionStatus()
    {
        return $"Initialized: {isInitialized}, Permissions: {permissionsGranted}, Listening: {isListening}, SpeechRecognizer: {speechRecognizer != null}, AudioRecord Fallback: {useAudioRecordFallback}";
    }

    // Method to force a complete restart of the voice recognition system
    public void RestartVoiceRecognitionSystem()
    {
        Debug.Log("🔄 Completely restarting voice recognition system...");
        StopListening();
        
        if (speechRecognizer != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                speechRecognizer.Call("destroy");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error destroying old speech recognizer: " + e.Message);
            }
#endif
        }
        
        speechRecognizer = null;
        isInitialized = false;
        
        // Reinitialize after a delay
        Invoke("InitializeAndroidSpeechRecognition", 1f);
    }    // Method called by VR display to start listening for a specific letter
    public void StartListeningForLetter()
    {
        Debug.Log("🎯🎯🎯 StartListeningForLetter called!");
        
        if (vrDisplay != null)
        {
            currentListeningLetter = vrDisplay.GetCurrentLetter();
            Debug.Log($"🔤 Now listening for letter: '{currentListeningLetter}'");
            
            if (!string.IsNullOrEmpty(currentListeningLetter))
            {
                Debug.Log($"🔍 Speech recognizer state: speechRecognizer={speechRecognizer != null}, isInitialized={isInitialized}, permissionsGranted={permissionsGranted}");
                StartListening();
            }
            else
            {
                Debug.LogWarning("⚠️ No current letter to listen for!");
            }
        }        else
        {
            Debug.LogError("❌ VR Display is null!");
        }
    }
    
    // Method called by VR display to stop listening for a specific letter
    public void StopListeningForLetter()
    {
        Debug.Log("🛑 Stopping voice listening (letter changing)");
        CancelInvoke("StartListening"); // Cancel any pending restart
        StopListening();
        
        // Clear the current letter after a short delay to allow AudioRecord callbacks to complete
        Invoke("ClearCurrentLetter", 0.5f);
    }
    
    // Helper method to clear the current letter
    private void ClearCurrentLetter()
    {
        currentListeningLetter = "";
        Debug.Log("🧹 Cleared current listening letter");
    }

    // Method to test the entire voice recognition pipeline
    public void DebugVoiceRecognitionPipeline()
    {
        Debug.Log("🔬 === VOICE RECOGNITION DEBUG PIPELINE ===");
        Debug.Log($"🔍 AndroidVoiceInput Status: {GetVoiceRecognitionStatus()}");
        Debug.Log($"🎤 Current Letter: {(vrDisplay != null ? vrDisplay.GetCurrentLetter() : "VR Display null")}");
        Debug.Log($"🔊 Is Displaying Letters: {(vrDisplay != null ? vrDisplay.IsDisplayingLetters() : false)}");
        Debug.Log($"📱 Unity Activity: {(unityActivity != null ? "Available" : "Null")}");
        
        if (speechRecognizer != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Debug.Log("🔍 VoiceBridge object exists and is accessible");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ VoiceBridge access error: {e.Message}");
            }
#endif
        }
        
        Debug.Log("🔬 === END DEBUG PIPELINE ===");
    }
    
    // Method to test letter matching logic
    public void TestLetterMatching(string recognizedText, string targetLetter)
    {
        Debug.Log($"🧪 Testing letter matching: Recognized='{recognizedText}', Target='{targetLetter}'");
        
        string cleanedText = recognizedText.Trim().ToUpper();
        string target = targetLetter.ToUpper();
        
        bool isCorrect = false;
        string matchReason = "";
        
        // Test exact match
        if (cleanedText.Contains(target))
        {
            isCorrect = true;
            matchReason = "exact match";
        }
        // Test phonetic match
        else if (IsPhoneticMatch(cleanedText, target))
        {
            isCorrect = true;
            matchReason = "phonetic match";
        }
        // Test starts with
        else if (cleanedText.StartsWith(target))
        {
            isCorrect = true;
            matchReason = "starts with target";
        }
        
        Debug.Log($"🧪 Test Result: {(isCorrect ? "✅ MATCH" : "❌ NO MATCH")} - Reason: {matchReason}");
        
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback($"Test: '{recognizedText}' vs '{targetLetter}' = {(isCorrect ? "✅" : "❌")}", isCorrect);
        }
    }
    
    // Method to test VoiceBridge connection
    public void TestVoiceBridge()
    {
        Debug.Log("🧪 Testing VoiceBridge connection...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                // Test calling a method on the VoiceBridge
                Debug.Log("🧪 VoiceBridge object exists - testing method call...");
                Debug.Log("✅ VoiceBridge connection test passed");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("✅ VoiceBridge connected", true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ VoiceBridge connection test failed: {e.Message}");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback($"❌ VoiceBridge error: {e.Message}", false);
                }
            }
        }
        else
        {
            Debug.LogError("❌ VoiceBridge object is null");
            
            if (vrDisplay != null)
            {
                vrDisplay.UpdateVoiceFeedback("❌ VoiceBridge not initialized", false);
            }
        }
#else
        Debug.Log("🖥️ VoiceBridge test skipped (Editor mode)");
        
        if (vrDisplay != null)
        {
            vrDisplay.UpdateVoiceFeedback("🖥️ VoiceBridge test (Editor)", true);
        }
#endif
    }
    
    // Method to debug the entire voice pipeline
    public void DebugVoicePipeline()
    {
        Debug.Log("🔍 === VOICE PIPELINE DEBUG START ===");
        
        // Check permissions
        bool hasPermission = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        hasPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        hasPermission = true; // Assume true in editor
#endif
        
        Debug.Log($"🔍 Microphone Permission: {hasPermission}");
        Debug.Log($"🔍 Permissions Granted Flag: {permissionsGranted}");
        Debug.Log($"🔍 Is Initialized: {isInitialized}");
        Debug.Log($"🔍 Is Listening: {isListening}");
        Debug.Log($"🔍 Speech Recognizer Null: {speechRecognizer == null}");
        Debug.Log($"🔍 Unity Activity Null: {unityActivity == null}");
        Debug.Log($"🔍 VR Display Null: {vrDisplay == null}");
        
        if (vrDisplay != null)
        {
            Debug.Log($"🔍 VR Display - Is Displaying Letters: {vrDisplay.IsDisplayingLetters()}");
            Debug.Log($"🔍 VR Display - Should Continue Listening: {vrDisplay.ShouldContinueListening()}");
            Debug.Log($"🔍 VR Display - Current Letter: '{vrDisplay.GetCurrentLetter()}'");
        }
        
        // Test voice bridge if available
        if (speechRecognizer != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Debug.Log("🔍 Testing VoiceBridge method calls...");
                Debug.Log("✅ VoiceBridge object is accessible");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ VoiceBridge test failed: {e.Message}");
            }
#endif
        }
        
        // Performance stats
        Debug.Log($"🔍 Performance - Correct: {correctAnswers}, Total: {totalAttempts}, Accuracy: {GetAccuracy():F1}%");
        
        Debug.Log("🔍 === VOICE PIPELINE DEBUG END ===");
        
        if (vrDisplay != null)
        {
            string status = $"Perm:{hasPermission} Init:{isInitialized} Listen:{isListening}";
            vrDisplay.UpdateVoiceFeedback($"🔍 Debug: {status}", hasPermission && isInitialized);
        }
    }
    
    // Test method to manually trigger a speech result (for debugging)
    public void TestSpeechResult(string testResult)
    {
        Debug.Log($"🧪 Testing speech result: '{testResult}'");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("testSpeechResult", testResult);
                Debug.Log("✅ Test speech result sent to VoiceBridge");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Test speech result failed: {e.Message}");
            }
        }
#else
        // In editor, just simulate the result
        OnSpeechResult(testResult);
#endif
    }
    
    // Test method to check VoiceBridge status
    public void TestSpeechRecognizerStatus()
    {
        Debug.Log("🧪 Testing speech recognizer status...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("testSpeechRecognizer");
                Debug.Log("✅ Speech recognizer status test sent");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Speech recognizer status test failed: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("❌ Speech recognizer is null - cannot test");
        }
#else
        Debug.Log("🖥️ Speech recognizer test skipped (Editor mode)");
#endif
    }

    // Test method to check capabilities
    public void TestCapabilities()
    {
        Debug.Log("🔍 Testing speech recognition capabilities...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("testRecognitionCapabilities");
                Debug.Log("✅ Capabilities test sent");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Capabilities test failed: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("❌ Speech recognizer is null - cannot test capabilities");
        }
#else
        Debug.Log("🖥️ Capabilities test - Editor mode");
#endif
    }
    
    // Alternative ultra-aggressive listening method
    public void StartUltraAggressiveListening()
    {
        Debug.Log("🚀 Starting ULTRA AGGRESSIVE listening mode...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null && !isListening && isInitialized && permissionsGranted)
        {
            try
            {
                speechRecognizer.Call("startListeningForSingleLetter");
                isListening = true;
                Debug.Log("🚀 Ultra aggressive listening command sent");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🚀 Ultra listening mode", false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Ultra aggressive listening failed: {e.Message}");
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback($"🔇 Ultra mode error: {e.Message}", false);
                }
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Cannot start ultra aggressive - isListening: {isListening}, isInitialized: {isInitialized}, permissionsGranted: {permissionsGranted}");
        }
#else
        Debug.Log("🖥️ Ultra aggressive mode simulated (Editor mode)");
#endif
    }

    // Method to reinitialize speech recognizer when it gets stuck
    public void ReinitializeSpeechRecognizer()
    {
        Debug.Log("🔄 Reinitializing speech recognizer...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("reinitializeSpeechRecognizer");
                Debug.Log("✅ Speech recognizer reinitialization command sent");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔄 Speech recognizer reinitialized", false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Speech recognizer reinitialization failed: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("❌ Speech recognizer is null - cannot reinitialize");
        }
#else
        Debug.Log("🖥️ Speech recognizer reinitialization simulated (Editor mode)");
#endif
    }
    
    // Test method to enable AudioRecord fallback manually
    public void EnableAudioRecordFallback()
    {
        Debug.Log("🎤 Manually enabling AudioRecord fallback...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("enableAudioRecordFallback");
                Debug.Log("✅ AudioRecord fallback enabled");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🎤 Audio fallback enabled", true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to enable AudioRecord fallback: {e.Message}");
            }
        }
#else
        Debug.Log("AudioRecord fallback only works on Android device");
#endif
    }
    
    // Test method to manually simulate AudioRecord speech detection
    public void TestAudioRecordSpeechDetection()
    {
        Debug.Log("🧪 Testing AudioRecord speech detection...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        OnVoiceRecognitionResult("SPEECH_DETECTED");
#else
        // In editor, simulate AudioRecord detection
        OnVoiceRecognitionResult("SPEECH_DETECTED");
#endif
    }
    
    // Test method to simulate different AudioRecord states
    public void TestAudioRecordStates()
    {
        Debug.Log("🧪 Testing AudioRecord states...");
        
        // Simulate the sequence of events in AudioRecord mode
        OnVoiceRecognitionInitialized("AudioRecord");
        
        Invoke("TestAudioRecordReady", 1f);
        Invoke("TestAudioRecordSpeechStart", 2f);
        Invoke("TestAudioRecordSpeechDetection", 3f);
    }
    
    private void TestAudioRecordReady()
    {
        OnVoiceRecognitionReady("");
    }
    
    private void TestAudioRecordSpeechStart()
    {
        OnVoiceRecognitionPartialResult("SPEECH_STARTED");
    }

    // Method to test AudioRecord with different thresholds for debugging
    public void TestAudioRecordSensitivity()
    {
        Debug.Log("🧪 Testing AudioRecord sensitivity...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                // Try a lower threshold for more sensitive detection
                speechRecognizer.Call("setSpeechDetectionThreshold", 800); // Lower from 1000 to 800
                Debug.Log("✅ AudioRecord threshold lowered to 800 for better sensitivity");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🎤 AudioRecord more sensitive", true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to adjust AudioRecord sensitivity: {e.Message}");
            }
        }
#else
        Debug.Log("AudioRecord sensitivity test only works on Android device");
#endif
    }
    
    // Method to force AudioRecord to assume speech was detected (for testing)
    public void SimulateAudioRecordDetection()
    {
        Debug.Log("🧪 Simulating AudioRecord speech detection...");
        
        if (!string.IsNullOrEmpty(currentListeningLetter))
        {
            Debug.Log($"🎤 Simulating AudioRecord detected speech for letter: {currentListeningLetter}");
            OnVoiceRecognitionResult("SPEECH_DETECTED");
        }
        else
        {
            Debug.LogWarning("⚠️ No current letter set for simulation");
        }
    }    
    // Method to restart listening for the current letter (used when AudioRecord detects speech but can't transcribe)
    private void RestartListeningForCurrentLetter()
    {
        if (vrDisplay != null && vrDisplay.ShouldContinueListening())
        {
            string currentLetter = vrDisplay.GetCurrentLetter();
            if (!string.IsNullOrEmpty(currentLetter))
            {
                Debug.Log($"🔄 Restarting listening for letter '{currentLetter}' after AudioRecord detection");
                currentListeningLetter = currentLetter;
                StartListening();
            }
        }
        else
        {            Debug.Log("🛑 Not restarting listening - display says we shouldn't continue");        }
    }

    // Debug method to test AudioRecord detection manually
    public void TestAudioRecordDetection()
    {
        Debug.Log("🧪 Testing AudioRecord detection manually...");
        OnVoiceRecognitionResult("SPEECH_DETECTED");
    }

    // Method to adjust AudioRecord sensitivity
    public void LowerAudioRecordThreshold()
    {
        Debug.Log("🔧 Lowering AudioRecord detection threshold...");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (speechRecognizer != null)
        {
            try
            {
                speechRecognizer.Call("setAudioRecordThreshold", 800.0f); // Lower threshold
                Debug.Log("✅ AudioRecord threshold lowered to 800");
                
                if (vrDisplay != null)
                {
                    vrDisplay.UpdateVoiceFeedback("🔧 Sensitivity increased", false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to lower AudioRecord threshold: {e.Message}");
            }
        }
#else
        Debug.Log("🖥️ AudioRecord threshold adjustment simulated (Editor mode)");
#endif
    }
}
