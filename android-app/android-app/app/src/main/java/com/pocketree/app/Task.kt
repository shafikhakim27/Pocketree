package com.pocketree.app

enum class TaskDifficulty{
    Easy, Normal, Hard
}

data class Task (
    val id: Int,
    val description: String,
    val difficulty: TaskDifficulty,
    var coinReward: Int,
    val requiresEvidence: Boolean
    )