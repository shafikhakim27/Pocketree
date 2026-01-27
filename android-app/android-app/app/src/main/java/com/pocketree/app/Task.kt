package com.pocketree.app

import com.google.gson.annotations.SerializedName

//enum class TaskDifficulty{
//    Easy, Normal, Hard
//}

data class Task (
    @SerializedName("TaskId") val taskID: Int,
    @SerializedName("Description") val description: String,
    @SerializedName("IsCompleted") val isCompleted:Boolean,
    val difficulty: String,
    @SerializedName("CoinReward") var coinReward: Int,
    @SerializedName("RequiresEvidence") val requiresEvidence: Boolean,
    val keyword: String?
    )