package com.pocketree.app

data class LoginResponse (
    val token: String, // needed for interceptor to work
    val user: User
)