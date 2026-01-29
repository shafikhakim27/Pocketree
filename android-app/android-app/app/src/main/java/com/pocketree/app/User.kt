package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class User (
    // default behaviour of asp.net core is to convert keys to camelCase for JSON response
    // regardless of how it is typed in the new {...} block
    @SerializedName("username") val username: String,
    @SerializedName("totalCoins") val totalCoins: Int = 0,
    @SerializedName("levelID") val currentLevelId: Int = 1,
    @SerializedName("levelName") val levelName: String = "Seedling",
    @SerializedName("levelImageURL") val levelImageUrl: String = "",
    @SerializedName("isWithered") val isWithered: Boolean = false,
    val lastLoginDate: String = "", //Gson unable to parse LocalDateTime
)
