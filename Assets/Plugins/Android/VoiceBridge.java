package com.unity3d.player;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.util.Log;
import java.util.ArrayList;
import java.util.Locale;
import android.Manifest;

public class VoiceBridge {
    
    private static final String TAG = "VoiceBridge";
    private SpeechRecognizer speechRecognizer;
    private Context context;
    private String gameObjectName;
    private boolean isListening = false;
    private boolean isInitialized = false;
    
    public VoiceBridge(Context context, String gameObjectName) {
        this.context = context;
        this.gameObjectName = gameObjectName;
        
        Log.d(TAG, "VoiceBridge constructor called with gameObject: " + gameObjectName);
        
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
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", "Microphone permission not granted");
        }
    }
    
    private boolean checkMicrophonePermission() {
        int permission = context.checkSelfPermission(Manifest.permission.RECORD_AUDIO);
        boolean hasPermission = permission == PackageManager.PERMISSION_GRANTED;
        Log.d(TAG, "Microphone permission check: " + hasPermission);
        return hasPermission;
    }
    
    private void initializeSpeechRecognizer() {
        if (SpeechRecognizer.isRecognitionAvailable(context)) {
            speechRecognizer = SpeechRecognizer.createSpeechRecognizer(context);
            speechRecognizer.setRecognitionListener(new CustomRecognitionListener());
            Log.d(TAG, "Speech recognizer initialized successfully");
            
            isInitialized = true;
            // Notify Unity that initialization was successful
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechInitialized", "Speech recognizer ready");
        } else {
            Log.e(TAG, "Speech recognition not available on this device");
            
            // Notify Unity that initialization failed
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", "Speech recognition not available on device");
        }
    }    public void startListening() {
        if (speechRecognizer != null && !isListening) {
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
        } else {
            Log.w(TAG, "Cannot start listening - speechRecognizer: " + speechRecognizer + ", isListening: " + isListening);
        }
    }
    
    private void performStartListening() {
        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 5);
        intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, context.getPackageName());
        
        // Important: Don't show UI - this prevents the popup
        intent.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, false);
        // Add partial results for better responsiveness
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
        
        try {
            speechRecognizer.startListening(intent);
            isListening = true;
            Log.d(TAG, "Started listening for speech (no UI)");
            
            // Send status to Unity
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechListeningStarted", "Listening started");
        } catch (Exception e) {
            Log.e(TAG, "Error starting speech recognition: " + e.getMessage());
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", "Failed to start listening: " + e.getMessage());
            isListening = false;
        }
    }
    
    public void stopListening() {
        if (speechRecognizer != null && isListening) {
            speechRecognizer.stopListening();
            isListening = false;
            Log.d(TAG, "Stopped listening for speech");
        }
    }
    
    public void destroy() {
        if (speechRecognizer != null) {
            speechRecognizer.destroy();
            speechRecognizer = null;
        }
    }
    
    private class CustomRecognitionListener implements RecognitionListener {
        @Override
        public void onReadyForSpeech(Bundle params) {
            Log.d(TAG, "Ready for speech");
        }

        @Override
        public void onBeginningOfSpeech() {
            Log.d(TAG, "Speech detected");
        }

        @Override
        public void onRmsChanged(float rmsdB) {
            // Audio level changed - can be used for visual feedback
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
            // Audio buffer received
        }

        @Override
        public void onEndOfSpeech() {
            Log.d(TAG, "End of speech");
            isListening = false;
        }

        @Override
        public void onError(int error) {
            String errorMessage = getErrorText(error);
            Log.e(TAG, "Speech recognition error: " + errorMessage);
            
            // Send error to Unity
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", errorMessage);
            isListening = false;
        }

        @Override
        public void onResults(Bundle results) {
            ArrayList<String> matches = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            if (matches != null && matches.size() > 0) {
                String recognizedText = matches.get(0);
                Log.d(TAG, "Speech recognition result: " + recognizedText);
                
                // Send result to Unity
                UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechResult", recognizedText);
            }
            isListening = false;
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            ArrayList<String> matches = partialResults.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            if (matches != null && matches.size() > 0) {
                String partialText = matches.get(0);
                Log.d(TAG, "Partial result: " + partialText);
                // Optionally send partial results to Unity for real-time feedback
            }
        }

        @Override
        public void onEvent(int eventType, Bundle params) {
            Log.d(TAG, "Speech recognition event: " + eventType);
        }
        
        private String getErrorText(int errorCode) {
            String message;
            switch (errorCode) {
                case SpeechRecognizer.ERROR_AUDIO:
                    message = "Audio recording error";
                    break;
                case SpeechRecognizer.ERROR_CLIENT:
                    message = "Client side error";
                    break;
                case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                    message = "Insufficient permissions";
                    break;
                case SpeechRecognizer.ERROR_NETWORK:
                    message = "Network error";
                    break;
                case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                    message = "Network timeout";
                    break;
                case SpeechRecognizer.ERROR_NO_MATCH:
                    message = "No match";
                    break;
                case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                    message = "RecognitionService busy";
                    break;
                case SpeechRecognizer.ERROR_SERVER:
                    message = "Error from server";
                    break;
                case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                    message = "No speech input";
                    break;
                default:
                    message = "Unknown error";
                    break;
            }
            return message;
        }
    }
}