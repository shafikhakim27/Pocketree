package com.pocketree.app

import android.app.AlertDialog
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
import com.google.android.material.textfield.TextInputEditText
import com.pocketree.app.databinding.FragmentSettingsBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

class SettingsFragment: Fragment() {
    private var _binding: FragmentSettingsBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
    private var mediaPlayer: MediaPlayer? = null    // for background music
    private lateinit var prefs: SharedPreferences    // to save user settings
    private val baseUrl = "http://10.0.2.2:5042/api/User"
    private val client = NetworkClient.okHttpClient

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
        changePassword()
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
//                Toast.makeText(context, "Music On", Toast.LENGTH_SHORT).show()
            } else {
                stopMusic()
//                Toast.makeText(context, "Music Off", Toast.LENGTH_SHORT).show()
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
        binding.btnLogout.setOnClickListener {
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
                "You have logged out successfully",
                Toast.LENGTH_SHORT
            ).show()
        }
    }


    // --- Change Password Logic  ---
    private fun changePassword() {
        binding.btnChangePassword.setOnClickListener {
            showChangePasswordDialog()
        }
    }

    private fun showChangePasswordDialog() {
        val dialogView = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_change_password, null)
        val etCurrent = dialogView.findViewById<TextInputEditText>(R.id.etCurrentPassword)
        val etNew = dialogView.findViewById<TextInputEditText>(R.id.etNewPassword)
        val etConfirm = dialogView.findViewById<TextInputEditText>(R.id.etConfirmPassword)

        val dialog = AlertDialog.Builder(requireContext())
            .setView(dialogView)
            .setTitle("Change Password")
            .setPositiveButton("Update", null) // Set null here to override later
            .setNegativeButton("Cancel", null)
            .create()

        dialog.show()

        // Override the positive button onClick handler to prevent auto-dismiss
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
            val currentPass = etCurrent.text.toString()
            val newPass = etNew.text.toString()
            val confirmPass = etConfirm.text.toString()

            if (validatePasswordInput(currentPass, newPass, confirmPass)) {
                sendChangePasswordRequest(currentPass, newPass, confirmPass, dialog)
            }
        }
    }

    private fun validatePasswordInput(current: String, new: String, confirm: String): Boolean {
        if (current.isEmpty() || new.isEmpty() || confirm.isEmpty()) {
            Toast.makeText(context, "All fields are required", Toast.LENGTH_SHORT).show()
            return false
        }
        if (new.length < 8) {
            Toast.makeText(context, "New password must be at least 8 characters", Toast.LENGTH_SHORT).show()
            return false
        }
        if (new != confirm) {
            Toast.makeText(context, "New passwords do not match", Toast.LENGTH_SHORT).show()
            return false
        }
        return true
    }

    private fun sendChangePasswordRequest(current: String, new: String, confirm: String, dialog: AlertDialog) {
        // 1. Get Token
        val token = NetworkClient.loadToken(requireContext())
        android.util.Log.d("DEBUG_TOKEN", "Token is: $token") // 查看 Logcat
        android.util.Log.e("DEBUG_PASSWORD", "Token sending: '$token'")
        if (token.isNullOrEmpty() || token == "no_token") {
            Toast.makeText(context, "Authentication error. Please login again.", Toast.LENGTH_SHORT).show()
            return
        }

        // 2. Prepare JSON data
        val jsonObject = JSONObject().apply {
            put("CurrentPassword", current)
            put("NewPassword", new)
            put("ConfirmNewPassword", confirm)
        }

        val body = jsonObject.toString()
            .toRequestBody("application/json; charset=utf-8".toMediaType())

        // 3. Build Request
        val request = Request.Builder()
            .url("$baseUrl/change-password")
            //.addHeader("Authorization", "Bearer $token")
            .post(body)
            .build()

        // 4. Send Request
        client.newCall(request).enqueue(object : Callback {
            // Case 1: Network Failure (Server down, no wifi, etc.)
            override fun onFailure(call: Call, e: IOException) {
                activity?.runOnUiThread {
                    Toast.makeText(context, "Cannot connect to backend: ${e.message}", Toast.LENGTH_SHORT).show()
                }
            }

            override fun onResponse(call: Call, response: Response) {
                // Note: response.body?.string() can only be called once
                val responseBody = response.body?.string() ?: ""

                activity?.runOnUiThread {
                    if (response.isSuccessful) {
                        Toast.makeText(context, "Password updated successfully!", Toast.LENGTH_LONG).show()
                        dialog.dismiss() // Close dialog only on success
                    } else {
                        // Error Handling based on HTTP Status Code
                        when (response.code) {
                            400 -> {
                                // backend returns 400 Bad Request (Business Logic Error)
                                if (responseBody.contains("incorrect", ignoreCase = true)) {
                                    Toast.makeText(context, "Wrong current password", Toast.LENGTH_SHORT).show()
                                } else if (responseBody.contains("match", ignoreCase = true)) {
                                    Toast.makeText(context, "New passwords do not match", Toast.LENGTH_SHORT).show()
                                } else {
                                    // Other validation errors
                                    Toast.makeText(context, "Validation failed: $responseBody", Toast.LENGTH_SHORT).show()
                                }
                            }
                            401 -> {
                                // 401 Unauthorized (Token expired or invalid)
                                Toast.makeText(context, "Session expired, please login again", Toast.LENGTH_SHORT).show()
                            }
                            500 -> {
                                // 500 Internal Server Error
                                Toast.makeText(context, "Server error, please try again later", Toast.LENGTH_SHORT).show()
                            }
                            else -> {
                                // Other errors
                                Toast.makeText(context, "Error ${response.code}: $responseBody", Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                }
            }
        })
    }


    override fun onDestroyView(){
        super.onDestroyView()
        mediaPlayer?.release()
        mediaPlayer = null
        _binding = null
    }

}