package com.pocketree.app

import android.app.ProgressDialog.show
import android.content.Intent
import android.os.Bundle
import android.widget.Toast

import androidx.activity.enableEdgeToEdge
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

class LoginActivity: AppCompatActivity() {
    private val client= OkHttpClient()
    private lateinit var binding: ActivityLoginBinding

    override fun onCreate(savedInstanceState: Bundle?){
        super.onCreate(savedInstanceState)
        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        enableEdgeToEdge()
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        initButtons()
    }

    private fun initButtons(){
        binding.loginButton.setOnClickListener {
            val username = binding.username.text.toString()
            val password = binding.password.text.toString()

            if (username.isEmpty() || password.isEmpty()) {
                Toast.makeText(this, "Username and password cannot be empty", Toast.LENGTH_SHORT)
                    .show()
            }
            else {
                sendLoginRequest(username, password)
            }
        }

        binding.createAccountButton.setOnClickListener {
            val intent = Intent(this, CreateUserActivity::class.java)
            startActivity(intent)
        }
    }

//    private fun hashPassword(password: String): String {
//        val bytes = password.toByteArray()
//        val md = MessageDigest.getInstance("SHA-256")
//        val digest = md.digest(bytes)
//        return digest.fold("") { str, it -> str + "%02x".format(it) }
//    }

    private fun sendLoginRequest(username:String, password:String) {
        // hash the password before sending to backend for verification against database
      // val hashedPassword = hashPassword(password)

        // create request body
        val json = JSONObject().apply{
            put("username", username)
            put("password", password)
        }

        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        // build the request
        val request= okhttp3.Request.Builder()
            .url("http://10.0.2.2:5000/api/User/LoginApi")
            .post(body)
            .build()

        // execute asynchronously
        client.newCall(request).enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                // handle network failure ie no internet
                runOnUiThread {
                    Toast.makeText(this@LoginActivity,
                        "Network error",
                        Toast.LENGTH_LONG
                    ).show()
                }
            }

            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string()

                runOnUiThread {
                    // login successful
                    if (response.isSuccessful && !responseBody.isNullOrEmpty()) {
                        try {
                            // get information of user from database
                            val userData = Gson().fromJson(responseBody, User::class.java)

                            Toast.makeText(
                                this@LoginActivity,
                                "Welcome, ${userData.username}!",
                                Toast.LENGTH_SHORT
                            ).show()

                            val intent = Intent(this@LoginActivity, MainActivity::class.java).apply{
                                // pass data from Login to Main activity then pass to fragments
                                putExtra("USER_ID", userData.id)
                                putExtra("USERNAME", userData.username)
                                // pass whole object as a JSON string
                            }
                            startActivity(intent)
                            finish() // close LoginActivity so user can't go back
                        } catch (e: Exception) {
                            // handle server error
                            Toast.makeText(this@LoginActivity,
                                "Error in server response",
                                Toast.LENGTH_SHORT
                            ).show()
                        }
                    } else {
                        // user not authorised or not found
                        Toast.makeText(
                            this@LoginActivity,
                            "Invalid username or password, please try again",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }
            }
        });
    }
}
