package com.pocketree.app

import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor // Add this import
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

// ff
// RetrofitClient is the "builder of API services"
object RetrofitClient {
    private const val MAIN_URL = "http://10.113.238.196:5042/"
    private const val AI_URL = "https://clip-verifier-476909679179.us-central1.run.app/"

    // 1. Define the interceptor once
    private val ngrokInterceptor = Interceptor { chain ->
        val request = chain.request().newBuilder()
            .header("ngrok-skip-browser-warning", "true")
            .build()
        chain.proceed(request)
    }

    // 2. Setup Logging (Essential for seeing why it fails)
    private val loggingInterceptor = HttpLoggingInterceptor().apply {
        level = HttpLoggingInterceptor.Level.BODY
    }

    private val okHttpClient = OkHttpClient.Builder()
        .addInterceptor(ngrokInterceptor)    // Use the defined interceptor
        .addInterceptor(loggingInterceptor)  // Use logging to see the error
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    private val aiHttpClient = OkHttpClient.Builder()
        .connectTimeout(60, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .build()

//    val pocketreeInstance: PocketreeApi by lazy {
//        Retrofit.Builder()
//            .baseUrl(MAIN_URL)
//            .client(NetworkClient.okHttpClient)
//            .addConverterFactory(GsonConverterFactory.create())
//            .build()
//            .create(PocketreeApi::class.java)
//    }

    val mlInstance: MlApiService by lazy {
        Retrofit.Builder()
            .baseUrl(AI_URL)
            .client(aiHttpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(MlApiService::class.java)
    }
}