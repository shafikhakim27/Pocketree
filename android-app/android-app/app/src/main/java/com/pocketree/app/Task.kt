package com.pocketree.app

import com.google.gson.annotations.SerializedName

//enum class TaskDifficulty{
//    Easy, Normal, Hard
//}

data class Task (
    @SerializedName("taskID") val taskID: Int,
    @SerializedName("description") val description: String,
    @SerializedName("isCompleted") var isCompleted:Boolean,
    @SerializedName("difficulty") val difficulty: String,
    @SerializedName("coinReward") var coinReward: Int,
    @SerializedName("requiresEvidence") val requiresEvidence: Boolean,
    @SerializedName("keyword") val keyword: String?, //for ML
    @SerializedName("category") val category: String? // for ML
    )