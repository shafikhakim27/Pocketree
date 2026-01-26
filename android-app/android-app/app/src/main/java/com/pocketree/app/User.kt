package com.pocketree.app

data class User (
    val id: Int,
    val username: String,
    val totalCoins: Int = 0,
    val currentLevelId: Int = 1,
    val levelName: String = "Seedling",
    val levelImageUrl: String = "",
    val isWithered: Boolean = false,
    val lastLoginDate: String = "", //Gson unable to parse LocalDateTime
    val tasks: List<Task> = emptyList(),
    val email: String = "",
)
