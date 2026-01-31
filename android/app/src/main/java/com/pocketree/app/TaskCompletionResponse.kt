package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class TaskCompletionResponse (
    @SerializedName("success") val success: Boolean,
    @SerializedName("Status") val status: String,
    @SerializedName("LevelUp") val levelUp:Boolean,
    @SerializedName("NewCoins") val newCoins:Int,
    @SerializedName("NewLevel") val newLevel:Int,
    @SerializedName("IsWithered") val isWithered:Boolean
)