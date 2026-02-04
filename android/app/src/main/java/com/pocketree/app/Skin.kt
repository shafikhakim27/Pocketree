package com.pocketree.app

import com.google.gson.annotations.SerializedName

data class Skin(
    @SerializedName("SkinID", alternate = ["skinID", "skinId", "id", "SkinId"])
    val skinID: Int,

    @SerializedName("SkinName", alternate = ["skinName"])
    val skinName: String,

    @SerializedName("SkinPrice", alternate = ["skinPrice", "price"])
    val skinPrice: Int,

    @SerializedName("ImageURL", alternate = ["imageURL", "imageUrl", "Image"])
    val imageURL: String,

    @SerializedName("IsRedeemed", alternate = ["isRedeemed"])
    var isRedeemed: Boolean = false,

    @SerializedName("IsEquipped", alternate = ["isEquipped"])
    var isEquipped: Boolean = false
)