# VR Letter Recognition with Voice Input

This Unity project implements a VR letter recognition system with integrated Android voice recognition. The system displays letters in a peripheral vision pattern and uses voice recognition to detect when users correctly identify the displayed letters.

## Features

### Voice Recognition
- **Background Recognition**: Uses Android's SpeechRecognizer API without popup dialogs
- **Continuous Listening**: Automatically restarts voice recognition after each result
- **Smart Matching**: Recognizes both direct letter names and phonetic variations
- **Real-time Feedback**: Shows immediate visual feedback for correct/incorrect answers
- **Performance Tracking**: Displays accuracy statistics in real-time

### VR Display
- **Stereo View**: Split-screen display for VR goggles
- **Gyroscope Support**: Head tracking using device gyroscope
- **Peripheral Letters**: Letters displayed at various eccentricities and meridians
- **Sequential Display**: Shows one letter at a time with configurable intervals
- **Fixation Point**: Central cross for eye focus

## Setup Instructions

### 1. Android Permissions
The system requires the following permissions (already configured in AndroidManifest.xml):
- `RECORD_AUDIO` - For voice recognition
- `INTERNET` - For Google's speech recognition service

### 2. Unity Components
Add the `SimpleVRGoggleDisplay` component to an empty GameObject in your scene. The system will automatically:
- Create cameras for stereo view
- Generate letter displays
- Initialize voice recognition
- Set up UI feedback elements

### 3. Configuration
Configure the system using the inspector:

#### Voice Recognition Settings
- `Enable Voice Recognition`: Toggle voice input on/off
- `Recognition Timeout`: Maximum time to wait for speech input
- `Confidence Threshold`: Minimum confidence for accepting results
- `Log All Results`: Whether to save all voice recognition attempts

#### VR Display Settings
- `Enable Stereo View`: Split screen for VR goggles
- `Eye Separation`: Distance between left/right eye views
- `Fixed Position`: Camera position in world space

#### Letter Display Settings
- `Display Interval`: Time each letter is shown
- `Interval Between Letters`: Pause between letter changes
- `Distance From Player`: How far letters appear from camera
- `Randomize Size`: Vary letter sizes randomly

## Usage

1. **Build and Deploy**: Build the project to Android device
2. **Grant Permissions**: Allow microphone access when prompted
3. **Start Recognition**: Voice recognition starts automatically
4. **View Letters**: Look at the displayed letters through VR goggles
5. **Speak Letters**: Say the letter name when it appears
6. **Monitor Progress**: Check accuracy statistics at the top of view

## Voice Recognition Details

### Supported Letter Names
The system recognizes multiple variations for each letter:
- Standard names: "A", "B", "C", etc.
- Phonetic names: "AY", "BEE", "SEE", etc.
- Spelled names: "EH", "EF", "ELL", etc.

### Error Handling
- Network errors: Retry with longer delay
- No match/timeout: Quick retry
- Audio errors: Display error message and retry
- Permission denied: Show error in UI

### Performance Optimization
- Continuous recognition without UI popups
- Automatic restart after errors
- Background processing for smooth VR experience
- Efficient phonetic matching algorithms

## File Locations

### Voice Recognition Logs
Logs are saved to: `Application.persistentDataPath/VoiceRecognition_YYYY-MM-DD.txt`

Example log format:
```
[14:30:25] Target: E | Recognized: 'E' | Correct: True | Accuracy: 15/20
[14:30:28] Target: F | Recognized: 'EFF' | Correct: True | Accuracy: 16/21
[14:30:31] Target: P | Recognized: 'B' | Correct: False | Accuracy: 16/22
```

## Troubleshooting

### Voice Recognition Not Working
1. Check microphone permissions
2. Ensure internet connectivity
3. Verify Android device supports speech recognition
4. Check Unity console for error messages

### No Letters Displayed
1. Verify `SimpleVRGoggleDisplay` component is active
2. Check that TextMeshPro is imported
3. Ensure cameras are properly configured
4. Verify letter generation in inspector

### Performance Issues
1. Reduce number of letters in rings
2. Increase display intervals
3. Disable performance stats display
4. Optimize VR settings for device

## Technical Architecture

### Android Integration
- `VoiceBridge.java`: Android plugin for speech recognition
- Uses `SpeechRecognizer` API without UI components
- Custom `RecognitionListener` for continuous operation

### Unity Components
- `AndroidVoiceInput.cs`: Main voice recognition controller
- `SimpleVRGoggleDisplay.cs`: VR display and letter management
- Automatic integration between voice input and display

### Performance Tracking
- Real-time accuracy calculation
- Detailed logging of all attempts
- Visual feedback for immediate response
- Statistics display for progress monitoring

## Future Enhancements

- Offline voice recognition support
- Customizable letter sets
- Advanced difficulty progression
- Multi-language support
- Cloud data synchronization
- Performance analytics dashboard

## Support

For issues or questions:
1. Check Unity console logs
2. Review Android logcat for native errors
3. Verify all permissions are granted
4. Test with different Android devices
