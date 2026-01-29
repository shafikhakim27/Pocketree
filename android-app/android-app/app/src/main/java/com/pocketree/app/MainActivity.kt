package com.pocketree.app

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Bundle
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.ViewModelProvider
import androidx.navigation.fragment.NavHostFragment
import androidx.navigation.ui.setupWithNavController
import com.pocketree.app.databinding.ActivityMainBinding

class MainActivity : AppCompatActivity() {
    private lateinit var viewModel: UserViewModel
    private lateinit var binding: ActivityMainBinding

    override fun onCreate(savedInstanceState: Bundle?) {
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
        initUser()
        setupNavigation()
    }

    private fun initUser(){
        // initialise NetworkClient context (safety net)
        NetworkClient.context = this.applicationContext
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
            // manually push intent data into ViewModel
            viewModel.updateUserData(
                username = username,
                totalCoins = intent.getIntExtra("totalCoins", 0),
                currentLevelId = intent.getIntExtra("currentLevelId", 1),
                levelName = intent.getStringExtra("levelName") ?: "Seedling",
                isWithered = intent.getBooleanExtra("isWithered", false),
                levelImageUrl = intent.getStringExtra("levelImageUrl")
            )
        } else {
            // fallback - in case intent is empty (e.g. app was killed/restored),
            // fetch fresh data from the server using the token
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

    override fun onStart(){
        super.onStart()
        registerReceiver(logoutReceiver, IntentFilter("ACTION_LOGOUT"), RECEIVER_NOT_EXPORTED)
    }

    override fun onStop() {
        super.onStop()
        unregisterReceiver(logoutReceiver)
    }
}