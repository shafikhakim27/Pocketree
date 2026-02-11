package com.pocketree.app


// this class is required for recycler view
// helps app decide if chat bubble is on left or right, and in what colour
// whether the one talking is bot or person
data class ChatMessage (
    val text: String,
    val isUser: Boolean // true if User and false if AI
)
