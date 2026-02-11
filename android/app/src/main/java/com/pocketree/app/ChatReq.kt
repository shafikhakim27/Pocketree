package com.pocketree.app

// this data class is to send chatbot info to ML
data class ChatReq (
    val user_id: String,
    val message: String
)

