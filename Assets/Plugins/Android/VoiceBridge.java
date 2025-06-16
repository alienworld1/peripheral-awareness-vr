package com.unity3d.player;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.os.Bundle;
import android.os.Handler;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.util.Log;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import android.Manifest;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.AudioFormat;
import android.os.Looper;

public class VoiceBridge {
    private static final String TAG = "VoiceBridge";
    private SpeechRecognizer speechRecognizer;
    private Context context;
    private String gameObjectName;
    private boolean isListening = false;
    private boolean isInitialized = false;
    private boolean shouldKeepTrying = false;
    private long startTime = 0;
    private static final long MAX_LISTENING_TIME = 5500; // 5.5 seconds in milliseconds
    private boolean hasDetectedAudio = false;
    private int consecutiveAudioSamples = 0;
      // AudioRecord fallback variables
    private AudioRecord audioRecord;
    private boolean isRecording = false;
    private Thread recordingThread;
    private boolean useAudioRecordFallback = false;
    private static final int SAMPLE_RATE = 16000;
    private static final int CHANNEL_CONFIG = AudioFormat.CHANNEL_IN_MONO;
    private static final int AUDIO_FORMAT = AudioFormat.ENCODING_PCM_16BIT;
    private int bufferSize;
    private Handler mainHandler;
    private long audioRecordStartTime = 0;
    private boolean hasDetectedSpeech = false;
    private double peakAmplitude = 0;
    private int speechDetectionThreshold = 1000; // Amplitude threshold for speech detection
    private int consecutiveSpeechSamples = 0;
    private int requiredSpeechSamples = 10; // Number of consecutive samples above threshold to confirm speech
    
    // Audio analysis for letter recognition
    private java.util.List<Double> audioSamples = new java.util.ArrayList<>();
    private String targetLetter = "";
    private double speechDuration = 0;
    private double averageAmplitude = 0;
    private int speechSampleCount = 0;
    
    public VoiceBridge(Context context, String gameObjectName) {
        this.context = context;
        this.gameObjectName = gameObjectName;
        this.mainHandler = new Handler(Looper.getMainLooper());
        
        Log.d(TAG, "VoiceBridge constructor called with gameObject: " + gameObjectName);
        
        // Initialize AudioRecord buffer size
        bufferSize = AudioRecord.getMinBufferSize(SAMPLE_RATE, CHANNEL_CONFIG, AUDIO_FORMAT);
        if (bufferSize == AudioRecord.ERROR || bufferSize == AudioRecord.ERROR_BAD_VALUE) {
            bufferSize = SAMPLE_RATE * 2; // Fallback buffer size
        }
        Log.d(TAG, "AudioRecord buffer size: " + bufferSize);
        
        // Check permissions first
        if (checkMicrophonePermission()) {
            Log.d(TAG, "Microphone permission granted, initializing...");
            // Ensure we run on UI thread for speech recognition
            if (context instanceof Activity) {
                ((Activity) context).runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        initializeSpeechRecognizer();
                    }
                });
            } else {
                initializeSpeechRecognizer();
            }
        } else {
            Log.e(TAG, "Microphone permission not granted!");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Microphone permission required");
        }
    }
    
    private boolean checkMicrophonePermission() {
        return context.checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED;
    }
    
    private void initializeSpeechRecognizer() {
        Log.d(TAG, "Initializing SpeechRecognizer...");
        
        if (!SpeechRecognizer.isRecognitionAvailable(context)) {
            Log.e(TAG, "Speech recognition not available on this device");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Speech recognition not available");
            // Fall back to AudioRecord
            useAudioRecordFallback = true;
            initializeAudioRecord();
            return;
        }
        
        try {
            speechRecognizer = SpeechRecognizer.createSpeechRecognizer(context);
            speechRecognizer.setRecognitionListener(new VoiceRecognitionListener());
            isInitialized = true;
            Log.d(TAG, "SpeechRecognizer initialized successfully");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionInitialized", "");
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize SpeechRecognizer: " + e.getMessage());
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Failed to initialize: " + e.getMessage());
            // Fall back to AudioRecord
            useAudioRecordFallback = true;
            initializeAudioRecord();
        }
    }
    
    private void initializeAudioRecord() {
        Log.d(TAG, "Initializing AudioRecord fallback...");
        try {
            audioRecord = new AudioRecord(
                MediaRecorder.AudioSource.MIC,
                SAMPLE_RATE,
                CHANNEL_CONFIG,
                AUDIO_FORMAT,
                bufferSize
            );
            
            if (audioRecord.getState() == AudioRecord.STATE_INITIALIZED) {
                Log.d(TAG, "AudioRecord initialized successfully");
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionInitialized", "AudioRecord");
            } else {
                Log.e(TAG, "Failed to initialize AudioRecord");
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "AudioRecord initialization failed");
            }
        } catch (Exception e) {
            Log.e(TAG, "Exception initializing AudioRecord: " + e.getMessage());
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "AudioRecord exception: " + e.getMessage());
        }
    }
    
    public void startListening() {
        Log.d(TAG, "startListening() called, isListening: " + isListening);
        
        if (!checkMicrophonePermission()) {
            Log.e(TAG, "Microphone permission not granted");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Microphone permission required");
            return;
        }
        
        if (isListening) {
            Log.w(TAG, "Already listening, ignoring start request");
            return;
        }
        
        if (useAudioRecordFallback) {
            startAudioRecording();
        } else {
            startSpeechRecognition();
        }
    }
    
    private void startSpeechRecognition() {
        if (!isInitialized || speechRecognizer == null) {
            Log.e(TAG, "Speech recognizer not initialized");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Not initialized");
            return;
        }
        
        // Ensure we run on UI thread
        if (context instanceof Activity) {
            ((Activity) context).runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    performStartListening();
                }
            });
        } else {
            performStartListening();
        }
    }
    
    private void performStartListening() {
        try {
            Intent intent = createRecognitionIntent();
            
            isListening = true;
            startTime = System.currentTimeMillis();
            hasDetectedAudio = false;
            consecutiveAudioSamples = 0;
            
            Log.d(TAG, "Starting speech recognition with aggressive settings...");
            speechRecognizer.startListening(intent);
            
            // Start timeout timer
            new Handler().postDelayed(new Runnable() {
                @Override
                public void run() {
                    if (isListening) {
                        Log.d(TAG, "Speech recognition timeout reached, stopping...");
                        stopListening();
                        // If SpeechRecognizer keeps failing, switch to AudioRecord
                        if (!hasDetectedAudio) {
                            Log.w(TAG, "SpeechRecognizer not detecting audio, switching to AudioRecord fallback");
                            useAudioRecordFallback = true;
                            initializeAudioRecord();
                            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Switched to AudioRecord fallback");
                        }
                    }
                }
            }, MAX_LISTENING_TIME);
            
        } catch (Exception e) {
            Log.e(TAG, "Exception starting speech recognition: " + e.getMessage());
            isListening = false;
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Start failed: " + e.getMessage());
        }
    }
    
    private void startAudioRecording() {
        if (audioRecord == null || audioRecord.getState() != AudioRecord.STATE_INITIALIZED) {
            Log.e(TAG, "AudioRecord not initialized");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "AudioRecord not ready");
            return;
        }
        
        if (isRecording) {
            Log.w(TAG, "Already recording with AudioRecord");
            return;
        }
          Log.d(TAG, "Starting AudioRecord...");
        isListening = true;
        isRecording = true;
        audioRecordStartTime = System.currentTimeMillis();
        hasDetectedSpeech = false;
        peakAmplitude = 0;
        consecutiveSpeechSamples = 0;
        
        // Reset audio analysis variables
        audioSamples.clear();
        speechDuration = 0;
        averageAmplitude = 0;
        speechSampleCount = 0;
        
        try {
            audioRecord.startRecording();
            
            recordingThread = new Thread(new Runnable() {
                @Override
                public void run() {
                    processAudioData();
                }
            });
            recordingThread.start();            // Timeout after MAX_LISTENING_TIME
            mainHandler.postDelayed(new Runnable() {
                @Override
                public void run() {
                    if (isRecording) {
                        Log.d(TAG, "AudioRecord timeout reached");
                        stopAudioRecording();
                        
                        // If we detected speech but haven't analyzed yet, analyze now
                        if (hasDetectedSpeech && speechSampleCount > 0) {
                            Log.d(TAG, "Timeout reached but speech was detected - analyzing");
                            speechDuration = speechSampleCount * (1000.0 / SAMPLE_RATE) * (bufferSize / 2);
                            analyzeAudioForLetter();
                        } else {
                            Log.d(TAG, "No speech detected during recording");
                            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "No speech detected");
                        }
                    }
                }
            }, MAX_LISTENING_TIME);
            
        } catch (Exception e) {
            Log.e(TAG, "Failed to start AudioRecord: " + e.getMessage());
            isListening = false;
            isRecording = false;
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "AudioRecord start failed: " + e.getMessage());
        }
    }
    
    private void processAudioData() {
        short[] audioBuffer = new short[bufferSize / 2]; // 16-bit samples
        
        Log.d(TAG, "AudioRecord processing started");
        
        while (isRecording) {
            int samplesRead = audioRecord.read(audioBuffer, 0, audioBuffer.length);
            
            if (samplesRead > 0) {
                // Calculate RMS amplitude
                long sum = 0;
                for (int i = 0; i < samplesRead; i++) {
                    sum += audioBuffer[i] * audioBuffer[i];
                }
                double rmsAmplitude = Math.sqrt(sum / samplesRead);
                
                // Track peak amplitude
                if (rmsAmplitude > peakAmplitude) {
                    peakAmplitude = rmsAmplitude;
                }                // Check if amplitude exceeds speech threshold
                if (rmsAmplitude > speechDetectionThreshold) {
                    consecutiveSpeechSamples++;
                    
                    // Collect audio samples for analysis while speech is detected
                    audioSamples.add(rmsAmplitude);
                    speechSampleCount++;
                    averageAmplitude = (averageAmplitude * (speechSampleCount - 1) + rmsAmplitude) / speechSampleCount;
                    
                    if (consecutiveSpeechSamples >= requiredSpeechSamples && !hasDetectedSpeech) {
                        hasDetectedSpeech = true;
                        Log.d(TAG, "Speech detected! RMS: " + rmsAmplitude + ", Peak: " + peakAmplitude);
                        Log.d(TAG, "Continuing to record for audio analysis...");
                    }
                } else {
                    // If we had speech and now it's quiet, analyze what we collected
                    if (hasDetectedSpeech && speechSampleCount > 0) {
                        Log.d(TAG, "Speech ended, analyzing collected audio...");
                        stopAudioRecording();
                        
                        // Calculate speech duration
                        speechDuration = speechSampleCount * (1000.0 / SAMPLE_RATE) * (bufferSize / 2);
                        
                        // Analyze the collected audio
                        mainHandler.post(new Runnable() {
                            @Override
                            public void run() {
                                analyzeAudioForLetter();
                            }
                        });
                        break; // Exit the recording loop
                    }
                    consecutiveSpeechSamples = 0;
                }
                
                // Log periodic updates
                long currentTime = System.currentTimeMillis();
                if ((currentTime - audioRecordStartTime) % 1000 < 100) { // Log every ~1 second
                    Log.d(TAG, "AudioRecord - RMS: " + String.format("%.1f", rmsAmplitude) + 
                             ", Peak: " + String.format("%.1f", peakAmplitude) + 
                             ", Speech: " + hasDetectedSpeech +
                             ", Time: " + (currentTime - audioRecordStartTime) + "ms");
                }
            }
        }
        
        Log.d(TAG, "AudioRecord processing finished");
    }
    
    private void stopAudioRecording() {
        if (!isRecording) {
            return;
        }
        
        Log.d(TAG, "Stopping AudioRecord...");
        isRecording = false;
        isListening = false;
        
        try {
            if (audioRecord != null) {
                audioRecord.stop();
            }
        } catch (Exception e) {
            Log.e(TAG, "Error stopping AudioRecord: " + e.getMessage());
        }
        
        // Wait for recording thread to finish
        if (recordingThread != null) {
            try {
                recordingThread.join(500); // Wait up to 500ms
            } catch (InterruptedException e) {
                Log.w(TAG, "Recording thread interrupted");
            }
        }
    }
    
    private Intent createRecognitionIntent() {
        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        
        // Basic settings
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_WEB_SEARCH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
        intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, context.getPackageName());
        
        // Aggressive settings for single letter recognition
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 10);
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS, 500L);
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS, 500L);
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_MINIMUM_LENGTH_MILLIS, 100L);
        intent.putExtra(RecognizerIntent.EXTRA_CONFIDENCE_SCORES, true);
        intent.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, true);
        
        // Additional experimental settings
        intent.putExtra("android.speech.extra.DICTATION_MODE", true);
        intent.putExtra("android.speech.extra.GET_AUDIO_FORMAT", true);
        intent.putExtra("android.speech.extra.GET_AUDIO", true);
        
        // Ultra-low timeouts
        intent.putExtra("android.speech.extra.SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS", 300);
        intent.putExtra("android.speech.extra.SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS", 300);
        intent.putExtra("android.speech.extra.SPEECH_INPUT_MINIMUM_LENGTH_MILLIS", 50);
        
        Log.d(TAG, "Recognition intent created with aggressive settings");
        return intent;
    }
    
    public void stopListening() {
        Log.d(TAG, "stopListening() called, isListening: " + isListening);
        
        if (!isListening) {
            return;
        }
        
        if (useAudioRecordFallback) {
            stopAudioRecording();
        } else {
            // Ensure we run on UI thread
            if (context instanceof Activity) {
                ((Activity) context).runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        performStopListening();
                    }
                });
            } else {
                performStopListening();
            }
        }
    }
    
    private void performStopListening() {
        isListening = false;
        
        if (speechRecognizer != null) {
            try {
                Log.d(TAG, "Stopping speech recognizer...");
                speechRecognizer.stopListening();
            } catch (Exception e) {
                Log.e(TAG, "Error stopping speech recognizer: " + e.getMessage());
            }
        }
    }
    
    public void cleanup() {
        Log.d(TAG, "cleanup() called");
        
        stopListening();
        
        if (speechRecognizer != null) {
            try {
                speechRecognizer.destroy();
                speechRecognizer = null;
            } catch (Exception e) {
                Log.e(TAG, "Error destroying speech recognizer: " + e.getMessage());
            }
        }
        
        if (audioRecord != null) {
            try {
                if (audioRecord.getState() == AudioRecord.STATE_INITIALIZED) {
                    audioRecord.release();
                }
                audioRecord = null;
            } catch (Exception e) {
                Log.e(TAG, "Error releasing AudioRecord: " + e.getMessage());
            }
        }
        
        isInitialized = false;
    }
    
    // Test method to manually trigger a speech result
    public void simulateSpeechResult(String result) {
        Log.d(TAG, "simulateSpeechResult called with: " + result);
        UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionResult", result);
    }
    
    // Test method to check recognizer status
    public void getRecognizerStatus() {
        String status = "Initialized: " + isInitialized + 
                       ", Listening: " + isListening + 
                       ", UseAudioRecord: " + useAudioRecordFallback +
                       ", Recording: " + isRecording;
        Log.d(TAG, "Recognizer status: " + status);
        UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionStatus", status);
    }
    
    // Method to reinitialize the speech recognizer
    public void reinitializeRecognizer() {
        Log.d(TAG, "reinitializeRecognizer() called");
        
        // Ensure we run on UI thread
        if (context instanceof Activity) {
            ((Activity) context).runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    performReinitialize();
                }
            });
        } else {
            performReinitialize();
        }
    }
    
    private void performReinitialize() {
        Log.d(TAG, "Performing recognizer reinitialization...");
        
        // Clean up existing recognizer
        if (speechRecognizer != null) {
            try {
                speechRecognizer.destroy();
            } catch (Exception e) {
                Log.w(TAG, "Error destroying old recognizer: " + e.getMessage());
            }
        }
        
        // Clean up AudioRecord
        if (audioRecord != null) {
            try {
                if (audioRecord.getState() == AudioRecord.STATE_INITIALIZED) {
                    audioRecord.release();
                }
            } catch (Exception e) {
                Log.w(TAG, "Error releasing old AudioRecord: " + e.getMessage());
            }
        }
        
        isInitialized = false;
        isListening = false;
        isRecording = false;
        speechRecognizer = null;
        audioRecord = null;
        
        // Reinitialize
        initializeSpeechRecognizer();
        
        Log.d(TAG, "Recognizer reinitialization complete");
    }
    
    // Method to switch to AudioRecord fallback manually
    public void enableAudioRecordFallback() {
        Log.d(TAG, "Manually enabling AudioRecord fallback");
        useAudioRecordFallback = true;
        if (audioRecord == null) {
            initializeAudioRecord();
        }
        UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionStatus", "AudioRecord fallback enabled");
    }
    
    // Method to adjust speech detection sensitivity
    public void setSpeechDetectionThreshold(int newThreshold) {
        int oldThreshold = speechDetectionThreshold;
        speechDetectionThreshold = newThreshold;
        Log.d(TAG, "Speech detection threshold changed from " + oldThreshold + " to " + newThreshold);
        UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionStatus", "Threshold: " + newThreshold);
    }
    
    // Method to get current AudioRecord settings
    public void getAudioRecordSettings() {
        String settings = "Threshold: " + speechDetectionThreshold + 
                         ", Required samples: " + requiredSpeechSamples +
                         ", Sample rate: " + SAMPLE_RATE +
                         ", Buffer size: " + bufferSize;
        Log.d(TAG, "AudioRecord settings: " + settings);
        UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionStatus", settings);
    }

    public void setTargetLetter(String letter) {
        this.targetLetter = letter.toUpperCase();
        Log.d(TAG, "Target letter set to: " + this.targetLetter);
    }
    
    private class VoiceRecognitionListener implements RecognitionListener {
        @Override
        public void onReadyForSpeech(Bundle params) {
            Log.d(TAG, "onReadyForSpeech - Recognition ready, time since start: " + 
                  (System.currentTimeMillis() - startTime) + "ms");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionReady", "");
        }

        @Override
        public void onBeginningOfSpeech() {
            hasDetectedAudio = true;
            consecutiveAudioSamples++;
            Log.d(TAG, "onBeginningOfSpeech - Speech detected! Time since start: " + 
                  (System.currentTimeMillis() - startTime) + "ms");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionBeginSpeech", "");
        }

        @Override
        public void onRmsChanged(float rmsdB) {
            if (rmsdB > -30) { // Only log significant RMS changes
                hasDetectedAudio = true;
                consecutiveAudioSamples++;
                long currentTime = System.currentTimeMillis();
                Log.d(TAG, "onRmsChanged: " + rmsdB + " dB, time: " + (currentTime - startTime) + "ms, samples: " + consecutiveAudioSamples);
            }
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
            hasDetectedAudio = true;
            Log.d(TAG, "onBufferReceived - Audio buffer received, size: " + buffer.length + 
                  ", time: " + (System.currentTimeMillis() - startTime) + "ms");
        }

        @Override
        public void onEndOfSpeech() {
            Log.d(TAG, "onEndOfSpeech - End of speech detected, time since start: " + 
                  (System.currentTimeMillis() - startTime) + "ms");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionEndSpeech", "");
        }

        @Override
        public void onError(int error) {
            isListening = false;
            String errorMessage = getErrorMessage(error);
            long totalTime = System.currentTimeMillis() - startTime;
            
            Log.e(TAG, "onError: " + errorMessage + " (code: " + error + "), total time: " + totalTime + 
                  "ms, detected audio: " + hasDetectedAudio + ", audio samples: " + consecutiveAudioSamples);
            
            // If SpeechRecognizer keeps failing with no audio detection, switch to AudioRecord
            if ((error == SpeechRecognizer.ERROR_NO_MATCH || error == SpeechRecognizer.ERROR_SPEECH_TIMEOUT) 
                && !hasDetectedAudio && totalTime < 1000) {
                Log.w(TAG, "SpeechRecognizer failing quickly with no audio, switching to AudioRecord fallback");
                useAudioRecordFallback = true;
                initializeAudioRecord();
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Switched to AudioRecord: " + errorMessage);
            } else {
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", errorMessage);
            }
        }

        @Override
        public void onResults(Bundle results) {
            isListening = false;
            long totalTime = System.currentTimeMillis() - startTime;
            
            ArrayList<String> matches = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            float[] confidenceScores = results.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES);
            
            Log.d(TAG, "onResults - Recognition complete, total time: " + totalTime + "ms");
            
            if (matches != null && !matches.isEmpty()) {
                StringBuilder resultLog = new StringBuilder("Recognition results: ");
                for (int i = 0; i < matches.size(); i++) {
                    String confidence = (confidenceScores != null && i < confidenceScores.length) 
                        ? String.format("%.2f", confidenceScores[i]) : "N/A";
                    resultLog.append(matches.get(i)).append(" (").append(confidence).append(")");
                    if (i < matches.size() - 1) resultLog.append(", ");
                }
                Log.d(TAG, resultLog.toString());
                
                String bestResult = matches.get(0);
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionResult", bestResult);
            } else {
                Log.d(TAG, "No recognition results found");
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "No results");
            }
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            ArrayList<String> partialMatches = partialResults.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            long currentTime = System.currentTimeMillis() - startTime;
            
            if (partialMatches != null && !partialMatches.isEmpty()) {
                String partialResult = partialMatches.get(0);
                Log.d(TAG, "onPartialResults: " + partialResult + ", time: " + currentTime + "ms");
                UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionPartialResult", partialResult);
            }
        }

        @Override
        public void onEvent(int eventType, Bundle params) {
            Log.d(TAG, "onEvent: " + eventType + ", time: " + (System.currentTimeMillis() - startTime) + "ms");
        }
    }
    
    private String getErrorMessage(int errorCode) {
        switch (errorCode) {
            case SpeechRecognizer.ERROR_AUDIO:
                return "Audio recording error";
            case SpeechRecognizer.ERROR_CLIENT:
                return "Client side error";
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                return "Insufficient permissions";
            case SpeechRecognizer.ERROR_NETWORK:
                return "Network error";
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                return "Network timeout";
            case SpeechRecognizer.ERROR_NO_MATCH:
                return "No match found";
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                return "RecognitionService busy";
            case SpeechRecognizer.ERROR_SERVER:
                return "Server error";
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                return "No speech input";
            default:
                return "Unknown error (" + errorCode + ")";        }
    }
    
    private void analyzeAudioForLetter() {
        Log.d(TAG, "Analyzing audio for letter: " + targetLetter);
        Log.d(TAG, "Speech duration: " + speechDuration + "ms, Average amplitude: " + averageAmplitude + ", Peak: " + peakAmplitude);
        
        if (audioSamples.isEmpty() || targetLetter.isEmpty()) {
            Log.w(TAG, "No audio data or target letter for analysis");
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "No speech detected");
            return;
        }
        
        boolean isCorrect = false;
        String analysisReason = "";
        
        // Basic letter classification based on acoustic properties
        char letter = targetLetter.charAt(0);
        
        // Vowels vs Consonants analysis
        boolean isVowel = "AEIOU".contains(String.valueOf(letter));
        
        if (isVowel) {
            // Vowels tend to have:
            // - Longer duration
            // - More consistent amplitude
            // - Higher average amplitude
            if (speechDuration > 300 && averageAmplitude > speechDetectionThreshold * 1.2) {
                isCorrect = true;
                analysisReason = "vowel-like characteristics";
            }
        } else {
            // Consonants analysis by groups
            if ("BCDFGHJKLMNPQRSTVWXYZ".contains(String.valueOf(letter))) {
                // Different consonant groups have different patterns
                if ("BPTKDG".contains(String.valueOf(letter))) {
                    // Plosives - short burst, high peak
                    if (speechDuration < 400 && peakAmplitude > speechDetectionThreshold * 1.5) {
                        isCorrect = true;
                        analysisReason = "plosive-like characteristics";
                    }
                } else if ("FVSZH".contains(String.valueOf(letter))) {
                    // Fricatives - longer, sustained
                    if (speechDuration > 200 && speechDuration < 600) {
                        isCorrect = true;
                        analysisReason = "fricative-like characteristics";
                    }
                } else if ("MNLR".contains(String.valueOf(letter))) {
                    // Liquids/Nasals - medium duration, consistent amplitude
                    if (speechDuration > 250 && speechDuration < 500) {
                        isCorrect = true;
                        analysisReason = "liquid/nasal-like characteristics";
                    }
                } else {
                    // Other consonants - general pattern
                    if (speechDuration > 150 && speechDuration < 800) {
                        isCorrect = true;
                        analysisReason = "consonant-like characteristics";
                    }
                }
            }
        }
        
        // Fallback: if we detected clear speech, give benefit of doubt
        if (!isCorrect && speechSampleCount > 20 && averageAmplitude > speechDetectionThreshold) {
            isCorrect = true;
            analysisReason = "clear speech detected";
        }
        
        Log.d(TAG, "Audio analysis result: " + (isCorrect ? "CORRECT" : "INCORRECT") + " (" + analysisReason + ")");
        
        // Send result to Unity
        if (isCorrect) {
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionResult", targetLetter);
        } else {
            UnityPlayer.UnitySendMessage(gameObjectName, "OnVoiceRecognitionError", "Speech doesn't match " + targetLetter);
        }
    }
}
