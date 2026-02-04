package com.pocketree.app

import okhttp3.MultipartBody
import okhttp3.RequestBody
import retrofit2.Response
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part
import com.google.gson.annotations.SerializedName

interface MlApiService {
    @Multipart
    @POST("classify")
    fun verifyImage(
        @Part file: MultipartBody.Part,
        @Part("keyword") keyword: RequestBody
    ): retrofit2.Call<VerificationResponse>

//    suspend fun verifyImage(
//        @Part file: MultipartBody.Part,
//        @Part("keyword") keyword: RequestBody
//    ): Response<VerificationResponse>
}