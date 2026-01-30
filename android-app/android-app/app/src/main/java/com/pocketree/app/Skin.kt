package com.pocketree.app

data class Skin(
    val skinId: Int,
    val skinName: String,
    val skinPrice: Int,
    val imageURL: Int,
    var isRedeemed: Boolean,
    var isEquipped: Boolean
)