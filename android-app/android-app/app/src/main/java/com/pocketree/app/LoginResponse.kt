package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class LoginResponse (
    @SerializedName("token") val token: String, // needed for interceptor to work
    @SerializedName("user") val user: User
)