package com.pocketree.app

data class Voucher (
    val voucherID: Int,
    val voucherName: String,
    //val level: Int,
    val description: String,
    var isUsed: Boolean
)