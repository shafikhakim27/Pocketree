package com.pocketree.app

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.SharedPreferences
import android.media.MediaPlayer
import android.os.Bundle
import android.view.View
import android.view.ViewGroup
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.ViewModelProvider
import androidx.navigation.fragment.NavHostFragment
import androidx.navigation.ui.setupWithNavController
import com.pocketree.app.databinding.ActivityMainBinding

class MainActivity : AppCompatActivity() {
    private lateinit var viewModel: UserViewModel
    private lateinit var binding: ActivityMainBinding
    private var mediaPlayer: MediaPlayer? = null
    private lateinit var prefs: SharedPreferences

    private val logoutReceiver = object: BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            NetworkClient.setToken(this@MainActivity, null)

            // go to Login and clear backstack
            val loginIntent = Intent(this@MainActivity, LoginActivity::class.java)
            loginIntent.flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            startActivity(loginIntent)
            finish()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        // Force light mode - prevents dark mode from activating
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO)

        super.onCreate(savedInstanceState)
        prefs = getSharedPreferences("AppSettings", Context.MODE_PRIVATE)

        binding = ActivityMainBinding.inflate(layoutInflater)
        enableEdgeToEdge()
        setContentView(binding.root)

        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        viewModel = ViewModelProvider(this).get(UserViewModel::class.java)

        // pass viewModel to SignalRManager
        val token = NetworkClient.loadToken(this)
        if (!token.isNullOrEmpty()) {
            SignalRManager.init(token, viewModel)
        }

        if (prefs.getBoolean("KEY_MUSIC_ON", true)) {
            playMusic()
        }

        initUser()
        setupNavigation()
        observeViewModel()
    }

    // Helper function to play background music
    fun playMusic() {
        if (mediaPlayer == null) {
            mediaPlayer = MediaPlayer.create(this, R.raw.bgm)
            mediaPlayer?.isLooping = true   // Loop the music
        }
        if (mediaPlayer?.isPlaying == false) {
            mediaPlayer?.start()
            viewModel.isMusicPlaying.postValue(true)    // Update LiveData to inform all the observers
        }
    }

    // Helper function to stop background music
    fun stopMusic() {
        mediaPlayer?.pause()
        viewModel.isMusicPlaying.postValue(false)   // Update LiveData to inform all the observers
    }


//    private fun playSfx(resId: Int) {
//        try {
//            val fxPlayer = MediaPlayer.create(this, resId)
//            fxPlayer.setOnCompletionListener { mp ->
//                mp.release()
//            }
//            fxPlayer.start()
//        } catch (e: Exception) {
//            e.printStackTrace()
//        }
//    }

    private fun initUser(){
        val token = NetworkClient.loadToken(this)

        // check login status
        if (token.isNullOrEmpty() || token == "no_token") {
            val intent = Intent(this, LoginActivity::class.java)
            startActivity(intent)
            finish()
            return // stop execution here
        }

        val username = intent.getStringExtra("username")

        if (username != null) {
            // push data obtained from LoginActivity into ViewModel
            viewModel.updateUserData(
                username = username,
                totalCoins = intent.getIntExtra("totalCoins", 0),
                currentLevelId = intent.getIntExtra("currentLevelId", 1),
                levelName = intent.getStringExtra("levelName") ?: "Seedling",
                isWithered = intent.getBooleanExtra("isWithered", false),
                levelImageUrl = intent.getStringExtra("levelImageUrl"),
                profileImageUrl = intent.getStringExtra("profileImageUrl"),
                plantHealthPercent = intent.getIntExtra("plantHealthPercent", 100)
            )
        } else {
            // fallback - in case intent is empty (e.g. app was killed/restored)
            // fill UI with last known data so user is able to see something on screen
            viewModel.loadCachedData(this)

            // fetch fresh data from the server to ensure info is up to date
            viewModel.fetchUserProfile()
        }
    }

    private fun setupNavigation() {
        val navHostFragment = supportFragmentManager
            .findFragmentById(R.id.nav_host_fragment) as NavHostFragment
        val navController = navHostFragment.navController

        binding.bottomNav.setupWithNavController(navController)
        // links the bottom navigation clicks to the fragment swaps

        navController.addOnDestinationChangedListener { _, _, _ ->
            val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
            setSoundEffectsRecursive(binding.root, isSfxOn)
        }
    }

    fun observeViewModel() {
        viewModel.adminMessage.observe(this) { message ->
            message?.let {
                showAdminDialog(this, it)
                // Clear the message after showing so it doesn't pop up again on rotation
                viewModel.adminMessage.value = null
            }
        }
//        viewModel.playSoundEffectEvent.observe(this) { shouldPlay ->
//            if (shouldPlay == true) {
//                // check whether user has turned sound effects on
//                val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
//                if (isSfxOn) {
//                    playSfx(R.raw.click_sound) // helper function to play sound effects
//                }
//                viewModel.playSoundEffectEvent.value = false
//            }
//        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            val isSfxOn = prefs.getBoolean("KEY_SFX_ON", true)
            // check whether user has turned sound effects on
            binding.root.isSoundEffectsEnabled = isSfxOn
        }
    }

    private fun setSoundEffectsRecursive(view: View, enabled: Boolean) {
        view.isSoundEffectsEnabled = enabled
        if (view is ViewGroup) {
            for (i in 0 until view.childCount) {
                setSoundEffectsRecursive(view.getChildAt(i), enabled)
            }
        }
    }

    private fun showAdminDialog(activityContext: Context, message: String) {
        androidx.appcompat.app.AlertDialog.Builder(activityContext)
            .setTitle("Admin Message")
            .setMessage(message)
            .setPositiveButton("OK") {dialog, _ -> dialog.dismiss()}
            .setCancelable(false)
            .show()
    }

    override fun onStart() {
        super.onStart()
        registerReceiver(logoutReceiver,
            IntentFilter("ACTION_LOGOUT"),
            RECEIVER_NOT_EXPORTED
        )
    }

    override fun onStop() {
        super.onStop()
        unregisterReceiver(logoutReceiver)
        // When the app enters the background, pause the music if it is currently playing.
        if (mediaPlayer?.isPlaying == true) {
            mediaPlayer?.pause()    // temporary pause
        }
    }

    override fun onRestart() {
        super.onRestart()
        // When user returns to the app from the background.
        // Check user's preferences: if it was originally ON, resume playback.
        val isMusicOn = prefs.getBoolean("KEY_MUSIC_ON", true)
        if (isMusicOn && mediaPlayer?.isPlaying == false) {
            mediaPlayer?.start()
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        mediaPlayer?.stop()
        mediaPlayer?.release()
        mediaPlayer = null
    }
}