package com.pocketree.app

import okhttp3.MultipartBody
import okhttp3.RequestBody
import retrofit2.http.Body
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part

interface MlApiService {
    @Multipart
    @POST("classify")
    fun verifyImage(
        @Part file: MultipartBody.Part,
        @Part("keyword") keyword: RequestBody
    ): retrofit2.Call<VerificationResponse>

    @POST("chat")
    fun chatWithBot(
        @Body request: ChatReq
    ): retrofit2.Call<ChatResponse>
}
