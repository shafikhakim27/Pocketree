package com.pocketree.app

import android.content.Context
import android.content.Intent
import com.google.gson.FieldNamingPolicy
import com.google.gson.GsonBuilder
import okhttp3.OkHttpClient

// interceptor object for Json Web Token

object NetworkClient {
    private var userToken: String? = null
    lateinit var context:Context

    val okHttpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            // Standard interceptor to add the current token
            val original = chain.request()
            val token = loadToken(context)

            val requestBuilder = original.newBuilder()
            if (!token.isNullOrEmpty()) {
                requestBuilder.addHeader("Authorization", "Bearer $token")
            }

            chain.proceed(requestBuilder.build())
        }
        .authenticator { _, response ->
            if (response.priorResponse != null) {
                triggerLogout()
                // logout if we already tried this request once and it still failed
                return@authenticator null
            }

            if (loadToken(context) == null) {
                triggerLogout()
            }
            null
        }
        .build()


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

    private fun triggerLogout(){
        val logoutIntent = Intent("ACTION_LOGOUT")
        logoutIntent.setPackage(context.packageName) // Safety for Android 14+
        context.sendBroadcast(logoutIntent)
    }
}
