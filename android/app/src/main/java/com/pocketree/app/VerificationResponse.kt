package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class VerificationResponse(
    @SerializedName("verified")
    val isVerified: Boolean
)