package com.pocketree.app

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Bundle
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

        initUser()
        setupNavigation()
        observeViewModel()
    }

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
    }

    fun observeViewModel() {
        viewModel.adminMessage.observe(this) { message ->
            message?.let {
                showAdminDialog(this, it)
                // Clear the message after showing so it doesn't pop up again on rotation
                viewModel.adminMessage.value = null
            }
        }
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
    }

    private fun showAdminDialog(activityContext: Context, message: String) {
        androidx.appcompat.app.AlertDialog.Builder(activityContext)
            .setTitle("Admin Message")
            .setMessage(message)
            .setPositiveButton("OK") {dialog, _ -> dialog.dismiss()}
            .setCancelable(false)
            .show()
    }
}