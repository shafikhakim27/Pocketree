package com.pocketree.app

import android.content.Context
import android.content.Intent
import com.google.gson.Gson
import okhttp3.OkHttpClient


// interceptor object for Json Web Token


object NetworkClient {
    private var userToken: String? = null
    lateinit var context:Context

    val okHttpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            val requestBuilder = chain.request().newBuilder()

            // ADD THESE DEBUG LOGS
            android.util.Log.d("NetworkClient", "Interceptor: userToken = ${userToken?.take(20)}...")
            android.util.Log.d("NetworkClient", "Interceptor: Request URL = ${chain.request().url}")

            userToken?.let {
                requestBuilder.addHeader("Authorization", "Bearer $it")
                android.util.Log.d("NetworkClient", "Interceptor: Added Authorization header")
            } ?: android.util.Log.e("NetworkClient", "Interceptor: userToken is NULL!")

            val response = chain.proceed(requestBuilder.build())

            if (response.code == 401) {
                val logoutIntent= Intent("ACTION_LOGOUT")
                context.sendBroadcast(logoutIntent)
            }
            response
        }.build()

    val gson = Gson()

    fun setToken(context:Context, token: String?) {
        userToken = token
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        prefs.edit().putString("JWT_TOKEN", token).apply()
        android.util.Log.d("NetworkClient", "setToken: Saved token = ${token?.take(20)}...")
    }

    fun loadToken(context:Context):String? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        userToken = prefs.getString("JWT_TOKEN", null)
        android.util.Log.d("NetworkClient", "loadToken: Loaded token = ${userToken?.take(20)}...")
        return userToken
    }
}
//object NetworkClient {
//    private var userToken: String? = null
//    lateinit var context:Context
//
//    val okHttpClient = OkHttpClient.Builder()
//        .addInterceptor { chain ->
//            val requestBuilder = chain.request().newBuilder()
//            userToken?.let {
//                requestBuilder.addHeader("Authorization", "Bearer $it")
//            }
//
//            val response = chain.proceed(requestBuilder.build())
//
//            // to see if token has expired - if server says 401 then token is dead
//            if (response.code == 401) {
//                val logoutIntent= Intent("ACTION_LOGOUT")
//                context.sendBroadcast(logoutIntent)
//            }
//            response
//        }.build()
//
//    val gson = Gson()
//
//    fun setToken(context:Context, token: String?) {
//        userToken = token
//        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
//        prefs.edit().putString("JWT_TOKEN", token).apply()
//    }
//
//    fun loadToken(context:Context):String? {
//        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
//        userToken = prefs.getString("JWT_TOKEN", null)
//        return userToken
//    }
//}
