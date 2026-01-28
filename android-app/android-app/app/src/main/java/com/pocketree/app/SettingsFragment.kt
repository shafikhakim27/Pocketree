package com.pocketree.app

import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.media.MediaPlayer
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import com.pocketree.app.databinding.FragmentSettingsBinding

class SettingsFragment: Fragment() {
    private var _binding: FragmentSettingsBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
    private var mediaPlayer: MediaPlayer? = null    // for background music
    private lateinit var prefs: SharedPreferences    // to save user settings

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentSettingsBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        prefs = requireActivity().getSharedPreferences("AppSettings", Context.MODE_PRIVATE)
        backgroundMusic()
        soundEffects()
        logOut()
    }

    private fun backgroundMusic() {
        // Load saved state (Default is true（on）)
        val isMusicOn = prefs.getBoolean("KEY_MUSIC_ON", true)
        binding.btnBackgroundSound.isChecked = isMusicOn

        // Initialize MediaPlayer if music is on
        if (isMusicOn) {
            playMusic()
        }

        // Listener for toggle changes
        binding.btnBackgroundSound.setOnCheckedChangeListener { _, isChecked ->
            // Save the new state
            prefs.edit().putBoolean("KEY_MUSIC_ON", isChecked).apply()

            if (isChecked) {
                playMusic()
                Toast.makeText(context, "Music On", Toast.LENGTH_SHORT).show()
            } else {
                stopMusic()
                Toast.makeText(context, "Music Off", Toast.LENGTH_SHORT).show()
            }
        }
    }
    // Helper function to play background music
    private fun playMusic() {
        if (mediaPlayer == null) {
            mediaPlayer = MediaPlayer.create(context, R.raw.bgm)
            mediaPlayer?.isLooping = true // Loop the music / 循环播放
        }
        mediaPlayer?.start()
    }

    // Helper function to stop background music
    private fun stopMusic() {
        mediaPlayer?.pause()
    }



    private fun soundEffects() {
        // Load saved state for SFX
        val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
        binding.btnSoundEffects.isChecked = isSfxOn

        binding.btnSoundEffects.setOnCheckedChangeListener { _, isChecked ->
            // Save state
            prefs.edit().putBoolean("KEY_SFX_ON", isChecked).apply()

            // Play a test sound if turned on (User Feedback)
            if (isChecked) {
                playSoundEffect()
            }
        }
    }
    // Helper function to play a sound effect
    private fun playSoundEffect() {
        // Create a temporary MediaPlayer for the click sound
        val sfxPlayer = MediaPlayer.create(context, R.raw.click_sound)
        sfxPlayer.setOnCompletionListener { mp -> mp.release() } // Release memory after playing
        sfxPlayer.start()
    }



    private fun logOut() {
        binding.logoutButton.setOnClickListener {
            // clear data in ViewModel
            sharedViewModel.logout()

            // 2. Navigate back to Login
            val loginIntent = Intent(requireContext(), LoginActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
                // clears app history so user can't press Back button to head back to Settings tab
            }
            startActivity(loginIntent) // start the login activity
            activity?.finish() // ensures the activity holding the fragment is closed

            Toast.makeText(
                context,
                "You have Logged out successfully",
                Toast.LENGTH_SHORT
            ).show()
        }
    }



    override fun onDestroyView(){
        super.onDestroyView()
        mediaPlayer?.release()
        mediaPlayer = null
        _binding = null
    }
}