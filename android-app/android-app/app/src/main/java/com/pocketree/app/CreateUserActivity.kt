package com.pocketree.app

import android.os.Bundle
import android.util.Patterns
import android.widget.Toast
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.widget.doAfterTextChanged
import com.pocketree.app.databinding.ActivityCreateUserBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import androidx.core.widget.doAfterTextChanged

class CreateUserActivity: AppCompatActivity() {
    private val client= OkHttpClient()
    private lateinit var binding: ActivityCreateUserBinding

    override fun onCreate(savedInstanceState: Bundle?){
        super.onCreate(savedInstanceState)
        binding = ActivityCreateUserBinding.inflate(layoutInflater)
        setContentView(binding.root)

        enableEdgeToEdge()
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        initButton()
        setupValidation()
    }

    private fun initButton() {
        binding.createButton.setOnClickListener {
            val username = binding.username.text.toString().trim()
            val email = binding.email.text.toString()
            val password = binding.password.text.toString()
            val confirmPassword = binding.confirmPassword.text.toString()

            // reset errors in all fields if any
            binding.usernameLayout.error = null
            binding.emailLayout.error = null
            binding.passwordLayout.error = null
            binding.confirmPasswordLayout.error = null

            var hasError = false

            // check for empty fields
            if (username.isEmpty()){
                binding.usernameLayout.error = "Username is required"
                hasError = true
            }

            if (email.isEmpty()){
                binding.emailLayout.error = "Email is required"
                hasError = true
            } else if (!Patterns.EMAIL_ADDRESS.matcher(email).matches()){
                // this checks for the @ and the domain (eg .com)
                binding.emailLayout.error = "Please enter a valid email address"
                hasError = true
            }

            if (password.isEmpty()) {
                binding.passwordLayout.error = "Password is required"
                hasError = true
            }

            if (password.length <8) {
                binding.passwordLayout.error = "Password must be at least 8 characters"
                hasError = true
            }

            if (confirmPassword.isEmpty()) {
                binding.confirmPasswordLayout.error = "Please type in your password again"
                hasError = true
            }

            // check if passwords match
            if (password != confirmPassword) {
                binding.confirmPasswordLayout.error = "Passwords do not match"
                hasError = true
            }

            // only proceed if there are no errors
            if (hasError) return@setOnClickListener
            // to exit the block and no network request is sent if something is wrong

            // if local checks pass, check server for username availability
            // if username is available, create user and persist in database
            performsSignup(username, email, password)
        }
    }

    // to clear off error message when user re-types into the text fields
    private fun setupValidation() {
        // for username
        binding.username.doAfterTextChanged { text ->
            if (text.isNullOrBlank()) {
                binding.usernameLayout.error = "Username is required"
            } else {
                binding.usernameLayout.error = null // clear error when user starts typing
            }
        }

        // for email
        binding.email.doAfterTextChanged { text ->
            val email = text.toString()
            if (email.isEmpty()) {
                binding.emailLayout.error = "Email is required"
            } else if (!Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
                binding.emailLayout.error = "Please enter a valid email address"
            } else {
                binding.emailLayout.error = null
            }
        }

        // for passwords
        binding.confirmPassword.doAfterTextChanged { text ->
            val pass = binding.password.text.toString()
            val confirmPass = text.toString()

            if (confirmPass != pass) {
                binding.confirmPasswordLayout.error = "Passwords do not match"
            } else {
                binding.confirmPasswordLayout.error = null
            }
        }
    }

    private fun performsSignup(username: String, email: String, password: String) {
        // create a JSON object
        val json = JSONObject().apply{
            put("Username", username)
            put("Email", email)
            put("Password", password)
        }

        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        // build the request
        val request= Request.Builder()
            .url("http://10.0.2.2:5000/api/User/RegisterApi")
            .post(body)
            .build()

        // execute the call
        client.newCall(request).enqueue(object: Callback {
            override fun onFailure(call: Call, e: IOException) {
                runOnUiThread {
                    Toast.makeText(
                        this@CreateUserActivity,
                        "Network error",
                        Toast.LENGTH_SHORT
                    ).show()
                }
            }
            override fun onResponse(call: Call, response: Response) {
                val responseText = response.body?.string()
                val responseCode = response.code // get code 400, 500, etc

                runOnUiThread {
                    if (response.isSuccessful) {
                        Toast.makeText(
                            this@CreateUserActivity,
                            "Account created successfully!",
                            Toast.LENGTH_SHORT
                        ).show()
                        finish() // to redirect to login screen
                    } else {
                        if (responseCode == 500) {
                            binding.usernameLayout.error = "Server database error."
                        } else {
                            binding.usernameLayout.error = responseText ?: "Registration failed"
                        }
                    }
                }
            }
        })
    }
}