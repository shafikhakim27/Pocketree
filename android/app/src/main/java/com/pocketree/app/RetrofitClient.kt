package com.pocketree.app

import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

// ff, edited by shirley
object RetrofitClient {
    // to configure local/live testing
    private const val IS_LOCAL_TESTING = false

    // for image verification
    private const val PROD_IMAGE_AI_URL = "https://pocketree-ml-service.azurewebsites.net/"
    //https://pocketree-ml-r7owjdp2qa-as.a.run.app/
    private const val LOCAL_IMAGE_AI_URL = "http://10.0.2.2:8080/"

    // for chatbot function
    private const val PROD_CHAT_AI_URL = "https://pocketree-ml-500550710563.asia-southeast1.run.app/"
    private const val LOCAL_CHAT_AI_URL = "http://10.0.2.2:8080/"

    private val IMAGE_AI_URL = if (IS_LOCAL_TESTING) LOCAL_IMAGE_AI_URL else PROD_IMAGE_AI_URL
    private val CHAT_AI_URL = if (IS_LOCAL_TESTING) LOCAL_CHAT_AI_URL else PROD_CHAT_AI_URL

    private val aiHttpClient = OkHttpClient.Builder()
        .connectTimeout(60, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .build()

    // instance for image verification
    val mlInstance: MlApiService by lazy {
        createService(IMAGE_AI_URL)
    }

    // instance for chatbot verification
    val chatService: MlApiService by lazy {
        createService(CHAT_AI_URL)
    }

    // helper function
    private fun createService(baseUrl:String): MlApiService {
        return Retrofit.Builder()
            .baseUrl(baseUrl)
            .client(aiHttpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(MlApiService::class.java)
    }
}
