package com.pocketree.app

data class Voucher (
    val voucherID: Int,
    val voucherName: String,
    val description: String,
    var isValid: Boolean, // whether voucher is still usable (not yet expired, not yet used)
    var isUsed: Boolean
)