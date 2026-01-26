package com.pocketree.app

data class User (
    val id: Int,
    val username: String,
    val password: String,
    val totalCoins: Int,
    val currentLevelId: Int,
    val levelName: String,
    val levelImageUrl: String,
    val isWithered: Boolean,
    val lastLoginDate: String, //Gson unable to parse LocalDateTime
    val tasks: List<Task>,
    val email: String,
)
