package com.pocketree.app

enum class TaskDifficulty{
    Easy, Normal, Hard
}

data class Task (
    val taskID: Int,
    val description: String,
    val isCompleted:Boolean,
    val difficulty: TaskDifficulty,
    var coinReward: Int,
    val requiresEvidence: Boolean,
    val keyword: String?
    )