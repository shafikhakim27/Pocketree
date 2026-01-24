package com.pocketree.app

data class User (
    val id: Int,
    val username: String,
    val password: String,
    val totalCoins: Int,
    val currentLevelId: Int,
    val lastLoginDate: String, //Gson unable to parse LocalDateTime
    val tasks: List<Task>
)
