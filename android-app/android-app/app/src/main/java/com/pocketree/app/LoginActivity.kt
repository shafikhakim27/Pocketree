package com.pocketree.app

import android.app.ProgressDialog.show
import android.content.Intent
import android.os.Bundle
import android.widget.Toast

import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import com.google.gson.Gson
import com.pocketree.app.databinding.ActivityLoginBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.security.MessageDigest
import kotlin.jvm.java
import androidx.activity.viewModels

class LoginActivity: AppCompatActivity() {
    private lateinit var binding: ActivityLoginBinding
    private val client = NetworkClient.okHttpClient

    private val userViewModel: UserViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // initialise NetworkClient context
        NetworkClient.context = this.applicationContext

        // check for existing token
        val existingToken = NetworkClient.loadToken(this)
        if (existingToken != null) {
            startActivity(Intent(this, MainActivity::class.java))
            finish()
            return
        }

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        userViewModel.username.observe(this) { username ->
            if (!username.isNullOrEmpty()) {
                Toast.makeText(this, "Welcome, $username!", Toast.LENGTH_SHORT).show()
                startActivity(Intent(this, MainActivity::class.java))
                finish()
            }
        }

        enableEdgeToEdge()
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        initButtons()
    }

    private fun initButtons() {
        binding.loginButton.setOnClickListener {
            val username = binding.username.text.toString()
            val password = binding.password.text.toString()

            if (username.isEmpty() || password.isEmpty()) {
                Toast.makeText(this, "Username and password cannot be empty", Toast.LENGTH_SHORT)
                    .show()
            } else {
                val credentials = JSONObject().apply{
                    put("username", username)
                    put("password", password)
                }
                userViewModel.loginUser(credentials)
//                sendLoginRequest(username, password)
            }
        }

        binding.createAccountButton.setOnClickListener {
            val intent = Intent(this, CreateUserActivity::class.java)
            startActivity(intent)
        }
    }
}

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
//            .url("http://10.0.2.2:5042/api/User/LoginApi")
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
//                        // Inside your onResponse / runOnUiThread
//                        // Inside your onResponse runOnUiThread
//                        try {
//                            val loginData = Gson().fromJson(responseBody, LoginResponse::class.java)
//
//                            // Save the token - use a fallback if the API doesn't send one yet
//                            NetworkClient.setToken(this@LoginActivity, loginData.token ?: "no_token")
//
//                            Toast.makeText(
//                                this@LoginActivity,
//                                "Welcome, ${loginData.user.username}!", // This works because of your 'get() = this' hack
//                                Toast.LENGTH_SHORT
//                            ).show()
//
//                            val intent = Intent(this@LoginActivity, MainActivity::class.java).apply {
//                                putExtra("USERNAME", loginData.user.username)
//                            }
//                            startActivity(intent)
//                            finish()
//                        } catch (e: Exception) {
//                            android.util.Log.e("LOGIN_DEBUG", "Error: ${e.message}")
//                            Toast.makeText(this@LoginActivity, "Login processing failed", Toast.LENGTH_SHORT).show()
//                        }

////                        try {
////                            // get information of user from database
////                            val loginData = Gson().fromJson(responseBody, LoginResponse::class.java)
////
////                            NetworkClient.setToken(this@LoginActivity, loginData.token)
////
////                            Toast.makeText(
////                                this@LoginActivity,
////                                "Welcome, ${loginData.user.username}!",
////                                Toast.LENGTH_SHORT
////                            ).show()
////
////                            val intent = Intent(this@LoginActivity, MainActivity::class.java).apply{
////                                putExtra("USERNAME", loginData.user.username)
////                            }
////                            startActivity(intent)
////                            finish() // close LoginActivity so user can't go back
////                        } catch (e: Exception) {
////                            // handle server error
////                            Toast.makeText(this@LoginActivity,
////                                "Error in server response",
////                                Toast.LENGTH_SHORT
////                            ).show()
////                        }

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
