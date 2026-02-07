package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class Badge (
    @SerializedName("badgeID") val badgeID: Int,
    @SerializedName("badgeName") val badgeName: String?,
    @SerializedName("badgeDescription") val badgeDescription: String?,
    @SerializedName("badgeImageURL") val badgeImageUrl: String?,
    @SerializedName("dateEarned") val dateEarned: String? = null,
    val criteriaType: String?,
    val requiredDifficulty: String?,
    val requiredCount: Int?
)