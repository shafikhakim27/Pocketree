package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class LoginResponse (
    val token: String, // needed for interceptor to work
    val user: User
)

// bring back if needed
//@SerializedName("Token") val token: String?,
//@SerializedName("UserID") val userID: Int

    // fres version
//    val userID: Int,
//    val username: String,
//    val totalCoins: Int,
//    val levelName: String,
//    val tasks: List<Task>, // Links the tasks directly to the login
//    val token: String? = "no_token"
//    ) {
//    val user: LoginResponse get() = this
//    // so data.user.username can work for viewModel
//    }