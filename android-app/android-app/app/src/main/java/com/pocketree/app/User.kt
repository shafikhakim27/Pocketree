package com.pocketree.app

import kotlinx.datetime.LocalDateTime
import com.pocketree.app.Task

data class User (
    val id: Int,
    val username: String,
    val passswordHash: String,
    val totalCoins: Int,
    val currentLevelId: Int,
    val lastLoginDate: String, //Gson unable to parse LocalDateTime
    val tasks: List<Task>
)
