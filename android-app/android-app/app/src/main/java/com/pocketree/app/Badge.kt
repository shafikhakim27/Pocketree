package com.pocketree.app

data class Badge (
    val badgeID: Int,
    val badgeName: String,
    val criteriaType: String,
    val requiredDifficulty: String,
    val requiredCount: Int
)