package com.pocketree.app

import android.content.Context
import android.content.Intent
import com.google.gson.FieldNamingPolicy
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import okhttp3.OkHttpClient

// interceptor object for Json Web Token

object NetworkClient {
    private var userToken: String? = null
    lateinit var context:Context

    val okHttpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            val requestBuilder = chain.request().newBuilder()
            userToken?.let {
                requestBuilder.addHeader("Authorization", "Bearer $it")
            }

            val response = chain.proceed(requestBuilder.build())

            // to see if token has expired - if server says 401 then token is dead
            if (response.code == 401) {
                val logoutIntent= Intent("ACTION_LOGOUT")
                context.sendBroadcast(logoutIntent)
            }
            response
        }.build()

    // val gson = Gson()
    // trial
    val gson = GsonBuilder()
        .setFieldNamingPolicy(FieldNamingPolicy.IDENTITY) // Or check if your backend is sending camelCase
        .create()

    fun setToken(context:Context, token: String?) {
        userToken = token
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        prefs.edit().putString("JWT_TOKEN", token).apply()
    }

    fun loadToken(context:Context):String? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        userToken = prefs.getString("JWT_TOKEN", null)
        return userToken
    }
}
