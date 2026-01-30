package com.pocketree.app

import android.R.attr.data
import android.R.attr.password
import android.R.attr.text
import android.content.Intent
import android.os.Bundle
import android.util.Log.e
import android.util.Patterns
import android.widget.Toast
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat.startActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.widget.doAfterTextChanged
import com.pocketree.app.NetworkClient.gson
import com.pocketree.app.databinding.ActivityLoginBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import kotlin.jvm.java

class LoginActivity: AppCompatActivity() {
    private lateinit var binding: ActivityLoginBinding
    private val client = NetworkClient.okHttpClient
    private val baseUrl = "http://10.0.2.2:5042/api/User"
    private val gson = NetworkClient.gson

    override fun onCreate(savedInstanceState: Bundle?) {
        // initialise NetworkClient context
//        NetworkClient.context = this.applicationContext

        // check for existing token
        val existingToken = NetworkClient.loadToken(this)
        if (!existingToken.isNullOrEmpty() && existingToken != "no_token") {
            val intent = Intent(this, MainActivity::class.java)
            intent.flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            startActivity(intent)
            finish()
            return
        }

        enableEdgeToEdge()
        super.onCreate(savedInstanceState)

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        setupValidation()
        initButtons()
    }

    private fun initButtons() {
        binding.loginButton.setOnClickListener {
            val username = binding.username.text.toString()
            val password = binding.password.text.toString()

            binding.usernameLayout.error = null
            binding.passwordLayout.error = null

            var hasError = false

            if (username.isEmpty()) {
                binding.usernameLayout.error = "Username is required"
                hasError = true
            }
            if (password.isEmpty()) {
                binding.passwordLayout.error = "Password is required"
                hasError = true
            }
            if (!hasError) {
                val credentials = JSONObject().apply {
                    put("Username", username)
                    put("Password", password)
                }
                // 1. Show Loading and Disable Button
                setLoadingState(true)
                sendLoginRequest(credentials)
            }
            if (hasError) return@setOnClickListener
        }


        binding.createAccountButton.setOnClickListener {
            val intent = Intent(this, CreateUserActivity::class.java)
            startActivity(intent)
        }
    }
    // Helper function to handle UI state
    private fun setLoadingState(isLoading: Boolean) {
        if (isLoading) {
            binding.loadingProgressBar.visibility = android.view.View.VISIBLE
            binding.loginButton.isEnabled = false
            binding.createAccountButton.isEnabled = false // Also disable register
            binding.loginButton.text = "Logging in..."
        } else {
            binding.loadingProgressBar.visibility = android.view.View.GONE
            binding.loginButton.isEnabled = true
            binding.createAccountButton.isEnabled = true
            binding.loginButton.text = "Login"
        }
    }

    fun sendLoginRequest(credentials: JSONObject) {
        val body = credentials.toString()
            .toRequestBody("application/json; charset=utf-8".toMediaType())
        val request = Request.Builder()
            .url("$baseUrl/LoginApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                runOnUiThread {
                    // 2. Hide Loading on Failure
                    setLoadingState(false)
                    Toast.makeText(this@LoginActivity, "Connection failed", Toast.LENGTH_SHORT).show()
                }
            }

            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string()

                if (response.isSuccessful && !responseBody.isNullOrEmpty()) {
                    try {
                        // get information of user from database
                        val loginData = gson.fromJson(responseBody, LoginResponse::class.java)

                        // Check: Is token null?
                        if (loginData.token.isNullOrEmpty()) {
                            runOnUiThread {
                                Toast.makeText(this@LoginActivity, "Login failed: Token missing", Toast.LENGTH_SHORT).show()
                            }
                            return
                        }

                        // save the token to the Singleton so the Interceptor can find it
                        NetworkClient.setToken(applicationContext, loginData.token)
                        NetworkClient.saveUserCache(applicationContext, loginData.user)

                        runOnUiThread {
                            loginUser(loginData.user)
                        }
                    } catch (e: Exception) {
                        runOnUiThread {
                            setLoadingState(false) // Stop loading
                            Toast.makeText(
                                this@LoginActivity,
                                "Error in server response",
                                Toast.LENGTH_SHORT
                            ).show()
                        }
                    }
                } else {
                    runOnUiThread{
                        // user not authorised or not found
                        setLoadingState(false) // Stop loading
                        Toast.makeText(
                            this@LoginActivity,
                            "Invalid username or password, please try again",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }
            }
        })
    }

    private fun loginUser(user: User) {
        // After updating UI, proceed to MainActivity
        val intent = Intent(this@LoginActivity, MainActivity::class.java).apply{
            putExtra("username", user.username)
            putExtra("totalCoins", user.totalCoins)
            putExtra("currentLevelId", user.currentLevelId)
            putExtra("levelName", user.levelName)
            putExtra("isWithered", user.isWithered)
            putExtra("levelImageUrl", user.levelImageUrl)
        }
        Toast.makeText(this@LoginActivity,
            "Welcome ${user.username}!",
            Toast.LENGTH_SHORT
        ).show()
        startActivity(intent)
        finish() // close LoginActivity so user can't go back
    }

    private fun setupValidation() {
        binding.username.doAfterTextChanged { text ->
            if (text.isNullOrBlank()) {
                binding.usernameLayout.error = "Username is required"
            } else {
                binding.usernameLayout.error = null
            }
        }

        binding.password.doAfterTextChanged { text ->
            if (text.isNullOrBlank()) {
                binding.passwordLayout.error = "Password is required"
            } else {
                binding.passwordLayout.error = null
            }
        }
    }
}
