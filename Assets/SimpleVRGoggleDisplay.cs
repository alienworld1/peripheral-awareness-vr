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
    public Color letterColor = Color.white;

    [Header("Letter Display")]
    public float displayInterval = 2f;
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
                leftEyeCamera.transform.localRotation = correctedRotation;

            if (rightEyeCamera != null)
                rightEyeCamera.transform.localRotation = correctedRotation;
            
            if (fixationPoint != null)
            {
                fixationPoint.transform.LookAt(leftEyeCamera.transform);
                fixationPoint.transform.Rotate(0, 180, 0);
            }

        }
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
        
        // Notify voice input that letters are now ready
        NotifyVoiceInputReady();
        
        while (isDisplaying)
        {
            foreach (var letter in instantiatedLetters)
                letter.SetActive(false);

            if (currentIndex < instantiatedLetters.Count)
            {
                instantiatedLetters[currentIndex].SetActive(true);
                Debug.Log($"🔠 Showing letter: {GetCurrentLetter()}");
            }

            yield return new WaitForSeconds(displayInterval);
            currentIndex = (currentIndex + 1) % instantiatedLetters.Count;
            yield return new WaitForSeconds(intervalBetweenLetters);
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

        fixationPoint.transform.position = fixedPosition + new Vector3(0, 0, distanceFromPlayer);
        fixationPoint.transform.LookAt(leftEyeCamera.transform);
        fixationPoint.transform.Rotate(0, 180, 0);
    }

    void CreateVoiceFeedbackUI()
    {
        feedbackText = new GameObject("VoiceFeedback");
        TextMeshPro textMesh = feedbackText.AddComponent<TextMeshPro>();

        textMesh.text = "🎤 Voice Recognition Ready";
        textMesh.fontSize = 0.03f * 100;
        textMesh.color = feedbackColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;

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
    }

    public void UpdateVoiceFeedback(string message, bool isCorrect = false)
    {
        if (feedbackText != null)
        {
            TextMeshPro textMesh = feedbackText.GetComponent<TextMeshPro>();
            textMesh.text = message;
            textMesh.color = isCorrect ? Color.green : Color.yellow;
            
            // Auto-clear after 2 seconds
            CancelInvoke("ClearVoiceFeedback");
            Invoke("ClearVoiceFeedback", 2f);
        }
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
    }
}
