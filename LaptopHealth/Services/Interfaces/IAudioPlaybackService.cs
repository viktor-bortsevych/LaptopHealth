namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Service for audio playback and speaker testing
    /// </summary>
    public interface IAudioPlaybackService
    {
        /// <summary>
        /// Event raised when playback stops
        /// </summary>
        event EventHandler? PlaybackStopped;

        /// <summary>
        /// Gets a list of available audio output devices
        /// </summary>
        IEnumerable<string> GetOutputDevices();

        /// <summary>
        /// Selects an output device for playback
        /// </summary>
        bool SelectOutputDevice(string deviceName);

        /// <summary>
        /// Plays test audio with the specified type
        /// </summary>
        /// <param name="audioType">Type of test audio (sine440, sine880, whitenoise, pinknoise)</param>
        bool PlayTestAudio(string audioType);

        /// <summary>
        /// Plays an audio file from the specified path
        /// </summary>
        /// <param name="filePath">Full path to the audio file</param>
        bool PlayAudioFile(string filePath);

        /// <summary>
        /// Stops audio playback
        /// </summary>
        void StopPlayback();

        /// <summary>
        /// Sets the stereo balance
        /// </summary>
        /// <param name="balance">Balance value from -1.0 (left) to 1.0 (right), 0 is center</param>
        void SetStereoBalance(float balance);

        /// <summary>
        /// Gets whether audio is currently playing
        /// </summary>
        bool IsPlaying { get; }
    }
}
