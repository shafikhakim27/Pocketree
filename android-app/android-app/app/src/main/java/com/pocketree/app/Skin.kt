package com.pocketree.app

data class Skin(
    val id: Int,
    val name: String,
    val price: Int,
    val imageResId: Int,
    var isRedeemed: Boolean,
    var isEquipped: Boolean
)