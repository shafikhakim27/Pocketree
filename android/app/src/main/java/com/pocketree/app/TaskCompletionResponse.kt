package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class TaskCompletionResponse (
    @SerializedName("levelUp") val levelUp:Boolean,
    @SerializedName("newCoins") val newCoins:Int,
    @SerializedName("newLevel") val newLevel:Int,
    @SerializedName("isWithered") val isWithered:Boolean
)