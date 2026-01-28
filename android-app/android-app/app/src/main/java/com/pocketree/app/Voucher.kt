package com.pocketree.app

data class Voucher (
    val voucherID: Int,
    val voucherName: String,
    val description: String,
    var isUsable: Boolean
)