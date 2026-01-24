package com.pocketree.app

import android.R.attr.password
import android.content.Intent
import android.os.Bundle
import android.text.Editable
import android.text.TextWatcher
import android.widget.Toast
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import com.google.gson.Gson
import com.pocketree.app.databinding.ActivityCreateUserBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.security.MessageDigest

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
            val password = binding.password.text.toString()
            val confirmPassword = binding.confirmPassword.text.toString()

            // reset errors in all fields if any
            binding.usernameLayout.error = null
            binding.passwordLayout.error = null
            binding.confirmPasswordLayout.error = null

            var hasError = false

            // check for empty fields
            if (username.isEmpty()){
                binding.usernameLayout.error = "Username is required"
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
            performsSignup(username, password)
        }
    }

    // to clear off error message when user re-types into the text fields
    private fun setupValidation(){
        binding.username.addTextChangedListener(object: TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start:Int, count:Int, after:Int){
                // do nothing
            }
            override fun onTextChanged(s: CharSequence?, start:Int, before:Int, count:Int){
                binding.usernameLayout.error = null // clear error
            }
            override fun afterTextChanged(s: Editable?) {
                // do nothing
            }
        })
    }

//    private fun hashPassword(password: String): String {
//        val bytes = password.toByteArray()
//        val md = MessageDigest.getInstance("SHA-256")
//        val digest = md.digest(bytes)
//        return digest.fold("") { str, it -> str + "%02x".format(it) }
//    }

    private fun performsSignup(username: String, password: String) {
//        val hashedPassword = hashPassword(password)

        // create a JSON object
        val json = JSONObject().apply{
            put("username", username)
            put("password", password)
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
                runOnUiThread {
                    if (response.isSuccessful) {
                        Toast.makeText(
                            this@CreateUserActivity,
                            "Account created successfully!",
                            Toast.LENGTH_SHORT
                        ).show()
                        finish() // to redirect to login screen
                    } else {
                        binding.usernameLayout.error = responseText ?: "Registration failed"
                    }
                }
            }
        })
    }
}