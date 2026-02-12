package com.pocketree.app

import com.google.gson.annotations.SerializedName

// this data class is to fetch chatbot response from ML model
data class ChatResponse (
    @SerializedName("response") val response: String
)
