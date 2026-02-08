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
import androidx.core.widget.doAfterTextChanged
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import com.bumptech.glide.Glide
import com.google.android.material.textfield.TextInputEditText
import com.google.android.material.textfield.TextInputLayout
import com.pocketree.app.databinding.FragmentSettingsBinding

// written by Haoting, edited by Shirley
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

        // set up observers first
        observeViewModel()

        // set up UI features
        backgroundMusic()
        soundEffects()
        changePassword()
        observeViewModel()
        logOut()
    }

    private fun observeViewModel(){
        // observe consolidated userState
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            // update UI using properties of state object
            binding.accountInfo.text = state.username
            binding.coinDisplay.text="${state.totalCoins} coins"

            Glide.with(requireContext())
                .load(state.profileImageUrl.ifEmpty{null}) // converts "" to null
                .circleCrop() // to make image round
                .placeholder(R.drawable.profile_pic)
                .into(binding.profilePic)
        }

        // listen for background music state
        sharedViewModel.isMusicPlaying.observe(viewLifecycleOwner) { isPlaying ->
            if (binding.btnBackgroundSound.isChecked != isPlaying) {
                binding.btnBackgroundSound.isChecked = isPlaying
            }
        }

        // listen for any error messages and show a Toast
        sharedViewModel.errorMessage.observe(viewLifecycleOwner) { msg ->
            msg?.let{
                Toast.makeText(requireContext(), it,
                    Toast.LENGTH_SHORT
                ).show()
                sharedViewModel.errorMessage.value = null // clear error after showing
            }
        }

        // listen for logout success
        sharedViewModel.logoutSuccess.observe(viewLifecycleOwner) {success ->
            if (success) {
                navigateToLogin()
                Toast.makeText(requireContext(),
                    "You have logged out successfully!",
                    Toast.LENGTH_SHORT
                ).show()
            }
        }

        // listen for password success
        sharedViewModel.passwordUpdateSuccess.observe(viewLifecycleOwner) { success ->
            if (success) {
                Toast.makeText(
                    context,
                    "Password updated successfully!",
                    Toast.LENGTH_SHORT
                ).show()
                sharedViewModel.passwordUpdateSuccess.value = null
            }
        }
    }

    private fun backgroundMusic() {
        // Load saved state (Default is true（on）)
        val isMusicOn = prefs.getBoolean("KEY_MUSIC_ON", true)
        binding.btnBackgroundSound.isChecked = isMusicOn

        binding.btnBackgroundSound.setOnCheckedChangeListener { _, isChecked ->
            prefs.edit().putBoolean("KEY_MUSIC_ON", isChecked).apply()

            // fetch mainActivity and use its methods to control music
            val mainActivity = activity as? MainActivity
            if (isChecked) mainActivity?.playMusic() else mainActivity?.stopMusic()
        }
    }

    private fun soundEffects() {
        // Load saved state for SFX
        val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
        binding.btnSoundEffects.isChecked = isSfxOn

        binding.btnSoundEffects.setOnCheckedChangeListener { _, isChecked ->
            // store status in SharedPreferences
            prefs.edit().putBoolean("KEY_SFX_ON", isChecked).apply()
            applySoundSettingToAllViews(binding.root, isChecked)

            // update MediaPlayer status
            view?.isSoundEffectsEnabled = isChecked

            // show Toast to inform user Sound Effects status
            if (isChecked) {
                Toast.makeText(requireContext(), "Sound Effects Enabled", Toast.LENGTH_SHORT).show()
            }
            else {
                Toast.makeText(requireContext(), "Sound Effects Disabled", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun applySoundSettingToAllViews(view: View, enabled: Boolean) {
        view.isSoundEffectsEnabled = enabled
        if (view is ViewGroup) {
            for (i in 0 until view.childCount) {
                applySoundSettingToAllViews(view.getChildAt(i), enabled)
            }
        }
    }

//    private fun soundEffects() {
//        // Load saved state for SFX
//        val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
//        binding.btnSoundEffects.isChecked = isSfxOn
//
//        binding.btnSoundEffects.setOnCheckedChangeListener { _, isChecked ->
//            // Save state
//            prefs.edit().putBoolean("KEY_SFX_ON", isChecked).apply()
//            // Play a test sound if turned on (User Feedback)
//            if (isChecked) {
//                sharedViewModel.triggerSoundEffect()
//            }
//        }
//    }

    private fun logOut() {
        binding.btnLogout.setOnClickListener {
            SignalRManager.stopConnection()
            (activity as? MainActivity)?.stopMusic()
            // clear data in ViewModel
            SignalRManager.stopConnection()
            navigateToLogin() // navigate to login immediately
            sharedViewModel.logout()
        }
    }

    private fun navigateToLogin() {
        val loginIntent = Intent(requireContext(), LoginActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            // clears app history so user can't press Back button to head back to Settings tab
        }
        startActivity(loginIntent) // start the login activity
        activity?.finish() // ensures the activity holding the fragment is closed
    }

    // change password
    private fun changePassword() {
        binding.btnChangePassword.setOnClickListener{
            showChangePasswordDialog()
        }
    }

    private fun showChangePasswordDialog(){
        val dialogView = LayoutInflater.from(requireContext()).inflate(
            R.layout.dialog_change_password, null)

        val layoutCurrent = dialogView.findViewById<TextInputLayout>(R.id.tilCurrentPassword)
        val layoutNew = dialogView.findViewById<TextInputLayout>(R.id.tilNewPassword)
        val layoutConfirm = dialogView.findViewById<TextInputLayout>(R.id.tilConfirmPassword)

        val etCurrent = dialogView.findViewById<TextInputEditText>(R.id.etCurrentPassword)
        val etNew = dialogView.findViewById<TextInputEditText>(R.id.etNewPassword)
        val etConfirm = dialogView.findViewById<TextInputEditText>(R.id.etConfirmPassword)

        // real time validation within dialog context
        etCurrent.doAfterTextChanged { layoutCurrent.error = null }
        etNew.doAfterTextChanged { layoutNew.error = null }
        etConfirm.doAfterTextChanged { layoutConfirm.error = null }

        val dialog = AlertDialog.Builder(requireContext())
            .setView(dialogView)
            .setTitle("Change Password")
            .setPositiveButton("Update", null) // set null here to override later
            .setNegativeButton("Cancel", null)
            .create()

        dialog.show()

        // override the positive button onClick handler to prevent auto-dismiss
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
            val currentPass = etCurrent.text.toString()
            val newPass = etNew.text.toString()
            val confirmPass = etConfirm.text.toString()

            val isValid = validatePasswordInput(
                currentPass, newPass, confirmPass,
                layoutCurrent, layoutNew, layoutConfirm
            )

            if (isValid){
                sharedViewModel.sendPasswordChangeRequest(currentPass, newPass, confirmPass)
                dialog.dismiss() // close dialog only on success
            }
        }
    }

    private fun validatePasswordInput(
        current:String, new:String, confirm:String,
        layoutCurrent: TextInputLayout, layoutNew: TextInputLayout, layoutConfirm: TextInputLayout
    ):Boolean{
        var isValid = true

        if (current.isEmpty()) {
            layoutCurrent.error = "Current password required"
            isValid = false
        }

        if (new.isEmpty()) {
            layoutNew.error = "New password required"
            isValid = false
        }

        if (confirm.isEmpty()) {
            layoutConfirm.error = "Please confirm your password"
            isValid = false
        }

        if (new.length <8) {
            layoutNew.error = "Password must be at least 8 characters"
            isValid = false
        }

        if (new != confirm) {
            layoutConfirm.error = "Passwords do not match"
            isValid = false
        }
        // return true
        return isValid
    }

    override fun onDestroyView(){
        super.onDestroyView()
        mediaPlayer?.release()
        mediaPlayer = null
        _binding = null
    }
}
