package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class User (
    val id: Int,
    @SerializedName("Username") val username: String,
    @SerializedName("TotalCoins") val totalCoins: Int = 0,
    @SerializedName("LevelID") val currentLevelId: Int = 1,
    @SerializedName("LevelName") val levelName: String = "Seedling",
    @SerializedName("LevelImageURL") val levelImageUrl: String = "",
    @SerializedName("IsWithered") val isWithered: Boolean = false,
    val lastLoginDate: String = "", //Gson unable to parse LocalDateTime
    val tasks: List<Task> = emptyList(),
    val email: String = "",
)
