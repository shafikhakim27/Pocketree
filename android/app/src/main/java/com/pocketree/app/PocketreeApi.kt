//package com.pocketree.app
//
//import okhttp3.MultipartBody
//import okhttp3.RequestBody
//import retrofit2.Call
//import retrofit2.Response
//import retrofit2.http.*
//
//data class SubmitResponse(val success: String)
//
//interface PocketreeApi {
//
//    // 1. Get Daily Tasks (Main Backend)
//    // Based on your ASP.NET code, this is actually a POST with [Authorize]
//    @POST("api/Task/GetDailyTasksApi")
//    fun getTask(): Call<List<Task>>
//
//    // 2. AI Image Classification (Python Cloud Run Backend)
//    @Multipart
//    @POST("classify")
//    suspend fun verifyImage(
//        @Part file: MultipartBody.Part,
//        @Part("keyword") keyword: RequestBody
//    ): Response<VerificationResponse>
//
//    @POST("api/User/LoginApi")
//    fun login(@Body loginDto: UserLoginDto): Call<LoginResponse>
//}