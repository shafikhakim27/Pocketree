package com.pocketree.app

import android.R.attr.data
import android.R.attr.password
import android.R.attr.text
import android.content.Intent
import android.os.Bundle
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

//    private val sharedViewModel: UserViewModel by viewModels()
    // activityViewModels() is used by Fragments to share a viewModel with parent Activity

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)

        // initialise NetworkClient context
        NetworkClient.context = this.applicationContext

        // check for existing token
        val existingToken = NetworkClient.loadToken(this)
        if (!existingToken.isNullOrEmpty() && existingToken != "no_token") {
            startActivity(Intent(this, MainActivity::class.java))
            finish()
            return
        }

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        setupValidation()
        initButtons()
        // observeViewModel()
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
                sendLoginRequest(credentials)
                // sendLoginRequest(username, password)
            }
            if (hasError) return@setOnClickListener
        }

        binding.createAccountButton.setOnClickListener {
            val intent = Intent(this, CreateUserActivity::class.java)
            startActivity(intent)
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
                    Toast.makeText(this@LoginActivity, "Connection failed", Toast.LENGTH_SHORT).show()
                }
            }

            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string()

                if (response.isSuccessful && !responseBody.isNullOrEmpty()) {
                    try {
                        // get information of user from database
                        val loginData = gson.fromJson(responseBody, LoginResponse::class.java)

                        // save the token to the Singleton so the Interceptor can find it
                        NetworkClient.setToken(NetworkClient.context, loginData.token ?: "")

                        fetchUserProfile()
                    } catch (e: Exception) {
                        runOnUiThread {
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

    private fun fetchUserProfile() {
        val token = NetworkClient.loadToken(this)

        val request = Request.Builder()
            .url("$baseUrl/GetUserProfileApi")
            .addHeader("Authorization", "Bearer $token")
            .get()
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                Toast.makeText(this@LoginActivity,
                    "Network error",
                    Toast.LENGTH_SHORT
                ).show()
            }

            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string()
                if (response.isSuccessful && responseBody != null) {
                    try {
                        // Map the response to your User data class
                        val userProfile = gson.fromJson(responseBody, User::class.java)
                        runOnUiThread {
                            loginUser(userProfile)
                        }
                    } catch (e:Exception) {
                        runOnUiThread {
                            Toast.makeText(this@LoginActivity,
                                "Error in server response",
                                Toast.LENGTH_SHORT
                            ).show()
                        }
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
//        Toast.makeText(this@LoginActivity,
//            "Welcome ${user.username}!",
//            Toast.LENGTH_SHORT
//        ).show()
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

//    private fun observeViewModel() {
//        sharedViewModel.username.observe(this) { username ->
//            if (!username.isNullOrEmpty()) {
//                Toast.makeText(this, "Welcome, $username!", Toast.LENGTH_SHORT).show()
//                startActivity(Intent(this, MainActivity::class.java))
//                finish()
//            }
//        }
//    }

//    private fun sendLoginRequest(username:String, password:String) {
//        // create request body
//        val json = JSONObject().apply{
//            put("username", username)
//            put("password", password)
//        }
//
//        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())
//
//        // build the request
//        val request= okhttp3.Request.Builder()
//            .url("http://10.0.2.2:5000/api/User/LoginApi")
//            .post(body)
//            .build()
//
//        // execute asynchronously
//        client.newCall(request).enqueue(object : Callback {
//            override fun onFailure(call: Call, e: IOException) {
//                // handle network failure ie no internet
//                runOnUiThread {
//                    Toast.makeText(this@LoginActivity,
//                        "Network error",
//                        Toast.LENGTH_LONG
//                    ).show()
//                }
//            }
//
//            override fun onResponse(call: Call, response: Response) {
//                val responseBody = response.body?.string()
//
//                runOnUiThread {
//                    // login successful
//                    if (response.isSuccessful && !responseBody.isNullOrEmpty()) {
//                        try {
//                            // get information of user from database
//                            val loginData = Gson().fromJson(responseBody, LoginResponse::class.java)
//
//                            NetworkClient.setToken(this@LoginActivity, loginData.token)
//
//                            Toast.makeText(
//                                this@LoginActivity,
//                                "Welcome, ${loginData.user.username}!",
//                                Toast.LENGTH_SHORT
//                            ).show()
//
//                            val intent = Intent(this@LoginActivity, MainActivity::class.java).apply{
//                                putExtra("USERNAME", loginData.user.username)
//                            }
//                            startActivity(intent)
//                            finish() // close LoginActivity so user can't go back
//                        } catch (e: Exception) {
//                            // handle server error
//                            Toast.makeText(this@LoginActivity,
//                                "Error in server response",
//                                Toast.LENGTH_SHORT
//                            ).show()
//                        }
//                    } else {
//                        // user not authorised or not found
//                        Toast.makeText(
//                            this@LoginActivity,
//                            "Invalid username or password, please try again",
//                            Toast.LENGTH_SHORT
//                        ).show()
//                    }
//                }
//            }
//        });
//    }


// add logic later on for wrong password to show error message instead of toast also